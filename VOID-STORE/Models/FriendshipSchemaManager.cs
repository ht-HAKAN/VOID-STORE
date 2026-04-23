using System;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;

namespace VOID_STORE.Models
{
    // arkadaşlık tablosu yöneticisi
    public static class FriendshipSchemaManager
    {
        // gerekli kolonlar
        private static readonly string[] RequiredColumns = new[]
        {
            "FriendshipId",
            "RequesterUserId",
            "AddresseeUserId",
            "Status",
            "CreatedAt",
            "RespondedAt"
        };

        // tabloyu kontrol et ve gerekirse oluştur
        public static void EnsureSchema()
        {
            DropLegacyFriendshipsTableIfIncompatible();
            EnsureFriendshipsTable();
        }

        // uyumsuz eski tabloyu kaldır
        private static void DropLegacyFriendshipsTableIfIncompatible()
        {
            if (!TableExists("Friendships"))
            {
                return;
            }

            foreach (string column in RequiredColumns)
            {
                if (!ColumnExists("Friendships", column))
                {
                    DatabaseManager.ExecuteNonQuery("DROP TABLE IF EXISTS Friendships;");
                    return;
                }
            }
        }

        // tabloyu sıfırdan oluştur
        private static void EnsureFriendshipsTable()
        {
            DatabaseManager.ExecuteNonQuery(
                @"CREATE TABLE IF NOT EXISTS Friendships (
                    FriendshipId INT NOT NULL AUTO_INCREMENT,
                    RequesterUserId INT NOT NULL,
                    AddresseeUserId INT NOT NULL,
                    Status VARCHAR(16) NOT NULL DEFAULT 'pending',
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    RespondedAt DATETIME NULL,
                    PRIMARY KEY (FriendshipId),
                    UNIQUE KEY UX_Friendships_Pair (RequesterUserId, AddresseeUserId),
                    KEY IX_Friendships_Addressee (AddresseeUserId),
                    KEY IX_Friendships_Status (Status),
                    CONSTRAINT FK_Friendships_Requester
                        FOREIGN KEY (RequesterUserId) REFERENCES Users(UserId)
                        ON DELETE CASCADE,
                    CONSTRAINT FK_Friendships_Addressee
                        FOREIGN KEY (AddresseeUserId) REFERENCES Users(UserId)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
        }

        // tablo varlık kontrolü
        private static bool TableExists(string tableName)
        {
            object result = DatabaseManager.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM INFORMATION_SCHEMA.TABLES
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = @tableName",
                new SqlParameter("@tableName", tableName));

            return result != null && Convert.ToInt32(result) > 0;
        }

        // kolon varlık kontrolü
        private static bool ColumnExists(string tableName, string columnName)
        {
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
