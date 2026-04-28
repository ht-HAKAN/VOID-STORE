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
            // controller acilisini hafif tut
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
                    AND (@Category = '' OR Category LIKE CONCAT('%', @Category, '%'));",
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
                    DiscountRate,
                    DiscountStartDate,
                    DiscountEndDate,
                    ReleaseDate,
                    CoverImagePath,
                    COALESCE(NULLIF(Publisher, ''), Developer, '') AS Subtitle
                  FROM Games
                  WHERE ApprovalStatus = 'approved'
                    AND IsActive = 1
                    AND (@SearchText = '' OR Title LIKE CONCAT('%', @SearchText, '%'))
                    AND (@Category = '' OR Category LIKE CONCAT('%', @Category, '%'))
                  ORDER BY GameId DESC
                  LIMIT @Limit OFFSET @Offset;",
                new SqlParameter("@SearchText", normalizedSearch),
                new SqlParameter("@Category", normalizedCategory),
                new SqlParameter("@Limit", PageSize),
                new SqlParameter("@Offset", offset));

            List<StoreGameCardItem> items = new();
            DateTime now = DateTime.Now;

            foreach (DataRow row in table.Rows)
            {
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;

                decimal basePrice = row["Price"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Price"], CultureInfo.InvariantCulture);
                int discountRate = row["DiscountRate"] == DBNull.Value ? 0 : Convert.ToInt32(row["DiscountRate"]);
                DateTime? discStart = row["DiscountStartDate"] == DBNull.Value ? null : (DateTime?)row["DiscountStartDate"];
                DateTime? discEnd = row["DiscountEndDate"] == DBNull.Value ? null : (DateTime?)row["DiscountEndDate"];
                DateTime? releaseDate = row["ReleaseDate"] == DBNull.Value ? null : (DateTime?)row["ReleaseDate"];

                bool isOnDiscount = discountRate > 0 && 
                                   (discStart == null || discStart <= now) && 
                                   (discEnd == null || discEnd >= now);
                bool isReleased = releaseDate == null || releaseDate <= now;

                decimal finalPrice = isOnDiscount ? basePrice * (100 - discountRate) / 100 : basePrice;

                items.Add(new StoreGameCardItem
                {
                    GameId = Convert.ToInt32(row["GameId"]),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                    Subtitle = row["Subtitle"]?.ToString() ?? string.Empty,
                    PriceAmount = finalPrice,
                    PriceText = !isReleased ? "Çok Yakında" : (finalPrice == 0 ? "Oynaması Ücretsiz" : FormatPrice(finalPrice)),
                    OriginalPriceText = (isOnDiscount && finalPrice > 0) ? FormatPrice(basePrice) : string.Empty,
                    IsReleased = isReleased,
                    IsOnDiscount = isOnDiscount,
                    DiscountRate = discountRate,
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
                    DiscountRate,
                    DiscountStartDate,
                    DiscountEndDate,
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

            decimal basePrice = row["Price"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Price"], CultureInfo.InvariantCulture);
            int discountRate = row["DiscountRate"] == DBNull.Value ? 0 : Convert.ToInt32(row["DiscountRate"]);
            DateTime? discStart = row["DiscountStartDate"] == DBNull.Value ? null : (DateTime?)row["DiscountStartDate"];
            DateTime? discEnd = row["DiscountEndDate"] == DBNull.Value ? null : (DateTime?)row["DiscountEndDate"];
            DateTime now = DateTime.Now;

            bool isOnDiscount = discountRate > 0 && 
                               (discStart == null || discStart <= now) && 
                               (discEnd == null || discEnd >= now);

            decimal finalPrice = isOnDiscount ? basePrice * (100 - discountRate) / 100 : basePrice;

            DateTime? releaseDate = row["ReleaseDate"] == DBNull.Value ? null : (DateTime?)row["ReleaseDate"];
            bool isReleased = releaseDate == null || releaseDate <= now;

            StoreGameDetail detail = new StoreGameDetail
            {
                GameId = gameId,
                Title = row["Title"]?.ToString() ?? string.Empty,
                Category = GameCategoryCatalog.Normalize(row["Category"]?.ToString()),
                Description = row["Description"]?.ToString() ?? string.Empty,
                PriceAmount = finalPrice,
                PriceText = finalPrice == 0 ? "Oynaması Ücretsiz" : FormatPrice(finalPrice),
                OriginalPrice = basePrice,
                OriginalPriceText = (isOnDiscount && finalPrice > 0) ? FormatPrice(basePrice) : string.Empty,
                IsOnDiscount = isOnDiscount,
                DiscountRate = discountRate,
                DiscountEndDate = discEnd,
                ReleaseDate = releaseDate,
                IsReleased = isReleased,
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

            if (isOnDiscount && discEnd.HasValue)
            {
                TimeSpan diff = discEnd.Value - now;
                if (diff.TotalSeconds <= 0)
                {
                    detail.DiscountTimeRemainingText = string.Empty;
                }
                else if (diff.TotalDays >= 1)
                {
                    int days = (int)diff.TotalDays;
                    int hours = diff.Hours;
                    if (hours > 0)
                        detail.DiscountTimeRemainingText = $"{days} gün {hours} saat kaldı";
                    else
                        detail.DiscountTimeRemainingText = $"{days} gün kaldı";
                }
                else if (diff.TotalHours >= 1)
                {
                    int hours = (int)diff.TotalHours;
                    int minutes = diff.Minutes;
                    if (minutes > 0)
                        detail.DiscountTimeRemainingText = $"{hours} saat {minutes} dk kaldı";
                    else
                        detail.DiscountTimeRemainingText = $"{hours} saat kaldı";
                }
                else
                {
                    int minutes = (int)diff.TotalMinutes;
                    detail.DiscountTimeRemainingText = minutes > 0 ? $"{minutes} dakika kaldı" : "Son dakika!";
                }
            }

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
