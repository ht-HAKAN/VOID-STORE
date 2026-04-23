using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SqlConnection = MySql.Data.MySqlClient.MySqlConnection;
using SqlCommand = MySql.Data.MySqlClient.MySqlCommand;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using SqlTransaction = MySql.Data.MySqlClient.MySqlTransaction;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class ProfileController
    {
        private const int ShowcaseSlotCount = 3;
        private const int BannerWidth = 2560;
        private const int BannerHeight = 900;

        public ProfileController()
        {
            // controller'ı hafif tut
        }

        public ProfileSummary GetProfileSummary(int userId)
        {
            // kendi profil özetini getir
            return GetProfileSummary(userId, userId);
        }

        public ProfileSummary GetProfileSummary(int viewerUserId, int targetUserId)
        {
            // temel profil bilgilerini çek
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    UserId,
                    Username,
                    COALESCE(Bio, '') AS Bio,
                    COALESCE(ProfileImagePath, '') AS ProfileImagePath,
                    COALESCE(BannerImagePath, '') AS BannerImagePath
                  FROM Users
                  WHERE UserId = @UserId
                  LIMIT 1;",
                new SqlParameter("@UserId", targetUserId));

            if (table.Rows.Count == 0)
            {
                throw new InvalidOperationException("Profil bilgisi alınamadı");
            }

            DataRow row = table.Rows[0];
            string avatarPath = row["ProfileImagePath"]?.ToString() ?? string.Empty;
            string bannerPath = row["BannerImagePath"]?.ToString() ?? string.Empty;

            FriendshipController friendshipController = new FriendshipController();

            // profil özetini oluştur
            return new ProfileSummary
            {
                UserId = Convert.ToInt32(row["UserId"], CultureInfo.InvariantCulture),
                Username = row["Username"]?.ToString() ?? "Oyuncu",
                DisplayName = row["Username"]?.ToString() ?? "Oyuncu",
                Bio = row["Bio"]?.ToString() ?? string.Empty,
                ProfileImagePath = avatarPath,
                BannerImagePath = bannerPath,
                AvatarPreview = GameAssetManager.LoadBitmap(avatarPath),
                BannerPreview = GameAssetManager.LoadBitmap(bannerPath),
                OwnedGameCount = GetOwnedGameCount(targetUserId),
                WishlistCount = GetWishlistCount(targetUserId),
                TotalPlaySeconds = GetTotalPlaySeconds(targetUserId),
                FriendsCount = friendshipController.GetFriendsCount(targetUserId),
                ViewerRelationship = friendshipController.GetRelationship(viewerUserId, targetUserId)
            };
        }

        public IReadOnlyList<ProfileOwnedGameOptionItem> GetOwnedGameOptions(int userId)
        {
            // sadece sahip olunan oyunları getir
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    g.GameId,
                    g.Title,
                    g.Category
                  FROM UserLibrary ul
                  INNER JOIN Games g ON g.GameId = ul.GameId
                  WHERE ul.UserId = @UserId
                  ORDER BY g.Title;",
                new SqlParameter("@UserId", userId));

            List<ProfileOwnedGameOptionItem> items = new();
            foreach (DataRow row in table.Rows)
            {
                // seçim kutusu metnini oluştur
                string title = row["Title"]?.ToString() ?? string.Empty;
                string category = GameCategoryCatalog.Normalize(row["Category"]?.ToString());
                items.Add(new ProfileOwnedGameOptionItem
                {
                    GameId = Convert.ToInt32(row["GameId"], CultureInfo.InvariantCulture),
                    Title = title,
                    DisplayText = string.IsNullOrWhiteSpace(category)
                        ? title
                        : $"{title}  |  {category}"
                });
            }

            return items;
        }

        public IReadOnlyList<ProfileShowcaseItem> GetShowcaseItems(int userId, int slotCount = ShowcaseSlotCount)
        {
            // vitrin slotlarını oyunlarla eşle
            Dictionary<int, ProfileShowcaseItem> showcaseMap = DatabaseManager.ExecuteQuery(
                @"SELECT
                    ps.SlotIndex,
                    g.GameId,
                    g.Title,
                    g.Category,
                    g.CoverImagePath
                  FROM ProfileShowcaseGames ps
                  INNER JOIN UserLibrary ul
                    ON ul.UserId = ps.UserId
                   AND ul.GameId = ps.GameId
                  INNER JOIN Games g ON g.GameId = ps.GameId
                  WHERE ps.UserId = @UserId;",
                new SqlParameter("@UserId", userId))
                .Rows
                .Cast<DataRow>()
                .ToDictionary(
                    row => Convert.ToInt32(row["SlotIndex"], CultureInfo.InvariantCulture),
                    row =>
                    {
                        string coverPath = row["CoverImagePath"] == DBNull.Value
                            ? string.Empty
                            : row["CoverImagePath"]?.ToString() ?? string.Empty;

                        return new ProfileShowcaseItem
                        {
                            SlotIndex = Convert.ToInt32(row["SlotIndex"], CultureInfo.InvariantCulture),
                            GameId = Convert.ToInt32(row["GameId"], CultureInfo.InvariantCulture),
                            Title = row["Title"]?.ToString() ?? string.Empty,
                            Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                            CoverImagePath = coverPath,
                            CoverPreview = GameAssetManager.LoadBitmap(coverPath),
                            IsEmpty = false
                        };
                    });

            List<ProfileShowcaseItem> items = new();
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (showcaseMap.TryGetValue(slot, out ProfileShowcaseItem? item))
                {
                    items.Add(item);
                    continue;
                }

                items.Add(new ProfileShowcaseItem
                {
                    SlotIndex = slot,
                    IsEmpty = true,
                    PlaceholderText = $"Vitrin Slotu {slot + 1}"
                });
            }

            return items;
        }

        public IReadOnlyList<ProfileRecentPlayItem> GetRecentPlays(int userId, int take = 6)
        {
            // son oynananları sırala
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    g.GameId,
                    g.Title,
                    g.Category,
                    g.CoverImagePath,
                    ul.TotalPlaySeconds,
                    ul.LastPlayedAt
                  FROM UserLibrary ul
                  INNER JOIN Games g ON g.GameId = ul.GameId
                  WHERE ul.UserId = @UserId
                    AND (ul.TotalPlaySeconds > 0 OR ul.LastPlayedAt IS NOT NULL)
                  ORDER BY ul.LastPlayedAt DESC, ul.TotalPlaySeconds DESC
                  LIMIT @Take;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Take", take));

            List<ProfileRecentPlayItem> items = new();
            foreach (DataRow row in table.Rows)
            {
                // modeli ekrana hazırla
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;
                int totalPlaySeconds = row["TotalPlaySeconds"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(row["TotalPlaySeconds"], CultureInfo.InvariantCulture);
                DateTime? lastPlayedAt = row["LastPlayedAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(row["LastPlayedAt"], CultureInfo.InvariantCulture);

                items.Add(new ProfileRecentPlayItem
                {
                    GameId = Convert.ToInt32(row["GameId"], CultureInfo.InvariantCulture),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                    PlayTimeText = BuildPlayTimeText(totalPlaySeconds),
                    LastPlayedText = lastPlayedAt.HasValue ? lastPlayedAt.Value.ToString("dd.MM.yyyy HH:mm") : "-",
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath)
                });
            }

            return items;
        }

        public ProfileSummary SaveProfile(
            int userId,
            string bio,
            string? avatarSourcePath,
            string? bannerSourcePath)
        {
            // giriş kontrolü
            if (userId <= 0)
            {
                throw new InvalidOperationException("Profil için giriş yapmanız gerekiyor");
            }

            // bio karakter sınırı
            string trimmedBio = (bio ?? string.Empty).Trim();
            if (trimmedBio.Length > 300)
            {
                trimmedBio = trimmedBio[..300];
            }

            // medya klasörünü hazırla
            string profileRoot = GetProfileRoot(userId);

            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // medya seçimlerini işle
                string currentAvatarPath = GetCurrentMediaPath(connection, transaction, userId, "ProfileImagePath");
                string currentBannerPath = GetCurrentMediaPath(connection, transaction, userId, "BannerImagePath");
                string avatarPath = CopyMediaIfNeeded(avatarSourcePath, profileRoot, "avatar", currentAvatarPath);
                string bannerPath = SaveBannerMediaIfNeeded(bannerSourcePath, profileRoot, currentBannerPath);

                // profil alanlarını güncelle (transaction)
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"UPDATE Users
                      SET Bio = @Bio,
                          ProfileImagePath = @ProfileImagePath,
                          BannerImagePath = @BannerImagePath
                      WHERE UserId = @UserId;",
                    new SqlParameter("@Bio", trimmedBio),
                    new SqlParameter("@ProfileImagePath", avatarPath),
                    new SqlParameter("@BannerImagePath", bannerPath),
                    new SqlParameter("@UserId", userId));

                transaction.Commit();
                return GetProfileSummary(userId);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private int GetOwnedGameCount(int userId)
        {
            // kütüphane sayısını al
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM UserLibrary
                  WHERE UserId = @UserId;",
                new SqlParameter("@UserId", userId));

            return result == null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private int GetWishlistCount(int userId)
        {
            // istek listesi sayısını al
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM WishlistItems wi
                  LEFT JOIN UserLibrary ul
                    ON ul.UserId = wi.UserId
                   AND ul.GameId = wi.GameId
                  WHERE wi.UserId = @UserId
                    AND ul.GameId IS NULL;",
                new SqlParameter("@UserId", userId));

            return result == null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private int GetTotalPlaySeconds(int userId)
        {
            // toplam oynama süresini getir
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT COALESCE(SUM(TotalPlaySeconds), 0)
                  FROM UserLibrary
                  WHERE UserId = @UserId;",
                new SqlParameter("@UserId", userId));

            return result == null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private string GetCurrentMediaPath(SqlConnection connection, SqlTransaction transaction, int userId, string columnName)
        {
            // mevcut medya yolunu oku
            using SqlCommand command = new SqlCommand(
                $@"SELECT COALESCE({columnName}, '')
                   FROM Users
                   WHERE UserId = @UserId
                   LIMIT 1;",
                connection,
                transaction);
            command.Parameters.AddWithValue("@UserId", userId);
            object? result = command.ExecuteScalar();
            return result?.ToString() ?? string.Empty;
        }

        private string CopyMediaIfNeeded(string? sourcePath, string profileRoot, string fileNameWithoutExtension, string currentPath)
        {
            // yeni seçim yoksa eskiyi koru
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return currentPath;
            }

            if (!File.Exists(sourcePath))
            {
                return currentPath;
            }

            // dosyayı profile kopyala
            Directory.CreateDirectory(profileRoot);
            string extension = Path.GetExtension(sourcePath);
            string targetPath = Path.Combine(profileRoot, $"{fileNameWithoutExtension}{extension}");
            File.Copy(sourcePath, targetPath, true);
            return targetPath;
        }

        private string SaveBannerMediaIfNeeded(string? sourcePath, string profileRoot, string currentPath)
        {
            // banner yoksa eskiyi koru
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return currentPath;
            }

            if (!File.Exists(sourcePath))
            {
                return currentPath;
            }

            // banner'ı kırp ve kaydet
            Directory.CreateDirectory(profileRoot);
            string targetPath = Path.Combine(profileRoot, "banner.jpg");

            using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return currentPath;
            }

            BitmapSource source = decoder.Frames[0];
            Int32Rect cropRect = CalculateCenterCrop(source.PixelWidth, source.PixelHeight, BannerWidth, BannerHeight);
            CroppedBitmap cropped = new CroppedBitmap(source, cropRect);

            // görseli hedef boyutta çiz
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawImage(cropped, new Rect(0, 0, BannerWidth, BannerHeight));
            }

            RenderTargetBitmap targetBitmap = new RenderTargetBitmap(BannerWidth, BannerHeight, 96, 96, PixelFormats.Pbgra32);
            targetBitmap.Render(visual);
            targetBitmap.Freeze();

            JpegBitmapEncoder encoder = new JpegBitmapEncoder
            {
                QualityLevel = 92
            };
            encoder.Frames.Add(BitmapFrame.Create(targetBitmap));

            using FileStream output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(output);
            return targetPath;
        }

        private Int32Rect CalculateCenterCrop(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            // banner için merkez kırpma alanı
            double sourceRatio = (double)sourceWidth / sourceHeight;
            double targetRatio = (double)targetWidth / targetHeight;

            if (sourceRatio > targetRatio)
            {
                int cropWidth = (int)Math.Round(sourceHeight * targetRatio, MidpointRounding.AwayFromZero);
                int x = Math.Max(0, (sourceWidth - cropWidth) / 2);
                return new Int32Rect(x, 0, Math.Min(cropWidth, sourceWidth), sourceHeight);
            }

            int cropHeight = (int)Math.Round(sourceWidth / targetRatio, MidpointRounding.AwayFromZero);
            int y = Math.Max(0, (sourceHeight - cropHeight) / 2);
            return new Int32Rect(0, y, sourceWidth, Math.Min(cropHeight, sourceHeight));
        }

        private string GetProfileRoot(int userId)
        {
            // profil klasör yolu (LocalAppData)
            string appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string profileRoot = Path.Combine(appDataRoot, "VOID STORE", "Profiles", $"user_{userId}");
            Directory.CreateDirectory(profileRoot);
            return profileRoot;
        }

        private string BuildPlayTimeText(int totalPlaySeconds)
        {
            // saniyeyi metne çevir
            if (totalPlaySeconds <= 0)
            {
                return "-";
            }

            TimeSpan playTime = TimeSpan.FromSeconds(totalPlaySeconds);
            if (playTime.TotalHours >= 1)
            {
                return $"{(int)playTime.TotalHours} sa {playTime.Minutes:D2} dk";
            }

            int minutes = Math.Max(1, (int)Math.Round(playTime.TotalMinutes));
            return $"{minutes} dk";
        }

        private void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string query, params SqlParameter[] parameters)
        {
            // transaction üzerinde komut çalıştır
            using SqlCommand command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            command.ExecuteNonQuery();
        }
    }
}

