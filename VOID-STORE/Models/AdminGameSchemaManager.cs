using System;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;

namespace VOID_STORE.Models
{
    public static class AdminGameSchemaManager
    {
        public static void EnsureSchema()
        {
        // veritabani alanlarini tamamla
            EnsureCategoryColumn();
            EnsurePublisherColumn();
            EnsureApprovalStatusColumn();
            EnsureTrailerVideoPathColumn();
            EnsureMinimumRequirementsColumn();
            EnsureRecommendedRequirementsColumn();
            EnsureSupportedLanguagesColumn();
            EnsureGameFeaturesColumn();
            EnsureGamePlatformsTable();
            EnsureGameDraftsTable();
            EnsureDraftCategoryColumn();
            EnsureDraftTrailerVideoPathColumn();
            EnsureDraftMinimumRequirementsColumn();
            EnsureDraftRecommendedRequirementsColumn();
            EnsureDraftSupportedLanguagesColumn();
            EnsureDraftGameFeaturesColumn();
            EnsureGameDraftPlatformsTable();
            NormalizeExistingGames();
        }

        private static void EnsureCategoryColumn()
        {
        // kategori alanini kontrol et
            if (!ColumnExists("Games", "Category"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN Category VARCHAR(50) NOT NULL DEFAULT 'Aksiyon' AFTER Title");
            }
        }

        private static void EnsurePublisherColumn()
        {
        // yayinci alanini kontrol et
            if (!ColumnExists("Games", "Publisher"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN Publisher VARCHAR(100) NULL AFTER Developer");
            }
        }

        private static void EnsureApprovalStatusColumn()
        {
        // onay durumu alanini kontrol et
            if (!ColumnExists("Games", "ApprovalStatus"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN ApprovalStatus VARCHAR(20) NOT NULL DEFAULT 'approved' AFTER IsActive");
            }
        }

        private static void EnsureMinimumRequirementsColumn()
        {
        // minimum gereksinim alanini kontrol et
            if (!ColumnExists("Games", "MinimumRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN MinimumRequirements TEXT NULL AFTER TrailerVideoPath");
            }
        }

        private static void EnsureTrailerVideoPathColumn()
        {
        // fragman video yolunu kontrol et
            if (!ColumnExists("Games", "TrailerVideoPath"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN TrailerVideoPath VARCHAR(255) NULL AFTER TrailerUrl");
            }
        }

        private static void EnsureRecommendedRequirementsColumn()
        {
        // onerilen gereksinim alanini kontrol et
            if (!ColumnExists("Games", "RecommendedRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN RecommendedRequirements TEXT NULL AFTER MinimumRequirements");
            }
        }

        private static void EnsureSupportedLanguagesColumn()
        {
        // diller alanini kontrol et
            if (!ColumnExists("Games", "SupportedLanguages"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN SupportedLanguages TEXT NULL AFTER RecommendedRequirements");
            }
        }

        private static void EnsureGameFeaturesColumn()
        {
        // ozellik alanini kontrol et
            if (!ColumnExists("Games", "GameFeatures"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN GameFeatures TEXT NULL AFTER SupportedLanguages");
            }
        }

        private static void EnsureGamePlatformsTable()
        {
        // platform tablosunu kontrol et
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS GamePlatforms (
                    GamePlatformId INT NOT NULL AUTO_INCREMENT,
                    GameId INT NOT NULL,
                    PlatformName VARCHAR(50) NOT NULL,
                    PRIMARY KEY (GamePlatformId),
                    UNIQUE KEY UX_GamePlatforms (GameId, PlatformName),
                    CONSTRAINT FK_GamePlatforms_Games
                        FOREIGN KEY (GameId) REFERENCES Games(GameId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        private static void EnsureGameDraftsTable()
        {
        // guncelleme tablosunu kontrol et
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS GameDrafts (
                    GameDraftId INT NOT NULL AUTO_INCREMENT,
                    GameId INT NOT NULL,
                    Title VARCHAR(100) NOT NULL,
                    Category VARCHAR(50) NOT NULL DEFAULT 'Aksiyon',
                    Description TEXT NOT NULL,
                    Price DECIMAL(18,2) NOT NULL,
                    CoverImagePath VARCHAR(255) NULL,
                    Developer VARCHAR(100) NULL,
                    Publisher VARCHAR(100) NULL,
                    ReleaseDate DATETIME NULL,
                    TrailerUrl VARCHAR(500) NULL,
                    TrailerVideoPath VARCHAR(255) NULL,
                    MinimumRequirements TEXT NULL,
                    RecommendedRequirements TEXT NULL,
                    SupportedLanguages TEXT NULL,
                    GameFeatures TEXT NULL,
                    DraftStatus VARCHAR(20) NOT NULL DEFAULT 'pending',
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    PRIMARY KEY (GameDraftId),
                    UNIQUE KEY UX_GameDrafts_GameId (GameId),
                    CONSTRAINT FK_GameDrafts_Games
                        FOREIGN KEY (GameId) REFERENCES Games(GameId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        private static void EnsureDraftCategoryColumn()
        {
        // taslak kategori alanini kontrol et
            if (!ColumnExists("GameDrafts", "Category"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN Category VARCHAR(50) NOT NULL DEFAULT 'Aksiyon' AFTER Title");
            }
        }

        private static void EnsureDraftMinimumRequirementsColumn()
        {
        // taslak minimum gereksinim alanini kontrol et
            if (!ColumnExists("GameDrafts", "MinimumRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN MinimumRequirements TEXT NULL AFTER TrailerVideoPath");
            }
        }

        private static void EnsureDraftTrailerVideoPathColumn()
        {
        // taslak fragman yolunu kontrol et
            if (!ColumnExists("GameDrafts", "TrailerVideoPath"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN TrailerVideoPath VARCHAR(255) NULL AFTER TrailerUrl");
            }
        }

        private static void EnsureDraftRecommendedRequirementsColumn()
        {
        // taslak onerilen gereksinim alanini kontrol et
            if (!ColumnExists("GameDrafts", "RecommendedRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN RecommendedRequirements TEXT NULL AFTER MinimumRequirements");
            }
        }

        private static void EnsureDraftSupportedLanguagesColumn()
        {
        // taslak diller alanini kontrol et
            if (!ColumnExists("GameDrafts", "SupportedLanguages"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN SupportedLanguages TEXT NULL AFTER RecommendedRequirements");
            }
        }

        private static void EnsureDraftGameFeaturesColumn()
        {
        // taslak ozellik alanini kontrol et
            if (!ColumnExists("GameDrafts", "GameFeatures"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN GameFeatures TEXT NULL AFTER SupportedLanguages");
            }
        }

        private static void EnsureGameDraftPlatformsTable()
        {
        // taslak platform tablosunu kontrol et
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS GameDraftPlatforms (
                    GameDraftPlatformId INT NOT NULL AUTO_INCREMENT,
                    GameDraftId INT NOT NULL,
                    PlatformName VARCHAR(50) NOT NULL,
                    PRIMARY KEY (GameDraftPlatformId),
                    UNIQUE KEY UX_GameDraftPlatforms (GameDraftId, PlatformName),
                    CONSTRAINT FK_GameDraftPlatforms_GameDrafts
                        FOREIGN KEY (GameDraftId) REFERENCES GameDrafts(GameDraftId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        private static void NormalizeExistingGames()
        {
        // eski kayitlari onayli hale getir
            DatabaseManager.ExecuteNonQuery(
                "UPDATE Games SET ApprovalStatus = 'approved' WHERE ApprovalStatus IS NULL OR TRIM(ApprovalStatus) = ''");

            DatabaseManager.ExecuteNonQuery(
                "UPDATE Games SET Category = 'Aksiyon' WHERE Category IS NULL OR TRIM(Category) = ''");

            DatabaseManager.ExecuteNonQuery(
                "UPDATE GameDrafts SET Category = 'Aksiyon' WHERE Category IS NULL OR TRIM(Category) = ''");
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
        // alan var mi bak
            object result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = @tableName
                    AND COLUMN_NAME = @columnName",
                new SqlParameter("@tableName", tableName),
                new SqlParameter("@columnName", columnName));

            return result != null && Convert.ToInt32(result) > 0;
        }
    }
}
