using System;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;

namespace VOID_STORE.Models
{
    public static class UserCommerceSchemaManager
    {
        public static void EnsureSchema()
        {
            // kullanici ticaret alanlarini sira ile hazirla
            EnsureBalanceColumn();
            EnsureWalletTransactionsTable();
            EnsureCartItemsTable();
            EnsureUserLibraryTable();
            EnsureUserLibraryColumns();
            EnsureUserDownloadsTable();
        }

        private static void EnsureBalanceColumn()
        {
            // users tablosunda bakiye alanini kontrol et
            if (!ColumnExists("Users", "Balance"))
            {
                // eski kayitlar icin varsayilan sifir ver
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE Users ADD COLUMN Balance DECIMAL(18,2) NOT NULL DEFAULT 0.00 AFTER IsAdmin");
            }
        }

        private static void EnsureWalletTransactionsTable()
        {
            // cuzdan hareketlerini saklayacak tabloyu kur
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS WalletTransactions (
                    TransactionId INT NOT NULL AUTO_INCREMENT,
                    UserId INT NOT NULL,
                    TransactionType VARCHAR(20) NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    BalanceBefore DECIMAL(18,2) NOT NULL,
                    BalanceAfter DECIMAL(18,2) NOT NULL,
                    Description VARCHAR(255) NULL,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (TransactionId),
                    KEY IX_WalletTransactions_UserId (UserId),
                    KEY IX_WalletTransactions_CreatedAt (CreatedAt),
                    CONSTRAINT FK_WalletTransactions_Users
                        FOREIGN KEY (UserId) REFERENCES Users(UserId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        private static void EnsureCartItemsTable()
        {
            // her kullanici oyunlari sepette tek satir tutsun
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS CartItems (
                    CartItemId INT NOT NULL AUTO_INCREMENT,
                    UserId INT NOT NULL,
                    GameId INT NOT NULL,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (CartItemId),
                    UNIQUE KEY UX_CartItems_UserGame (UserId, GameId),
                    KEY IX_CartItems_UserId (UserId),
                    CONSTRAINT FK_CartItems_Users
                        FOREIGN KEY (UserId) REFERENCES Users(UserId)
                        ON DELETE CASCADE,
                    CONSTRAINT FK_CartItems_Games
                        FOREIGN KEY (GameId) REFERENCES Games(GameId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        private static void EnsureUserLibraryTable()
        {
            // satin alinan oyunlari kalici tut
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS UserLibrary (
                    LibraryItemId INT NOT NULL AUTO_INCREMENT,
                    UserId INT NOT NULL,
                    GameId INT NOT NULL,
                    PurchasedPrice DECIMAL(18,2) NOT NULL,
                    PurchasedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (LibraryItemId),
                    UNIQUE KEY UX_UserLibrary_UserGame (UserId, GameId),
                    KEY IX_UserLibrary_UserId (UserId),
                    CONSTRAINT FK_UserLibrary_Users
                        FOREIGN KEY (UserId) REFERENCES Users(UserId)
                        ON DELETE CASCADE,
                    CONSTRAINT FK_UserLibrary_Games
                        FOREIGN KEY (GameId) REFERENCES Games(GameId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        private static void EnsureUserLibraryColumns()
        {
            // kutuphane istatistik kolonlarini kontrol et
            if (!ColumnExists("UserLibrary", "TotalPlaySeconds"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE UserLibrary ADD COLUMN TotalPlaySeconds INT NOT NULL DEFAULT 0 AFTER PurchasedAt");
            }

            if (!ColumnExists("UserLibrary", "LastPlayedAt"))
            {
                DatabaseManager.ExecuteNonQuery(
                    "ALTER TABLE UserLibrary ADD COLUMN LastPlayedAt DATETIME NULL AFTER TotalPlaySeconds");
            }
        }

        private static void EnsureUserDownloadsTable()
        {
            // kurulum ve indirme durumunu kalici tut
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS UserDownloads (
                    DownloadId INT NOT NULL AUTO_INCREMENT,
                    UserId INT NOT NULL,
                    GameId INT NOT NULL,
                    InstallStatus VARCHAR(24) NOT NULL DEFAULT 'not_installed',
                    ProgressPercent DECIMAL(5,2) NOT NULL DEFAULT 0.00,
                    DownloadedBytes BIGINT NOT NULL DEFAULT 0,
                    TotalBytes BIGINT NOT NULL DEFAULT 0,
                    InstallPath VARCHAR(500) NULL,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    CompletedAt DATETIME NULL,
                    PRIMARY KEY (DownloadId),
                    UNIQUE KEY UX_UserDownloads_UserGame (UserId, GameId),
                    KEY IX_UserDownloads_UserStatus (UserId, InstallStatus),
                    CONSTRAINT FK_UserDownloads_Users
                        FOREIGN KEY (UserId) REFERENCES Users(UserId)
                        ON DELETE CASCADE,
                    CONSTRAINT FK_UserDownloads_Games
                        FOREIGN KEY (GameId) REFERENCES Games(GameId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            // kolon bilgisi bilgi semasindan cekilir
            object result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = @TableName
                    AND COLUMN_NAME = @ColumnName",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));

            return result != null && Convert.ToInt32(result) > 0;
        }
    }
}
