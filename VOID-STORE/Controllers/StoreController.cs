using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class StoreController
    {
        public const int PageSize = 24;
        public const string AllCategory = "Tümü";

        public StoreController()
        {
            // store açılmadan önce gerekli alanları doğrula
            AdminGameSchemaManager.EnsureSchema();
        }

        public IReadOnlyList<string> GetCategories()
        {
            return GameCategoryCatalog.All;
        }

        public StoreGamePageResult GetGames(string searchText, string category, int requestedPage)
        {
            string normalizedSearch = searchText?.Trim() ?? string.Empty;
            string normalizedCategory = NormalizeCategoryFilter(category);

            SqlParameter[] countParameters =
            {
                new("@SearchText", normalizedSearch),
                new("@Category", normalizedCategory)
            };

            object? countResult = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM Games
                  WHERE ApprovalStatus = 'approved'
                    AND IsActive = 1
                    AND (@SearchText = '' OR Title LIKE CONCAT('%', @SearchText, '%'))
                    AND (@Category = '' OR Category = @Category);",
                countParameters);

            int totalCount = countResult == null || countResult == DBNull.Value
                ? 0
                : Convert.ToInt32(countResult);

            int totalPages = totalCount == 0
                ? 1
                : (int)Math.Ceiling(totalCount / (double)PageSize);

            int currentPage = Math.Max(1, Math.Min(requestedPage, totalPages));
            int offset = (currentPage - 1) * PageSize;

            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameId,
                    Title,
                    Category,
                    Price,
                    CoverImagePath,
                    COALESCE(NULLIF(Publisher, ''), Developer, '') AS Subtitle
                  FROM Games
                  WHERE ApprovalStatus = 'approved'
                    AND IsActive = 1
                    AND (@SearchText = '' OR Title LIKE CONCAT('%', @SearchText, '%'))
                    AND (@Category = '' OR Category = @Category)
                  ORDER BY GameId DESC
                  LIMIT @Limit OFFSET @Offset;",
                new SqlParameter("@SearchText", normalizedSearch),
                new SqlParameter("@Category", normalizedCategory),
                new SqlParameter("@Limit", PageSize),
                new SqlParameter("@Offset", offset));

            List<StoreGameCardItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;

                items.Add(new StoreGameCardItem
                {
                    GameId = Convert.ToInt32(row["GameId"]),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                    Subtitle = row["Subtitle"]?.ToString() ?? string.Empty,
                    PriceText = FormatPrice(row["Price"]),
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath)
                });
            }

            return new StoreGamePageResult
            {
                Items = items,
                TotalCount = totalCount,
                CurrentPage = currentPage,
                TotalPages = totalPages
            };
        }

        public StoreGameDetail GetGameDetail(int gameId)
        {
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameId,
                    Title,
                    Category,
                    Description,
                    Price,
                    CoverImagePath,
                    Developer,
                    Publisher,
                    ReleaseDate,
                    TrailerVideoPath,
                    MinimumRequirements,
                    RecommendedRequirements,
                    SupportedLanguages,
                    GameFeatures
                  FROM Games
                  WHERE GameId = @GameId
                    AND ApprovalStatus = 'approved'
                    AND IsActive = 1
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            if (table.Rows.Count == 0)
            {
                throw new InvalidOperationException("Seçilen oyun mağazada bulunamadı.");
            }

            DataRow row = table.Rows[0];
            string coverPath = row["CoverImagePath"] == DBNull.Value
                ? string.Empty
                : row["CoverImagePath"]?.ToString() ?? string.Empty;

            string storedTrailerPath = row["TrailerVideoPath"] == DBNull.Value ? string.Empty : row["TrailerVideoPath"]?.ToString() ?? string.Empty;
            string resolvedTrailerPath = ResolveTrailerVideoPath(gameId, storedTrailerPath);

            StoreGameDetail detail = new StoreGameDetail
            {
                GameId = gameId,
                Title = row["Title"]?.ToString() ?? string.Empty,
                Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                Description = row["Description"]?.ToString() ?? string.Empty,
                PriceText = FormatPrice(row["Price"]),
                CoverImagePath = coverPath,
                CoverPreview = GameAssetManager.LoadBitmap(coverPath),
                Developer = row["Developer"]?.ToString() ?? string.Empty,
                Publisher = row["Publisher"]?.ToString() ?? string.Empty,
                ReleaseDateText = FormatReleaseDate(row["ReleaseDate"]),
                TrailerVideoPath = resolvedTrailerPath,
                MinimumRequirements = row["MinimumRequirements"] == DBNull.Value ? string.Empty : row["MinimumRequirements"]?.ToString() ?? string.Empty,
                RecommendedRequirements = row["RecommendedRequirements"] == DBNull.Value ? string.Empty : row["RecommendedRequirements"]?.ToString() ?? string.Empty,
                SupportedLanguages = row["SupportedLanguages"] == DBNull.Value ? string.Empty : row["SupportedLanguages"]?.ToString() ?? string.Empty,
                Platforms = GetPlatforms(gameId),
                Features = ParseFeatures(row["GameFeatures"] == DBNull.Value ? string.Empty : row["GameFeatures"]?.ToString())
            };

            detail.MediaItems = BuildMediaItems(gameId, coverPath, detail.TrailerVideoPath);
            return detail;
        }

        public string GetDisplayNameForLogin(string usernameOrEmail)
        {
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT Username
                  FROM Users
                  WHERE Username = @User OR Email = @User
                  LIMIT 1;",
                new SqlParameter("@User", usernameOrEmail));

            return result?.ToString() ?? usernameOrEmail.Trim();
        }

        private List<string> GetPlatforms(int gameId)
        {
            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT PlatformName
                  FROM GamePlatforms
                  WHERE GameId = @GameId
                  ORDER BY PlatformName ASC;",
                new SqlParameter("@GameId", gameId));

            List<string> platforms = new();

            foreach (DataRow row in table.Rows)
            {
                string value = row["PlatformName"]?.ToString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    platforms.Add(value.Trim());
                }
            }

            return platforms;
        }

        private List<StoreMediaItem> BuildMediaItems(int gameId, string coverPath, string trailerVideoPath)
        {
            List<StoreMediaItem> items = new();
            IReadOnlyList<string> galleryPaths = GameAssetManager.GetGalleryImagePaths(gameId, false);
            string trailerPreviewPath = !string.IsNullOrWhiteSpace(coverPath)
                ? coverPath
                : galleryPaths.FirstOrDefault() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(trailerVideoPath))
            {
                items.Add(new StoreMediaItem
                {
                    Name = "Fragman",
                    MediaUrl = trailerVideoPath,
                    ImagePath = trailerPreviewPath,
                    Preview = GameAssetManager.LoadBitmap(trailerPreviewPath),
                    IsTrailer = true,
                    IsSelected = true
                });
            }

            for (int i = 0; i < galleryPaths.Count; i++)
            {
                items.Add(new StoreMediaItem
                {
                    Name = $"Galeri {i + 1}",
                    MediaUrl = galleryPaths[i],
                    ImagePath = galleryPaths[i],
                    Preview = GameAssetManager.LoadBitmap(galleryPaths[i]),
                    IsSelected = items.Count == 0
                });
            }

            if (items.Count == 0 && !string.IsNullOrWhiteSpace(coverPath))
            {
                items.Add(new StoreMediaItem
                {
                    Name = "Kapak Görseli",
                    MediaUrl = coverPath,
                    ImagePath = coverPath,
                    Preview = GameAssetManager.LoadBitmap(coverPath),
                    IsSelected = true
                });
            }

            return items;
        }

        private string ResolveTrailerVideoPath(int gameId, string storedTrailerPath)
        {
            // kayitli yol bozuksa canli klasorden dogru videoyu bul
            if (!string.IsNullOrWhiteSpace(storedTrailerPath))
            {
                string absoluteStoredPath = GameAssetManager.GetAbsoluteAssetPath(storedTrailerPath);

                if (System.IO.File.Exists(absoluteStoredPath))
                {
                    return storedTrailerPath;
                }

                string promotedTrailerPath = GameAssetManager.GetPromotedTrailerPath(gameId, storedTrailerPath);
                string absolutePromotedPath = GameAssetManager.GetAbsoluteAssetPath(promotedTrailerPath);

                if (!string.IsNullOrWhiteSpace(promotedTrailerPath) && System.IO.File.Exists(absolutePromotedPath))
                {
                    return promotedTrailerPath;
                }
            }

            string detectedTrailerPath = GameAssetManager.GetTrailerVideoPath(gameId, false);
            return string.IsNullOrWhiteSpace(detectedTrailerPath)
                ? string.Empty
                : $"voidstoregames/{gameId}/{System.IO.Path.GetFileName(detectedTrailerPath)}";
        }

        private List<string> ParseFeatures(string? storedValue)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
            {
                return new List<string>();
            }

            return GameFeatureCatalog.NormalizeMany(
                storedValue.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private string NormalizeCategoryFilter(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || string.Equals(category, AllCategory, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return GameCategoryCatalog.Normalize(category);
        }

        private string FormatPrice(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "Ücretsiz";
            }

            decimal price = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return $"₺{price.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))}";
        }

        private string FormatReleaseDate(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "Belirtilmedi";
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToString("dd.MM.yyyy");
            }

            if (DateTime.TryParse(value.ToString(), out DateTime parsedDate))
            {
                return parsedDate.ToString("dd.MM.yyyy");
            }

            return "Belirtilmedi";
        }
    }
}
