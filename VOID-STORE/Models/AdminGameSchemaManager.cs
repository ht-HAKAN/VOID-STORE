using System;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;

namespace VOID_STORE.Models
{
    public static class AdminGameSchemaManager
    {
        public static void EnsureSchema()
        {
        // eksik tablo ve alanlari tamamla
            EnsurePublisherColumn();
            EnsureApprovalStatusColumn();
            EnsureMinimumRequirementsColumn();
            EnsureRecommendedRequirementsColumn();
            EnsureSupportedLanguagesColumn();
            EnsureGamePlatformsTable();
            EnsureGameDraftsTable();
            EnsureDraftMinimumRequirementsColumn();
            EnsureDraftRecommendedRequirementsColumn();
            EnsureDraftSupportedLanguagesColumn();
            EnsureGameDraftPlatformsTable();
            NormalizeExistingGames();
        }

        private static void EnsurePublisherColumn()
        {
        // yayinci alanini eksikse ekle
            if (!ColumnExists("Games", "Publisher"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN Publisher VARCHAR(100) NULL AFTER Developer");
            }
        }

        private static void EnsureApprovalStatusColumn()
        {
        // onay durumu alanini eksikse ekle
            if (!ColumnExists("Games", "ApprovalStatus"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN ApprovalStatus VARCHAR(20) NOT NULL DEFAULT 'approved' AFTER IsActive");
            }
        }

        private static void EnsureMinimumRequirementsColumn()
        {
        // minimum gereksinim alanini eksikse ekle
            if (!ColumnExists("Games", "MinimumRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN MinimumRequirements TEXT NULL AFTER TrailerUrl");
            }
        }

        private static void EnsureRecommendedRequirementsColumn()
        {
        // onerilen gereksinim alanini eksikse ekle
            if (!ColumnExists("Games", "RecommendedRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN RecommendedRequirements TEXT NULL AFTER MinimumRequirements");
            }
        }

        private static void EnsureSupportedLanguagesColumn()
        {
        // dil alanini eksikse ekle
            if (!ColumnExists("Games", "SupportedLanguages"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Games ADD COLUMN SupportedLanguages TEXT NULL AFTER RecommendedRequirements");
            }
        }

        private static void EnsureGamePlatformsTable()
        {
        // platform tablosunu eksikse olustur
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
        // yeni surum tablosunu eksikse olustur
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS GameDrafts (
                    GameDraftId INT NOT NULL AUTO_INCREMENT,
                    GameId INT NOT NULL,
                    Title VARCHAR(100) NOT NULL,
                    Description TEXT NOT NULL,
                    Price DECIMAL(18,2) NOT NULL,
                    CoverImagePath VARCHAR(255) NULL,
                    Developer VARCHAR(100) NULL,
                    Publisher VARCHAR(100) NULL,
                    ReleaseDate DATETIME NULL,
                    TrailerUrl VARCHAR(500) NULL,
                    MinimumRequirements TEXT NULL,
                    RecommendedRequirements TEXT NULL,
                    SupportedLanguages TEXT NULL,
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

        private static void EnsureDraftMinimumRequirementsColumn()
        {
        // yeni surum minimum gereksinim alanini eksikse ekle
            if (!ColumnExists("GameDrafts", "MinimumRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN MinimumRequirements TEXT NULL AFTER TrailerUrl");
            }
        }

        private static void EnsureDraftRecommendedRequirementsColumn()
        {
        // yeni surum onerilen gereksinim alanini eksikse ekle
            if (!ColumnExists("GameDrafts", "RecommendedRequirements"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN RecommendedRequirements TEXT NULL AFTER MinimumRequirements");
            }
        }

        private static void EnsureDraftSupportedLanguagesColumn()
        {
        // yeni surum dil alanini eksikse ekle
            if (!ColumnExists("GameDrafts", "SupportedLanguages"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE GameDrafts ADD COLUMN SupportedLanguages TEXT NULL AFTER RecommendedRequirements");
            }
        }

        private static void EnsureGameDraftPlatformsTable()
        {
        // yeni surum platform tablosunu eksikse olustur
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
        // eski kayitlari onayli duruma getir
            DatabaseManager.ExecuteNonQuery(
                "UPDATE Games SET ApprovalStatus = 'approved' WHERE ApprovalStatus IS NULL OR TRIM(ApprovalStatus) = ''");
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
        // alanin varligini denetle
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
