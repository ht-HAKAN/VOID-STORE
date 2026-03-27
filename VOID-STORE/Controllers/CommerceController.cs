using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using SqlCommand = MySql.Data.MySqlClient.MySqlCommand;
using SqlConnection = MySql.Data.MySqlClient.MySqlConnection;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using SqlTransaction = MySql.Data.MySqlClient.MySqlTransaction;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class CommerceController
    {
        public CommerceController()
        {
            // hafta 7 tablolari hazir olsun
            UserCommerceSchemaManager.EnsureSchema();
        }

        public decimal GetBalance(int userId)
        {
            // kullanicinin guncel bakiyesini cek
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT Balance
                  FROM Users
                  WHERE UserId = @UserId
                  LIMIT 1;",
                new SqlParameter("@UserId", userId));

            // sonuc yoksa sifira don
            return result == null || result == DBNull.Value
                ? 0
                : Convert.ToDecimal(result, CultureInfo.InvariantCulture);
        }

        public IReadOnlyList<WalletTransactionItem> GetRecentTransactions(int userId, int take = 8)
        {
            // son hareketleri yeni tarihten eskiye getir
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT TransactionType, Amount, Description, CreatedAt
                  FROM WalletTransactions
                  WHERE UserId = @UserId
                  ORDER BY CreatedAt DESC, TransactionId DESC
                  LIMIT @Take;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Take", take));

            // tabloyu gorunum modeline cevir
            List<WalletTransactionItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                // satirdaki ham degerleri cikar
                decimal amount = Convert.ToDecimal(row["Amount"], CultureInfo.InvariantCulture);
                DateTime createdAt = Convert.ToDateTime(row["CreatedAt"], CultureInfo.InvariantCulture);
                string transactionType = row["TransactionType"]?.ToString() ?? string.Empty;
                string description = row["Description"]?.ToString() ?? string.Empty;

                // ekrana dogrudan basilan modeli kur
                items.Add(new WalletTransactionItem
                {
                    Title = BuildTransactionTitle(transactionType),
                    Description = string.IsNullOrWhiteSpace(description) ? $"I\u015flem kayd\u0131" : description,
                    AmountText = BuildAmountText(amount),
                    CreatedAtText = createdAt.ToString("dd.MM.yyyy HH:mm"),
                    IsPositive = amount >= 0
                });
            }

            return items;
        }

        public decimal AddBalance(int userId, int amount)
        {
            // oturum olmadan isleme girme
            if (userId <= 0)
            {
                throw new InvalidOperationException($"Bu i\u015flem i\u00e7in giri\u015f yapman\u0131z gerekiyor");
            }

            // sifir ve alti tutari reddet
            if (amount <= 0)
            {
                throw new InvalidOperationException($"Y\u00fcklenecek tutar s\u0131f\u0131rdan b\u00fcy\u00fck olmal\u0131d\u0131r");
            }

            // bakiye ve hareket kaydini ayni transaction icinde tut
            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // onceki bakiyeyi kilitleyerek al
                decimal balanceBefore = GetLockedBalance(connection, transaction, userId);
                decimal balanceAfter = balanceBefore + amount;

                // yeni bakiyeyi yaz
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"UPDATE Users
                      SET Balance = @Balance
                      WHERE UserId = @UserId;",
                    new SqlParameter("@Balance", balanceAfter),
                    new SqlParameter("@UserId", userId));

                // yukleme hareketini kaydet
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"INSERT INTO WalletTransactions
                        (UserId, TransactionType, Amount, BalanceBefore, BalanceAfter, Description)
                      VALUES
                        (@UserId, 'topup', @Amount, @BalanceBefore, @BalanceAfter, @Description);",
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Amount", amount),
                    new SqlParameter("@BalanceBefore", balanceBefore),
                    new SqlParameter("@BalanceAfter", balanceAfter),
                    new SqlParameter("@Description", $"{amount} TL bakiye y\u00fckleme"));

                // tum yazimlar tamamsa kalici yap
                transaction.Commit();
                return balanceAfter;
            }
            catch
            {
                // sorun olursa tek noktadan geri don
                transaction.Rollback();
                throw;
            }
        }

        public IReadOnlyList<CartGameItem> GetCartItems(int userId)
        {
            // misafir kullanicida bos liste don
            if (userId <= 0)
            {
                return new List<CartGameItem>();
            }

            // sepetteki oyunlari aktif oyunlarla birlestir
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    g.GameId,
                    g.Title,
                    g.Category,
                    g.Price,
                    g.CoverImagePath
                  FROM CartItems c
                  INNER JOIN Games g ON g.GameId = c.GameId
                  WHERE c.UserId = @UserId
                    AND g.ApprovalStatus = 'approved'
                    AND g.IsActive = 1
                  ORDER BY c.CreatedAt DESC;",
                new SqlParameter("@UserId", userId));

            // tabloyu kart listesine donustur
            List<CartGameItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                // kapak yolunu guvenli al
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;

                // fiyati para tipine cevir
                decimal price = Convert.ToDecimal(row["Price"], CultureInfo.InvariantCulture);

                // sepet kart modelini doldur
                items.Add(new CartGameItem
                {
                    GameId = Convert.ToInt32(row["GameId"]),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                    PriceAmount = price,
                    PriceText = FormatPrice(price),
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath)
                });
            }

            return items;
        }

        public IReadOnlyList<LibraryGameItem> GetLibraryGames(int userId)
        {
            // misafir kullanicida bos kutuphane don
            if (userId <= 0)
            {
                return new List<LibraryGameItem>();
            }

            // sahip olunan oyunlari yeni tarihe gore getir
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    g.GameId,
                    g.Title,
                    g.Category,
                    g.CoverImagePath,
                    ul.PurchasedPrice,
                    ul.PurchasedAt
                  FROM UserLibrary ul
                  INNER JOIN Games g ON g.GameId = ul.GameId
                  WHERE ul.UserId = @UserId
                  ORDER BY ul.PurchasedAt DESC, ul.LibraryItemId DESC;",
                new SqlParameter("@UserId", userId));

            // kutuphane kartlarini uret
            List<LibraryGameItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                // kapak yolunu guvenli al
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;

                // satin alma verilerini cikar
                decimal price = Convert.ToDecimal(row["PurchasedPrice"], CultureInfo.InvariantCulture);
                DateTime purchasedAt = Convert.ToDateTime(row["PurchasedAt"], CultureInfo.InvariantCulture);

                // kutuphane kart modelini doldur
                items.Add(new LibraryGameItem
                {
                    GameId = Convert.ToInt32(row["GameId"]),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                    PriceText = FormatPrice(price),
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath),
                    PurchasedAtText = purchasedAt.ToString("dd.MM.yyyy")
                });
            }

            return items;
        }

        public HashSet<int> GetOwnedGameIds(int userId)
        {
            // sahip olunan oyun kodlarini tek sorguda al
            return GetGameIdSet(
                @"SELECT GameId
                  FROM UserLibrary
                  WHERE UserId = @UserId;",
                userId);
        }

        public HashSet<int> GetCartGameIds(int userId)
        {
            // sepetteki oyun kodlarini tek sorguda al
            return GetGameIdSet(
                @"SELECT GameId
                  FROM CartItems
                  WHERE UserId = @UserId;",
                userId);
        }

        public void AddToCart(int userId, int gameId)
        {
            // oturum kontrolu yap
            if (userId <= 0)
            {
                throw new InvalidOperationException($"Sepet i\u015flemi i\u00e7in giri\u015f yapman\u0131z gerekiyor");
            }

            // oyun gecerli mi kontrol et
            if (!GameExists(gameId))
            {
                throw new InvalidOperationException($"Se\u00e7ilen oyun ma\u011fazada bulunamad\u0131");
            }

            // sahip olunan oyun tekrar sepete girmesin
            if (GetOwnedGameIds(userId).Contains(gameId))
            {
                throw new InvalidOperationException($"Bu oyun zaten k\u00fct\u00fcphanenizde bulunuyor");
            }

            // ayni oyunu ikinci kez sepete alma
            if (GetCartGameIds(userId).Contains(gameId))
            {
                throw new InvalidOperationException($"Bu oyun zaten sepetinizde bulunuyor");
            }

            // yeni sepet kaydini yaz
            DatabaseManager.ExecuteNonQuery(
                @"INSERT INTO CartItems (UserId, GameId)
                  VALUES (@UserId, @GameId);",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));
        }

        public void RemoveFromCart(int userId, int gameId)
        {
            // misafirde sessizce cik
            if (userId <= 0)
            {
                return;
            }

            // sepet kaydini sil
            DatabaseManager.ExecuteNonQuery(
                @"DELETE FROM CartItems
                  WHERE UserId = @UserId
                    AND GameId = @GameId;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));
        }

        public CheckoutResult CheckoutCart(int userId)
        {
            // satin alma sadece oturumla ilerler
            if (userId <= 0)
            {
                throw new InvalidOperationException($"Sat\u0131n alma i\u015flemi i\u00e7in giri\u015f yapman\u0131z gerekiyor");
            }

            // kutuphane sepete bakiye hareketine tek transaction uygula
            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // satin alinacak oyunlari cek
                List<(int GameId, string Title, decimal Price)> cartEntries = GetCartEntries(connection, transaction, userId);

                // bos sepette checkout yapma
                if (cartEntries.Count == 0)
                {
                    throw new InvalidOperationException($"Sat\u0131n al\u0131nacak oyun bulunamad\u0131");
                }

                // toplam tutari hesapla
                decimal balanceBefore = GetLockedBalance(connection, transaction, userId);
                decimal totalAmount = cartEntries.Sum(item => item.Price);

                // bakiye yetersizse islemi durdur
                if (balanceBefore < totalAmount)
                {
                    throw new InvalidOperationException($"Bakiyeniz bu sat\u0131n alma i\u015flemi i\u00e7in yeterli de\u011fil");
                }

                // tek satin alma zamani kullan
                DateTime purchaseTime = DateTime.Now;

                // her oyunu kutuphaneye ekle
                foreach ((int gameId, _, decimal price) in cartEntries)
                {
                    ExecuteNonQuery(
                        connection,
                        transaction,
                        @"INSERT IGNORE INTO UserLibrary (UserId, GameId, PurchasedPrice, PurchasedAt)
                          VALUES (@UserId, @GameId, @PurchasedPrice, @PurchasedAt);",
                        new SqlParameter("@UserId", userId),
                        new SqlParameter("@GameId", gameId),
                        new SqlParameter("@PurchasedPrice", price),
                        new SqlParameter("@PurchasedAt", purchaseTime));
                }

                // yeni bakiyeyi hesapla
                decimal balanceAfter = balanceBefore - totalAmount;

                // kullanici bakiyesini guncelle
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"UPDATE Users
                      SET Balance = @Balance
                      WHERE UserId = @UserId;",
                    new SqlParameter("@Balance", balanceAfter),
                    new SqlParameter("@UserId", userId));

                // sepeti tamamen temizle
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"DELETE FROM CartItems
                      WHERE UserId = @UserId;",
                    new SqlParameter("@UserId", userId));

                // toplam satin alma hareketini yaz
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"INSERT INTO WalletTransactions
                        (UserId, TransactionType, Amount, BalanceBefore, BalanceAfter, Description)
                      VALUES
                        (@UserId, 'purchase', @Amount, @BalanceBefore, @BalanceAfter, @Description);",
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Amount", -totalAmount),
                    new SqlParameter("@BalanceBefore", balanceBefore),
                    new SqlParameter("@BalanceAfter", balanceAfter),
                    new SqlParameter("@Description", $"{cartEntries.Count} oyun sat\u0131n al\u0131nd\u0131"));

                // tum adimlar tamamsa kalici yap
                transaction.Commit();

                // ekrana donecek sonucu uret
                return new CheckoutResult
                {
                    ItemCount = cartEntries.Count,
                    TotalAmount = totalAmount,
                    TotalText = FormatPrice(totalAmount),
                    BalanceAfter = balanceAfter,
                    BalanceAfterText = FormatPrice(balanceAfter)
                };
            }
            catch
            {
                // yarim veri kalmasin
                transaction.Rollback();
                throw;
            }
        }

        private HashSet<int> GetGameIdSet(string query, int userId)
        {
            // ham tabloyu cek
            DataTable table = DatabaseManager.ExecuteQuery(
                query,
                new SqlParameter("@UserId", userId));

            // oyun idlerini sete topla
            HashSet<int> ids = new();

            foreach (DataRow row in table.Rows)
            {
                ids.Add(Convert.ToInt32(row["GameId"]));
            }

            return ids;
        }

        private bool GameExists(int gameId)
        {
            // sadece onayli ve aktif oyunlar sepete girebilir
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM Games
                  WHERE GameId = @GameId
                    AND ApprovalStatus = 'approved'
                    AND IsActive = 1;",
                new SqlParameter("@GameId", gameId));

            return result != null && Convert.ToInt32(result) > 0;
        }

        private List<(int GameId, string Title, decimal Price)> GetCartEntries(SqlConnection connection, SqlTransaction transaction, int userId)
        {
            // kutuphane ile cakisan oyunlari cekme
            using SqlCommand command = new SqlCommand(
                @"SELECT
                    g.GameId,
                    g.Title,
                    g.Price
                  FROM CartItems c
                  INNER JOIN Games g ON g.GameId = c.GameId
                  LEFT JOIN UserLibrary ul ON ul.UserId = c.UserId AND ul.GameId = c.GameId
                  WHERE c.UserId = @UserId
                    AND g.ApprovalStatus = 'approved'
                    AND g.IsActive = 1
                    AND ul.LibraryItemId IS NULL
                  ORDER BY c.CreatedAt ASC;",
                connection,
                transaction);

            command.Parameters.AddWithValue("@UserId", userId);

            // reader sonucunu tuple listesine donustur
            using var reader = command.ExecuteReader();
            List<(int GameId, string Title, decimal Price)> items = new();

            while (reader.Read())
            {
                items.Add((
                    reader.GetInt32("GameId"),
                    reader.GetString("Title"),
                    reader.GetDecimal("Price")));
            }

            return items;
        }

        private decimal GetLockedBalance(SqlConnection connection, SqlTransaction transaction, int userId)
        {
            // bakiye satirini kilitleyerek cek
            using SqlCommand command = new SqlCommand(
                @"SELECT Balance
                  FROM Users
                  WHERE UserId = @UserId
                  LIMIT 1
                  FOR UPDATE;",
                connection,
                transaction);

            command.Parameters.AddWithValue("@UserId", userId);
            object? result = command.ExecuteScalar();

            // kayit yoksa isleme devam etme
            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException($"Kullan\u0131c\u0131 kayd\u0131 bulunamad\u0131");
            }

            return Convert.ToDecimal(result, CultureInfo.InvariantCulture);
        }

        private void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string query, params SqlParameter[] parameters)
        {
            // ortak parametreli komut yurutucusu
            using SqlCommand command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            command.ExecuteNonQuery();
        }

        private string BuildTransactionTitle(string transactionType)
        {
            // islem tipini okunur basliga cevir
            return transactionType switch
            {
                "topup" => $"Bakiye y\u00fckleme",
                "purchase" => $"Sat\u0131n alma",
                "refund" => $"Iade",
                _ => $"C\u00fczdan hareketi"
            };
        }

        private string BuildAmountText(decimal amount)
        {
            // arti eksi isaretini tek yerde olustur
            string prefix = amount >= 0 ? "+" : "-";
            decimal normalizedAmount = Math.Abs(amount);
            return $"{prefix}{FormatPrice(normalizedAmount)}";
        }

        private string FormatPrice(decimal price)
        {
            // para gorunumu tum uygulamada ayni olsun
            return $"\u20BA{price.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))}";
        }
    }
}
