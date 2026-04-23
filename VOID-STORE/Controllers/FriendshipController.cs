using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using SqlConnection = MySql.Data.MySqlClient.MySqlConnection;
using SqlCommand = MySql.Data.MySqlClient.MySqlCommand;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using SqlTransaction = MySql.Data.MySqlClient.MySqlTransaction;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    // arkadaşlık mantığı
    public class FriendshipController
    {
        private const string StatusPending = "pending";
        private const string StatusAccepted = "accepted";

        // arama limiti
        private const int DefaultSearchLimit = 20;

        public FriendshipController()
        {
            
        }

        // kullanıcı ara ve ilişki durumunu getir
        public IReadOnlyList<FriendSearchResultItem> SearchUsers(int currentUserId, string query, int limit = DefaultSearchLimit)
        {
            // boş sorgu kontrolü
            string normalizedQuery = (query ?? string.Empty).Trim();
            if (normalizedQuery.Length == 0)
            {
                return new List<FriendSearchResultItem>();
            }

            // misafir kontrolü
            if (currentUserId <= 0)
            {
                return new List<FriendSearchResultItem>();
            }

            FriendshipSchemaManager.EnsureSchema();

            // kullanıcıları ve mevcut ilişkileri çek (LEFT JOIN)
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    u.UserId,
                    u.Username,
                    COALESCE(u.ProfileImagePath, '') AS ProfileImagePath,
                    f.RequesterUserId AS RelRequester,
                    f.AddresseeUserId AS RelAddressee,
                    f.Status AS RelStatus
                  FROM Users u
                  LEFT JOIN Friendships f
                    ON (f.RequesterUserId = @Viewer AND f.AddresseeUserId = u.UserId)
                    OR (f.RequesterUserId = u.UserId AND f.AddresseeUserId = @Viewer)
                  WHERE u.UserId <> @Viewer
                    AND u.Username LIKE CONCAT('%', @Query, '%')
                  ORDER BY
                    CASE WHEN u.Username LIKE CONCAT(@Query, '%') THEN 0 ELSE 1 END,
                    u.Username ASC
                  LIMIT @Limit;",
                new SqlParameter("@Viewer", currentUserId),
                new SqlParameter("@Query", normalizedQuery),
                new SqlParameter("@Limit", Math.Max(1, limit)));

            List<FriendSearchResultItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                int userId = Convert.ToInt32(row["UserId"], CultureInfo.InvariantCulture);
                string username = row["Username"]?.ToString() ?? string.Empty;
                string avatarPath = row["ProfileImagePath"]?.ToString() ?? string.Empty;

                FriendshipRelationshipStatus status = ResolveRelationshipFromRow(row, currentUserId, userId);

                FriendSearchActionAppearance appearance = BuildActionAppearance(status);

                items.Add(new FriendSearchResultItem
                {
                    UserId = userId,
                    Username = username,
                    AvatarLetter = BuildAvatarLetter(username),
                    AvatarImagePath = avatarPath,
                    AvatarPreview = GameAssetManager.LoadBitmap(avatarPath),
                    RelationshipStatus = status,
                    StatusText = appearance.StatusText,
                    StatusAccent = appearance.StatusAccent,
                    ActionButtonText = appearance.ActionText,
                    ActionButtonAccent = appearance.StatusAccent,
                    ActionButtonBackground = appearance.ActionBackground,
                    ActionButtonForeground = appearance.ActionForeground,
                    IsActionEnabled = appearance.Enabled
                });
            }

            return items;
        }

        // arkadaş listesini getir
        public IReadOnlyList<FriendListItem> GetFriends(int userId)
        {
            // misafir kontrolü
            if (userId <= 0)
            {
                return new List<FriendListItem>();
            }

            FriendshipSchemaManager.EnsureSchema();

            // karşılıklı kayıtlar için eşleşeni seç (CASE)
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    u.UserId,
                    u.Username,
                    COALESCE(u.Bio, '') AS Bio,
                    COALESCE(u.ProfileImagePath, '') AS ProfileImagePath,
                    f.RespondedAt AS FriendSince
                  FROM Friendships f
                  INNER JOIN Users u
                    ON u.UserId = CASE
                        WHEN f.RequesterUserId = @UserId THEN f.AddresseeUserId
                        ELSE f.RequesterUserId
                    END
                  WHERE f.Status = @Status
                    AND (f.RequesterUserId = @UserId OR f.AddresseeUserId = @UserId)
                  ORDER BY u.Username ASC;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Status", StatusAccepted));

            List<FriendListItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                string username = row["Username"]?.ToString() ?? string.Empty;
                string avatarPath = row["ProfileImagePath"]?.ToString() ?? string.Empty;
                DateTime? since = row["FriendSince"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(row["FriendSince"], CultureInfo.InvariantCulture);

                items.Add(new FriendListItem
                {
                    UserId = Convert.ToInt32(row["UserId"], CultureInfo.InvariantCulture),
                    Username = username,
                    Bio = TrimBio(row["Bio"]?.ToString()),
                    AvatarLetter = BuildAvatarLetter(username),
                    AvatarImagePath = avatarPath,
                    AvatarPreview = GameAssetManager.LoadBitmap(avatarPath),
                    FriendsSinceText = since.HasValue ? $"Arkadaş olundu: {since.Value:dd.MM.yyyy}" : string.Empty
                });
            }

            return items;
        }

        // gelen istekleri listele
        public IReadOnlyList<FriendRequestItem> GetIncomingRequests(int userId)
        {
            return GetRequests(
                userId,
                FriendRequestDirection.Incoming,
                @"SELECT
                    u.UserId,
                    u.Username,
                    COALESCE(u.ProfileImagePath, '') AS ProfileImagePath,
                    f.CreatedAt AS SentAt
                  FROM Friendships f
                  INNER JOIN Users u ON u.UserId = f.RequesterUserId
                  WHERE f.AddresseeUserId = @UserId
                    AND f.Status = @Status
                  ORDER BY f.CreatedAt DESC;");
        }

        // giden istekleri listele
        public IReadOnlyList<FriendRequestItem> GetOutgoingRequests(int userId)
        {
            return GetRequests(
                userId,
                FriendRequestDirection.Outgoing,
                @"SELECT
                    u.UserId,
                    u.Username,
                    COALESCE(u.ProfileImagePath, '') AS ProfileImagePath,
                    f.CreatedAt AS SentAt
                  FROM Friendships f
                  INNER JOIN Users u ON u.UserId = f.AddresseeUserId
                  WHERE f.RequesterUserId = @UserId
                    AND f.Status = @Status
                  ORDER BY f.CreatedAt DESC;");
        }

        // bekleyen istek sayısı (navbar için)
        public int GetIncomingRequestCount(int userId)
        {
            if (userId <= 0)
            {
                return 0;
            }

            FriendshipSchemaManager.EnsureSchema();

            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM Friendships
                  WHERE AddresseeUserId = @UserId
                    AND Status = @Status;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Status", StatusPending));

            return result == null || result == DBNull.Value
                ? 0
                : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        // profil için arkadaş sayısı
        public int GetFriendsCount(int userId)
        {
            if (userId <= 0)
            {
                return 0;
            }

            FriendshipSchemaManager.EnsureSchema();

            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM Friendships
                  WHERE Status = @Status
                    AND (RequesterUserId = @UserId OR AddresseeUserId = @UserId);",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Status", StatusAccepted));

            return result == null || result == DBNull.Value
                ? 0
                : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        // iki kullanıcı arasındaki güncel ilişki
        public FriendshipRelationshipStatus GetRelationship(int viewerUserId, int targetUserId)
        {
            // geçersiz id kontrolü
            if (viewerUserId <= 0 || targetUserId <= 0)
            {
                return FriendshipRelationshipStatus.None;
            }

            // kendi profili kontrolü
            if (viewerUserId == targetUserId)
            {
                return FriendshipRelationshipStatus.Self;
            }

            FriendshipSchemaManager.EnsureSchema();

            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT RequesterUserId, AddresseeUserId, Status
                  FROM Friendships
                  WHERE (RequesterUserId = @Viewer AND AddresseeUserId = @Target)
                     OR (RequesterUserId = @Target AND AddresseeUserId = @Viewer)
                  LIMIT 1;",
                new SqlParameter("@Viewer", viewerUserId),
                new SqlParameter("@Target", targetUserId));

            if (table.Rows.Count == 0)
            {
                return FriendshipRelationshipStatus.None;
            }

            return ResolveRelationshipFromRow(table.Rows[0], viewerUserId, targetUserId);
        }

        // arkadaşlık isteği gönder (karşıdan istek varsa otomatik kabul et)
        public FriendshipRelationshipStatus SendRequest(int currentUserId, int targetUserId)
        {
            if (currentUserId <= 0)
            {
                throw new InvalidOperationException("Arkadaş özelliği için giriş yapmanız gerekiyor.");
            }

            // kendine istek kontrolü
            if (currentUserId == targetUserId)
            {
                throw new InvalidOperationException("Kendinize arkadaşlık isteği gönderemezsiniz.");
            }

            EnsureUserExists(targetUserId);
            FriendshipSchemaManager.EnsureSchema();

            // transaction ile istek/kabul işlemini yap
            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                DataRow? existing = GetPairRow(connection, transaction, currentUserId, targetUserId);

                if (existing == null)
                {
                    // yeni istek (pending) oluştur
                    ExecuteNonQuery(
                        connection,
                        transaction,
                        @"INSERT INTO Friendships (RequesterUserId, AddresseeUserId, Status)
                          VALUES (@Requester, @Addressee, @Status);",
                        new SqlParameter("@Requester", currentUserId),
                        new SqlParameter("@Addressee", targetUserId),
                        new SqlParameter("@Status", StatusPending));

                    transaction.Commit();
                    return FriendshipRelationshipStatus.PendingSent;
                }

                // mevcut durum kontrolleri
                if (existingStatus == StatusAccepted)
                {
                    throw new InvalidOperationException("Bu kullanıcı zaten arkadaşınız.");
                }

                // mükerrer istek kontrolü
                if (existingStatus == StatusPending && existingRequester == currentUserId)
                {
                    throw new InvalidOperationException("Bu kullanıcıya zaten istek gönderdiniz.");
                }

                // karşı taraf daha önce istek atmışsa arkadaş yap
                if (existingStatus == StatusPending && existingRequester == targetUserId)
                {
                    ExecuteNonQuery(
                        connection,
                        transaction,
                        @"UPDATE Friendships
                          SET Status = @Status,
                              RespondedAt = NOW()
                          WHERE RequesterUserId = @Requester
                            AND AddresseeUserId = @Addressee;",
                        new SqlParameter("@Status", StatusAccepted),
                        new SqlParameter("@Requester", targetUserId),
                        new SqlParameter("@Addressee", currentUserId));

                    transaction.Commit();
                    return FriendshipRelationshipStatus.Friends;
                }

                throw new InvalidOperationException("İstek gönderilemedi.");
            }
            catch
            {
                // hata durumunda geri al
                transaction.Rollback();
                throw;
            }
        }

        // isteği kabul et
        public void AcceptRequest(int currentUserId, int requesterUserId)
        {
            if (currentUserId <= 0)
            {
                throw new InvalidOperationException("Arkadaş özelliği için giriş yapmanız gerekiyor.");
            }

            FriendshipSchemaManager.EnsureSchema();

            // kaydı accepted olarak güncelle
            int affected = ExecuteAffected(
                @"UPDATE Friendships
                  SET Status = @NewStatus,
                      RespondedAt = NOW()
                  WHERE RequesterUserId = @Requester
                    AND AddresseeUserId = @Addressee
                    AND Status = @OldStatus;",
                new SqlParameter("@NewStatus", StatusAccepted),
                new SqlParameter("@Requester", requesterUserId),
                new SqlParameter("@Addressee", currentUserId),
                new SqlParameter("@OldStatus", StatusPending));

            // satır etkilenmediyse istek yoktur
            if (affected == 0)
            {
                throw new InvalidOperationException("Kabul edilecek istek bulunamadı.");
            }
        }

        // isteği reddet (kaydı sil)
        public void RejectRequest(int currentUserId, int requesterUserId)
        {
            if (currentUserId <= 0)
            {
                throw new InvalidOperationException("Arkadaş özelliği için giriş yapmanız gerekiyor.");
            }

            FriendshipSchemaManager.EnsureSchema();

            DatabaseManager.ExecuteNonQuery(
                @"DELETE FROM Friendships
                  WHERE RequesterUserId = @Requester
                    AND AddresseeUserId = @Addressee
                    AND Status = @Status;",
                new SqlParameter("@Requester", requesterUserId),
                new SqlParameter("@Addressee", currentUserId),
                new SqlParameter("@Status", StatusPending));
        }

        // gönderilen isteği geri çek
        public void CancelOutgoingRequest(int currentUserId, int targetUserId)
        {
            if (currentUserId <= 0)
            {
                throw new InvalidOperationException("Arkadaş özelliği için giriş yapmanız gerekiyor.");
            }

            FriendshipSchemaManager.EnsureSchema();

            DatabaseManager.ExecuteNonQuery(
                @"DELETE FROM Friendships
                  WHERE RequesterUserId = @Requester
                    AND AddresseeUserId = @Addressee
                    AND Status = @Status;",
                new SqlParameter("@Requester", currentUserId),
                new SqlParameter("@Addressee", targetUserId),
                new SqlParameter("@Status", StatusPending));
        }

        // arkadaşlıktan çıkar (çift yönlü sil)
        public void RemoveFriend(int currentUserId, int friendUserId)
        {
            if (currentUserId <= 0)
            {
                throw new InvalidOperationException("Arkadaş özelliği için giriş yapmanız gerekiyor.");
            }

            FriendshipSchemaManager.EnsureSchema();

            DatabaseManager.ExecuteNonQuery(
                @"DELETE FROM Friendships
                  WHERE Status = @Status
                    AND ((RequesterUserId = @A AND AddresseeUserId = @B)
                      OR (RequesterUserId = @B AND AddresseeUserId = @A));",
                new SqlParameter("@Status", StatusAccepted),
                new SqlParameter("@A", currentUserId),
                new SqlParameter("@B", friendUserId));
        }

        // istek listelerini dolduran yardımcı metot
        private IReadOnlyList<FriendRequestItem> GetRequests(int userId, FriendRequestDirection direction, string query)
        {
            if (userId <= 0)
            {
                return new List<FriendRequestItem>();
            }

            FriendshipSchemaManager.EnsureSchema();

            DataTable table = DatabaseManager.ExecuteQuery(
                query,
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Status", StatusPending));

            List<FriendRequestItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                string username = row["Username"]?.ToString() ?? string.Empty;
                string avatarPath = row["ProfileImagePath"]?.ToString() ?? string.Empty;
                DateTime sentAt = Convert.ToDateTime(row["SentAt"], CultureInfo.InvariantCulture);

                items.Add(new FriendRequestItem
                {
                    UserId = Convert.ToInt32(row["UserId"], CultureInfo.InvariantCulture),
                    Username = username,
                    AvatarLetter = BuildAvatarLetter(username),
                    AvatarImagePath = avatarPath,
                    AvatarPreview = GameAssetManager.LoadBitmap(avatarPath),
                    SentAtText = sentAt.ToString("dd.MM.yyyy HH:mm"),
                    Direction = direction
                });
            }

            return items;
        }

        // satır verisini ilişki durumuna çevir        private FriendshipRelationshipStatus ResolveRelationshipFromRow(DataRow row, int viewerUserId, int targetUserId)
        {
            // arama alias (RelStatus/RelRequester) ya da direkt kolon adi
            string statusColumn = row.Table.Columns.Contains("Status") ? "Status" : "RelStatus";
            string requesterColumn = row.Table.Columns.Contains("RequesterUserId") ? "RequesterUserId" : "RelRequester";

            // kolon yoksa iliski yok
            if (!row.Table.Columns.Contains(statusColumn) || !row.Table.Columns.Contains(requesterColumn))
            {
                return FriendshipRelationshipStatus.None;
            }

            // LEFT JOIN bos dondu
            if (row[statusColumn] == DBNull.Value)
            {
                return FriendshipRelationshipStatus.None;
            }

            string status = row[statusColumn]?.ToString() ?? string.Empty;
            int requesterId = Convert.ToInt32(row[requesterColumn], CultureInfo.InvariantCulture);

            // kabul edilmis = arkadas
            if (status == StatusAccepted)
            {
                return FriendshipRelationshipStatus.Friends;
            }

            // bekleyen istek yonunu requester'a gore ayirt et
            if (status == StatusPending)
            {
                return requesterId == viewerUserId
                    ? FriendshipRelationshipStatus.PendingSent
                    : FriendshipRelationshipStatus.PendingReceived;
            }

            return FriendshipRelationshipStatus.None;
        }

        // arama kartı için buton ve yazı tasarımı
        private FriendSearchActionAppearance BuildActionAppearance(FriendshipRelationshipStatus status)
        {
            return status switch
            {
                FriendshipRelationshipStatus.None => new FriendSearchActionAppearance(
                    "Arkadaş değil", "#8F98A5",
                    "Arkadaş Ekle", "#FFFFFF", "#0A0A0C",
                    true),
                FriendshipRelationshipStatus.PendingSent => new FriendSearchActionAppearance(
                    "Arkadaşlık isteği gönderildi", "#F3A761",
                    "İsteği İptal Et", "#E0555F", "#FFFFFF",
                    true),
                FriendshipRelationshipStatus.PendingReceived => new FriendSearchActionAppearance(
                    "Size arkadaşlık isteği gönderdi", "#6FCBFF",
                    "İsteği Kabul Et", "#6FCBFF", "#0A0A0C",
                    true),
                FriendshipRelationshipStatus.Friends => new FriendSearchActionAppearance(
                    "Zaten arkadaşsınız", "#82E4B0",
                    "Arkadaş", "#1A1F28", "#AAB2BD",
                    false),
                FriendshipRelationshipStatus.Self => new FriendSearchActionAppearance(
                    "Bu sizin hesabınız", "#8F98A5",
                    "Siz", "#1A1F28", "#AAB2BD",
                    false),
                _ => new FriendSearchActionAppearance(
                    "Arkadaş değil", "#8F98A5",
                    "Arkadaş Ekle", "#FFFFFF", "#0A0A0C",
                    true)
            };
        }

        // kart görünümü için veri paketi
        private readonly record struct FriendSearchActionAppearance(
            string StatusText,
            string StatusAccent,
            string ActionText,
            string ActionBackground,
            string ActionForeground,
            bool Enabled);

        // ilişki kaydını transaction içinde oku
        private DataRow? GetPairRow(SqlConnection connection, SqlTransaction transaction, int userA, int userB)
        {
            using SqlCommand command = new SqlCommand(
                @"SELECT FriendshipId, RequesterUserId, AddresseeUserId, Status
                  FROM Friendships
                  WHERE (RequesterUserId = @A AND AddresseeUserId = @B)
                     OR (RequesterUserId = @B AND AddresseeUserId = @A)
                  LIMIT 1;",
                connection,
                transaction);
            command.Parameters.AddWithValue("@A", userA);
            command.Parameters.AddWithValue("@B", userB);

            using var adapter = new MySql.Data.MySqlClient.MySqlDataAdapter(command);
            DataTable table = new();
            adapter.Fill(table);

            return table.Rows.Count == 0 ? null : table.Rows[0];
        }

        // kullanıcı var mı kontrolü
        private void EnsureUserExists(int userId)
        {
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM Users
                  WHERE UserId = @UserId;",
                new SqlParameter("@UserId", userId));

            if (result == null || Convert.ToInt32(result, CultureInfo.InvariantCulture) == 0)
            {
                throw new InvalidOperationException("Hedef kullanıcı bulunamadı.");
            }
        }

        // etkilenen satır sayısını dönen yardımcı
        private int ExecuteAffected(string query, params SqlParameter[] parameters)
        {
            using SqlConnection connection = DatabaseManager.GetConnection();
            using SqlCommand command = new SqlCommand(query, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            connection.Open();
            return command.ExecuteNonQuery();
        }

        // transaction üzerinde sorgu çalıştır
        private void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string query, params SqlParameter[] parameters)
        {
            using SqlCommand command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            command.ExecuteNonQuery();
        }

        // baş harften büyük harf avatar karakteri (TR uyumlu)
        private string BuildAvatarLetter(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return "?";
            }

            return username.Trim().Substring(0, 1).ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        }

        // bio metnini kart için kısalt
        private string TrimBio(string? bio)
        {
            if (string.IsNullOrWhiteSpace(bio))
            {
                return string.Empty;
            }

            string trimmed = bio.Trim();
            return trimmed.Length <= 90 ? trimmed : trimmed.Substring(0, 87) + "...";
        }
    }
}
