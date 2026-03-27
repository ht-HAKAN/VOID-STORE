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
            // kullanıcı ticaret alanlarını hazırla
            UserCommerceSchemaManager.EnsureSchema();
        }

        public decimal GetBalance(int userId)
        {
            // kullanıcının güncel bakiyesini çek
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT Balance
                  FROM Users
                  WHERE UserId = @UserId
                  LIMIT 1;",
                new SqlParameter("@UserId", userId));

            // sonuç yoksa sıfıra dön
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

            // tabloyu görünüm modeline çevir
            List<WalletTransactionItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                // satırdaki ham değerleri çıkar
                decimal amount = Convert.ToDecimal(row["Amount"], CultureInfo.InvariantCulture);
                DateTime createdAt = Convert.ToDateTime(row["CreatedAt"], CultureInfo.InvariantCulture);
                string transactionType = row["TransactionType"]?.ToString() ?? string.Empty;
                string description = row["Description"]?.ToString() ?? string.Empty;

                // ekrana doğrudan basılan modeli kur
                items.Add(new WalletTransactionItem
                {
                    Title = BuildTransactionTitle(transactionType),
                    Description = string.IsNullOrWhiteSpace(description) ? "İşlem kaydı" : description,
                    AmountText = BuildAmountText(amount),
                    CreatedAtText = createdAt.ToString("dd.MM.yyyy HH:mm"),
                    IsPositive = amount >= 0
                });
            }

            return items;
        }

        public decimal AddBalance(int userId, int amount)
        {
            // oturum olmadan işleme girme
            if (userId <= 0)
            {
                throw new InvalidOperationException("Bu işlem için giriş yapmanız gerekiyor");
            }

            // sıfır ve altı tutarı reddet
            if (amount <= 0)
            {
                throw new InvalidOperationException("Yüklenecek tutar sıfırdan büyük olmalıdır");
            }

            // bakiye ve hareket kaydını aynı transaction içinde tut
            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // önceki bakiyeyi kilitleyerek al
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

                // yükleme hareketini kaydet
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
                    new SqlParameter("@Description", $"{amount} TL bakiye yükleme"));

                // tüm yazımlar tamamsa kalıcı yap
                transaction.Commit();
                return balanceAfter;
            }
            catch
            {
                // sorun olursa tek noktadan geri dön
                transaction.Rollback();
                throw;
            }
        }

        public IReadOnlyList<CartGameItem> GetCartItems(int userId)
        {
            // misafir kullanıcıda boş liste dön
            if (userId <= 0)
            {
                return new List<CartGameItem>();
            }

            // sepetteki oyunları aktif oyunlarla birleştir
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

            // tabloyu kart listesine dönüştür
            List<CartGameItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                // kapak yolunu güvenli al
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;

                // fiyatı para tipine çevir
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
            // misafir kullanıcıda boş kütüphane dön
            if (userId <= 0)
            {
                return new List<LibraryGameItem>();
            }

            // sahip olunan oyunları yeni tarihe göre getir
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

            // kütüphane kartlarını üret
            List<LibraryGameItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                // kapak yolunu güvenli al
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;

                // satın alma verilerini çıkar
                decimal price = Convert.ToDecimal(row["PurchasedPrice"], CultureInfo.InvariantCulture);
                DateTime purchasedAt = Convert.ToDateTime(row["PurchasedAt"], CultureInfo.InvariantCulture);

                // kütüphane kart modelini doldur
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
            // sahip olunan oyun kodlarını tek sorguda al
            return GetGameIdSet(
                @"SELECT GameId
                  FROM UserLibrary
                  WHERE UserId = @UserId;",
                userId);
        }

        public HashSet<int> GetCartGameIds(int userId)
        {
            // sepetteki oyun kodlarını tek sorguda al
            return GetGameIdSet(
                @"SELECT GameId
                  FROM CartItems
                  WHERE UserId = @UserId;",
                userId);
        }

        public void AddToCart(int userId, int gameId)
        {
            // oturum kontrolü yap
            if (userId <= 0)
            {
                throw new InvalidOperationException("Sepet işlemi için giriş yapmanız gerekiyor");
            }

            // oyun geçerli mi kontrol et
            if (!GameExists(gameId))
            {
                throw new InvalidOperationException("Seçilen oyun mağazada bulunamadı");
            }

            // sahip olunan oyun tekrar sepete girmesin
            if (GetOwnedGameIds(userId).Contains(gameId))
            {
                throw new InvalidOperationException("Bu oyun zaten kütüphanenizde bulunuyor");
            }

            // aynı oyunu ikinci kez sepete alma
            if (GetCartGameIds(userId).Contains(gameId))
            {
                throw new InvalidOperationException("Bu oyun zaten sepetinizde bulunuyor");
            }

            // yeni sepet kaydını yaz
            DatabaseManager.ExecuteNonQuery(
                @"INSERT INTO CartItems (UserId, GameId)
                  VALUES (@UserId, @GameId);",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));
        }

        public void RemoveFromCart(int userId, int gameId)
        {
            // misafirde sessizce çık
            if (userId <= 0)
            {
                return;
            }

            // sepet kaydını sil
            DatabaseManager.ExecuteNonQuery(
                @"DELETE FROM CartItems
                  WHERE UserId = @UserId
                    AND GameId = @GameId;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));
        }

        public CheckoutResult CheckoutCart(int userId)
        {
            // satın alma sadece oturumla ilerler
            if (userId <= 0)
            {
                throw new InvalidOperationException("Satın alma işlemi için giriş yapmanız gerekiyor");
            }

            // kütüphane sepet bakiye hareketine tek transaction uygula
            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // satın alınacak oyunları çek
                List<(int GameId, string Title, decimal Price)> cartEntries = GetCartEntries(connection, transaction, userId);

                // boş sepette checkout yapma
                if (cartEntries.Count == 0)
                {
                    throw new InvalidOperationException("Satın alınacak oyun bulunamadı");
                }

                // toplam tutarı hesapla
                decimal balanceBefore = GetLockedBalance(connection, transaction, userId);
                decimal totalAmount = cartEntries.Sum(item => item.Price);

                // bakiye yetersizse işlemi durdur
                if (balanceBefore < totalAmount)
                {
                    throw new InvalidOperationException("Bakiyeniz bu satın alma işlemi için yeterli değil");
                }

                // tek satın alma zamanı kullan
                DateTime purchaseTime = DateTime.Now;

                // her oyunu kütüphaneye ekle
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

                // kullanıcı bakiyesini güncelle
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

                // toplam satın alma hareketini yaz
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
                    new SqlParameter("@Description", $"{cartEntries.Count} oyun satın alındı"));

                // tüm adımlar tamamsa kalıcı yap
                transaction.Commit();

                // ekrana dönecek sonucu üret
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
                // yarım veri kalmasın
                transaction.Rollback();
                throw;
            }
        }

        private HashSet<int> GetGameIdSet(string query, int userId)
        {
            // ham tabloyu çek
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
            // sadece onaylı ve aktif oyunlar sepete girebilir
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
            // kütüphane ile çakışan oyunları çekme
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

            // reader sonucunu tuple listesine dönüştür
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
            // bakiye satırını kilitleyerek çek
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

            // kayıt yoksa işleme devam etme
            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException("Kullanıcı kaydı bulunamadı");
            }

            return Convert.ToDecimal(result, CultureInfo.InvariantCulture);
        }

        private void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string query, params SqlParameter[] parameters)
        {
            // ortak parametreli komut yürütücüsü
            using SqlCommand command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            command.ExecuteNonQuery();
        }

        private string BuildTransactionTitle(string transactionType)
        {
            // işlem tipini okunur başlığa çevir
            return transactionType switch
            {
                "topup" => "Bakiye yükleme",
                "purchase" => "Satın alma",
                "refund" => "İade",
                _ => "Cüzdan hareketi"
            };
        }

        private string BuildAmountText(decimal amount)
        {
            // artı eksi işaretini tek yerde oluştur
            string prefix = amount >= 0 ? "+" : "-";
            decimal normalizedAmount = Math.Abs(amount);
            return $"{prefix}{FormatPrice(normalizedAmount)}";
        }

        private string FormatPrice(decimal price)
        {
            // para görünümü tüm uygulamada aynı olsun
            return $"₺{price.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))}";
        }
    }
}
