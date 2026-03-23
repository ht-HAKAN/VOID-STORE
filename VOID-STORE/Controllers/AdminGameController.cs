using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class AdminGameController
    {
        public const int MinGalleryImageCount = 3;
        public const int MaxGalleryImageCount = 8;

        private static readonly Regex PriceValidationRegex = new(@"^(?:[1-9]\d{0,3})(?:[.,]\d{1,2})?$");

        public void EnsureSchema()
        {
        // gerekli tablo ve alanlari hazirla
            AdminGameSchemaManager.EnsureSchema();
        }

        public int CreateGame(GameCreateRequest request)
        {
        // yeni oyun kaydini onaya hazirla
            string validationMessage = ValidateGameInput(
                request.Title,
                request.PriceText,
                request.Description,
                request.Developer,
                request.Publisher,
                request.Platforms,
                request.ReleaseDateText,
                request.TrailerUrl,
                request.MinimumRequirements,
                request.RecommendedRequirements,
                request.SupportedLanguages,
                request.CoverImageSourcePath,
                request.GalleryImageSourcePaths);

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            EnsureSchema();

            decimal price = ParsePrice(request.PriceText);
            DateTime releaseDate = DateTime.ParseExact(
                request.ReleaseDateText.Trim(),
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture);

            int createdGameId = 0;

            using MySqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();

            using MySqlTransaction transaction = connection.BeginTransaction();

            try
            {
                using MySqlCommand insertGameCommand = new MySqlCommand(
                    @"INSERT INTO Games
                        (Title, Description, Price, CoverImagePath, Developer, Publisher, ReleaseDate, IsActive, ApprovalStatus, TrailerUrl, MinimumRequirements, RecommendedRequirements, SupportedLanguages)
                      VALUES
                        (@Title, @Description, @Price, NULL, @Developer, @Publisher, @ReleaseDate, 1, 'pending', @TrailerUrl, @MinimumRequirements, @RecommendedRequirements, @SupportedLanguages);",
                    connection,
                    transaction);

                insertGameCommand.Parameters.AddWithValue("@Title", request.Title.Trim());
                insertGameCommand.Parameters.AddWithValue("@Description", request.Description.Trim());
                insertGameCommand.Parameters.AddWithValue("@Price", price);
                insertGameCommand.Parameters.AddWithValue("@Developer", request.Developer.Trim());
                insertGameCommand.Parameters.AddWithValue("@Publisher", request.Publisher.Trim());
                insertGameCommand.Parameters.AddWithValue("@ReleaseDate", releaseDate);
                insertGameCommand.Parameters.AddWithValue(
                    "@TrailerUrl",
                    string.IsNullOrWhiteSpace(request.TrailerUrl) ? DBNull.Value : request.TrailerUrl.Trim());
                insertGameCommand.Parameters.AddWithValue("@MinimumRequirements", request.MinimumRequirements.Trim());
                insertGameCommand.Parameters.AddWithValue("@RecommendedRequirements", request.RecommendedRequirements.Trim());
                insertGameCommand.Parameters.AddWithValue("@SupportedLanguages", request.SupportedLanguages.Trim());

                insertGameCommand.ExecuteNonQuery();
                createdGameId = Convert.ToInt32(insertGameCommand.LastInsertedId);

                string coverRelativePath = GameAssetManager.SaveGameAssets(
                    createdGameId,
                    request.CoverImageSourcePath,
                    request.GalleryImageSourcePaths);

                InsertPlatforms(connection, transaction, createdGameId, request.Platforms);

                using MySqlCommand updateCoverCommand = new MySqlCommand(
                    "UPDATE Games SET CoverImagePath = @CoverImagePath WHERE GameId = @GameId;",
                    connection,
                    transaction);

                updateCoverCommand.Parameters.AddWithValue("@CoverImagePath", coverRelativePath);
                updateCoverCommand.Parameters.AddWithValue("@GameId", createdGameId);
                updateCoverCommand.ExecuteNonQuery();

                transaction.Commit();
                return createdGameId;
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                if (createdGameId > 0)
                {
                    TryDeleteLiveAssets(createdGameId);
                }

                throw;
            }
        }

        public IReadOnlyList<AdminGameListItem> GetApprovedGames(string searchText)
        {
        // yayinda olan oyunlari listele
            EnsureSchema();

            string normalizedSearch = searchText?.Trim() ?? string.Empty;
            DataTable gameTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    g.GameId,
                    g.Title,
                    COALESCE(g.Publisher, '') AS Publisher,
                    COALESCE(
                        (
                            SELECT gd.CoverImagePath
                            FROM GameDrafts gd
                            WHERE gd.GameId = g.GameId
                              AND gd.DraftStatus = 'pending'
                            LIMIT 1
                        ),
                        g.CoverImagePath,
                        ''
                    ) AS CoverImagePath,
                    EXISTS(
                        SELECT 1
                        FROM GameDrafts gd
                        WHERE gd.GameId = g.GameId
                          AND gd.DraftStatus = 'pending'
                    ) AS HasPendingDraft
                  FROM Games g
                  WHERE g.ApprovalStatus = 'approved'
                    AND (
                        @SearchText = ''
                        OR g.Title LIKE CONCAT('%', @SearchText, '%')
                        OR COALESCE(g.Publisher, '') LIKE CONCAT('%', @SearchText, '%')
                    )
                  ORDER BY g.Title ASC;",
                new SqlParameter("@SearchText", normalizedSearch));

            List<AdminGameListItem> items = new();

            foreach (DataRow row in gameTable.Rows)
            {
                string coverPath = row["CoverImagePath"]?.ToString() ?? string.Empty;

                items.Add(new AdminGameListItem
                {
                    GameId = Convert.ToInt32(row["GameId"]),
                    GameDraftId = 0,
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Publisher = row["Publisher"]?.ToString() ?? string.Empty,
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath),
                    HasPendingDraft = Convert.ToInt32(row["HasPendingDraft"]) > 0,
                    IsPendingNewGame = false,
                    IsListed = true,
                    BadgeText = Convert.ToInt32(row["HasPendingDraft"]) > 0 ? "Güncelleme Var" : string.Empty,
                    StatusText = Convert.ToInt32(row["HasPendingDraft"]) > 0
                        ? "Güncelleme bekliyor"
                        : "Yayında"
                });
            }

            return items;
        }

        public IReadOnlyList<AdminGameListItem> GetPendingReviewGames(string searchText)
        {
        // onay bekleyen kayitlari listele
            EnsureSchema();

            string normalizedSearch = searchText?.Trim() ?? string.Empty;
            DataTable itemTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    pending.GameId,
                    pending.GameDraftId,
                    pending.Title,
                    pending.Publisher,
                    pending.CoverImagePath,
                    pending.RequestType
                  FROM (
                    SELECT
                        g.GameId,
                        0 AS GameDraftId,
                        g.Title,
                        COALESCE(g.Publisher, '') AS Publisher,
                        COALESCE(g.CoverImagePath, '') AS CoverImagePath,
                        'new' AS RequestType
                    FROM Games g
                    WHERE g.ApprovalStatus = 'pending'

                    UNION ALL

                    SELECT
                        gd.GameId,
                        gd.GameDraftId,
                        gd.Title,
                        COALESCE(gd.Publisher, '') AS Publisher,
                        COALESCE(gd.CoverImagePath, '') AS CoverImagePath,
                        'draft' AS RequestType
                    FROM GameDrafts gd
                    INNER JOIN Games g ON g.GameId = gd.GameId
                    WHERE gd.DraftStatus = 'pending'
                  ) pending
                  WHERE (
                        @SearchText = ''
                        OR pending.Title LIKE CONCAT('%', @SearchText, '%')
                        OR COALESCE(pending.Publisher, '') LIKE CONCAT('%', @SearchText, '%')
                  )
                  ORDER BY pending.Title ASC;",
                new SqlParameter("@SearchText", normalizedSearch));

            List<AdminGameListItem> items = new();

            foreach (DataRow row in itemTable.Rows)
            {
                bool isPendingNewGame = string.Equals(
                    row["RequestType"]?.ToString(),
                    "new",
                    StringComparison.OrdinalIgnoreCase);

                string coverPath = row["CoverImagePath"]?.ToString() ?? string.Empty;

                items.Add(new AdminGameListItem
                {
                    GameId = Convert.ToInt32(row["GameId"]),
                    GameDraftId = Convert.ToInt32(row["GameDraftId"]),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Publisher = row["Publisher"]?.ToString() ?? string.Empty,
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath),
                    HasPendingDraft = !isPendingNewGame,
                    IsPendingNewGame = isPendingNewGame,
                    IsListed = false,
                    BadgeText = isPendingNewGame ? "Yeni Oyun" : "Güncelleme",
                    StatusText = isPendingNewGame ? "Onay bekliyor" : "Değişiklik bekliyor"
                });
            }

            return items;
        }

        public IReadOnlyList<AdminGameListItem> GetListedGames(string searchText)
        {
        // listelenen oyunlari getir
            return GetApprovedVisibilityGames(searchText, true);
        }

        public IReadOnlyList<AdminGameListItem> GetUnlistedGames(string searchText)
        {
        // liste disindaki oyunlari getir
            return GetApprovedVisibilityGames(searchText, false);
        }

        public GameManageDetail GetManagementDetail(AdminGameListItem item)
        {
        // secilen kaydin detaylarini ekrana hazirla
            EnsureSchema();

            if (item.IsPendingNewGame)
            {
                GameEditState? pendingNewState = TryGetPendingNewGameState(item.GameId);

                if (pendingNewState == null)
                {
                    throw new InvalidOperationException("Seçilen kayıt bulunamadı.");
                }

                return new GameManageDetail
                {
                    GameId = item.GameId,
                    IsPendingNewGame = true,
                    CurrentState = pendingNewState
                };
            }

            if (item.HasPendingDraft)
            {
                GameEditState? draftState = TryGetPendingDraftState(item.GameId);
                GameEditState? liveState = TryGetApprovedGameState(item.GameId);

                if (draftState == null || liveState == null)
                {
                    throw new InvalidOperationException("Seçilen kayıt bulunamadı.");
                }

                return new GameManageDetail
                {
                    GameId = item.GameId,
                    IsPendingDraft = true,
                    CurrentState = draftState,
                    LiveState = liveState
                };
            }

            GameEditState? approvedState = TryGetApprovedGameState(item.GameId);

            if (approvedState == null)
            {
                throw new InvalidOperationException("Seçilen oyun bulunamadı.");
            }

            return new GameManageDetail
            {
                GameId = item.GameId,
                CurrentState = approvedState
            };
        }

        public GameEditState GetGameEditState(int gameId)
        {
        // guncelleme ekraninin verilerini hazirla
            EnsureSchema();

            DataTable draftTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameDraftId,
                    GameId,
                    Title,
                    Description,
                    Price,
                    CoverImagePath,
                    Developer,
                    Publisher,
                    ReleaseDate,
                    TrailerUrl,
                    MinimumRequirements,
                    RecommendedRequirements,
                    SupportedLanguages,
                    DraftStatus
                  FROM GameDrafts
                  WHERE GameId = @GameId
                    AND DraftStatus = 'pending'
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            if (draftTable.Rows.Count > 0)
            {
                DataRow draftRow = draftTable.Rows[0];
                int draftId = Convert.ToInt32(draftRow["GameDraftId"]);

                return new GameEditState
                {
                    GameId = gameId,
                    Title = draftRow["Title"]?.ToString() ?? string.Empty,
                    Description = draftRow["Description"]?.ToString() ?? string.Empty,
                    PriceText = FormatPriceText(draftRow["Price"]),
                    Developer = draftRow["Developer"]?.ToString() ?? string.Empty,
                    Publisher = draftRow["Publisher"]?.ToString() ?? string.Empty,
                    ReleaseDateText = FormatReleaseDate(draftRow["ReleaseDate"]),
                    TrailerUrl = draftRow["TrailerUrl"] == DBNull.Value ? string.Empty : draftRow["TrailerUrl"]?.ToString() ?? string.Empty,
                    MinimumRequirements = draftRow["MinimumRequirements"] == DBNull.Value ? string.Empty : draftRow["MinimumRequirements"]?.ToString() ?? string.Empty,
                    RecommendedRequirements = draftRow["RecommendedRequirements"] == DBNull.Value ? string.Empty : draftRow["RecommendedRequirements"]?.ToString() ?? string.Empty,
                    SupportedLanguages = draftRow["SupportedLanguages"] == DBNull.Value ? string.Empty : draftRow["SupportedLanguages"]?.ToString() ?? string.Empty,
                    CoverImagePath = draftRow["CoverImagePath"] == DBNull.Value ? string.Empty : draftRow["CoverImagePath"]?.ToString() ?? string.Empty,
                    CoverImageSourcePath = GameAssetManager.GetAbsoluteAssetPath(draftRow["CoverImagePath"] == DBNull.Value ? string.Empty : draftRow["CoverImagePath"]?.ToString() ?? string.Empty),
                    Platforms = GetDraftPlatforms(draftId),
                    GalleryImageSourcePaths = GameAssetManager.GetGalleryImagePaths(gameId, true).ToList(),
                    HasPendingDraft = true
                };
            }

            DataTable gameTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameId,
                    Title,
                    Description,
                    Price,
                    CoverImagePath,
                    Developer,
                    Publisher,
                    ReleaseDate,
                    TrailerUrl,
                    MinimumRequirements,
                    RecommendedRequirements,
                    SupportedLanguages
                  FROM Games
                  WHERE GameId = @GameId
                    AND ApprovalStatus = 'approved'
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            if (gameTable.Rows.Count == 0)
            {
                throw new InvalidOperationException("Seçilen oyun bulunamadı.");
            }

            DataRow gameRow = gameTable.Rows[0];

            return new GameEditState
            {
                GameId = gameId,
                Title = gameRow["Title"]?.ToString() ?? string.Empty,
                Description = gameRow["Description"]?.ToString() ?? string.Empty,
                PriceText = FormatPriceText(gameRow["Price"]),
                Developer = gameRow["Developer"]?.ToString() ?? string.Empty,
                Publisher = gameRow["Publisher"]?.ToString() ?? string.Empty,
                ReleaseDateText = FormatReleaseDate(gameRow["ReleaseDate"]),
                TrailerUrl = gameRow["TrailerUrl"] == DBNull.Value ? string.Empty : gameRow["TrailerUrl"]?.ToString() ?? string.Empty,
                MinimumRequirements = gameRow["MinimumRequirements"] == DBNull.Value ? string.Empty : gameRow["MinimumRequirements"]?.ToString() ?? string.Empty,
                RecommendedRequirements = gameRow["RecommendedRequirements"] == DBNull.Value ? string.Empty : gameRow["RecommendedRequirements"]?.ToString() ?? string.Empty,
                SupportedLanguages = gameRow["SupportedLanguages"] == DBNull.Value ? string.Empty : gameRow["SupportedLanguages"]?.ToString() ?? string.Empty,
                CoverImagePath = gameRow["CoverImagePath"] == DBNull.Value ? string.Empty : gameRow["CoverImagePath"]?.ToString() ?? string.Empty,
                CoverImageSourcePath = GameAssetManager.GetAbsoluteAssetPath(gameRow["CoverImagePath"] == DBNull.Value ? string.Empty : gameRow["CoverImagePath"]?.ToString() ?? string.Empty),
                Platforms = GetGamePlatforms(gameId),
                GalleryImageSourcePaths = GameAssetManager.GetGalleryImagePaths(gameId, false).ToList(),
                HasPendingDraft = HasPendingDraft(gameId)
            };
        }

        public int SaveGameDraft(GameDraftSaveRequest request)
        {
        // yeni surumu onay icin kaydet
            string validationMessage = ValidateGameInput(
                request.Title,
                request.PriceText,
                request.Description,
                request.Developer,
                request.Publisher,
                request.Platforms,
                request.ReleaseDateText,
                request.TrailerUrl,
                request.MinimumRequirements,
                request.RecommendedRequirements,
                request.SupportedLanguages,
                request.CoverImageSourcePath,
                request.GalleryImageSourcePaths);

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            EnsureSchema();

            if (!ApprovedGameExists(request.GameId))
            {
                throw new InvalidOperationException("Seçilen oyun bulunamadı.");
            }

            decimal price = ParsePrice(request.PriceText);
            DateTime releaseDate = DateTime.ParseExact(
                request.ReleaseDateText.Trim(),
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture);

            string coverRelativePath = GameAssetManager.SaveDraftAssets(
                request.GameId,
                request.CoverImageSourcePath,
                request.GalleryImageSourcePaths);

            int gameDraftId = GetPendingDraftId(request.GameId);

            using MySqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();

            using MySqlTransaction transaction = connection.BeginTransaction();

            try
            {
                if (gameDraftId > 0)
                {
                    using MySqlCommand updateDraftCommand = new MySqlCommand(
                        @"UPDATE GameDrafts
                          SET Title = @Title,
                              Description = @Description,
                              Price = @Price,
                              CoverImagePath = @CoverImagePath,
                              Developer = @Developer,
                              Publisher = @Publisher,
                              ReleaseDate = @ReleaseDate,
                              TrailerUrl = @TrailerUrl,
                              MinimumRequirements = @MinimumRequirements,
                              RecommendedRequirements = @RecommendedRequirements,
                              SupportedLanguages = @SupportedLanguages,
                              DraftStatus = 'pending'
                          WHERE GameDraftId = @GameDraftId;",
                        connection,
                        transaction);

                    updateDraftCommand.Parameters.AddWithValue("@Title", request.Title.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@Description", request.Description.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@Price", price);
                    updateDraftCommand.Parameters.AddWithValue("@CoverImagePath", coverRelativePath);
                    updateDraftCommand.Parameters.AddWithValue("@Developer", request.Developer.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@Publisher", request.Publisher.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@ReleaseDate", releaseDate);
                    updateDraftCommand.Parameters.AddWithValue(
                        "@TrailerUrl",
                        string.IsNullOrWhiteSpace(request.TrailerUrl) ? DBNull.Value : request.TrailerUrl.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@MinimumRequirements", request.MinimumRequirements.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@RecommendedRequirements", request.RecommendedRequirements.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@SupportedLanguages", request.SupportedLanguages.Trim());
                    updateDraftCommand.Parameters.AddWithValue("@GameDraftId", gameDraftId);
                    updateDraftCommand.ExecuteNonQuery();
                }
                else
                {
                    using MySqlCommand insertDraftCommand = new MySqlCommand(
                        @"INSERT INTO GameDrafts
                            (GameId, Title, Description, Price, CoverImagePath, Developer, Publisher, ReleaseDate, TrailerUrl, MinimumRequirements, RecommendedRequirements, SupportedLanguages, DraftStatus)
                          VALUES
                            (@GameId, @Title, @Description, @Price, @CoverImagePath, @Developer, @Publisher, @ReleaseDate, @TrailerUrl, @MinimumRequirements, @RecommendedRequirements, @SupportedLanguages, 'pending');",
                        connection,
                        transaction);

                    insertDraftCommand.Parameters.AddWithValue("@GameId", request.GameId);
                    insertDraftCommand.Parameters.AddWithValue("@Title", request.Title.Trim());
                    insertDraftCommand.Parameters.AddWithValue("@Description", request.Description.Trim());
                    insertDraftCommand.Parameters.AddWithValue("@Price", price);
                    insertDraftCommand.Parameters.AddWithValue("@CoverImagePath", coverRelativePath);
                    insertDraftCommand.Parameters.AddWithValue("@Developer", request.Developer.Trim());
                    insertDraftCommand.Parameters.AddWithValue("@Publisher", request.Publisher.Trim());
                    insertDraftCommand.Parameters.AddWithValue("@ReleaseDate", releaseDate);
                    insertDraftCommand.Parameters.AddWithValue(
                        "@TrailerUrl",
                        string.IsNullOrWhiteSpace(request.TrailerUrl) ? DBNull.Value : request.TrailerUrl.Trim());
                    insertDraftCommand.Parameters.AddWithValue("@MinimumRequirements", request.MinimumRequirements.Trim());
                    insertDraftCommand.Parameters.AddWithValue("@RecommendedRequirements", request.RecommendedRequirements.Trim());
                    insertDraftCommand.Parameters.AddWithValue("@SupportedLanguages", request.SupportedLanguages.Trim());
                    insertDraftCommand.ExecuteNonQuery();

                    gameDraftId = Convert.ToInt32(insertDraftCommand.LastInsertedId);
                }

                using MySqlCommand clearPlatformsCommand = new MySqlCommand(
                    "DELETE FROM GameDraftPlatforms WHERE GameDraftId = @GameDraftId;",
                    connection,
                    transaction);

                clearPlatformsCommand.Parameters.AddWithValue("@GameDraftId", gameDraftId);
                clearPlatformsCommand.ExecuteNonQuery();

                foreach (string platform in request.Platforms.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    using MySqlCommand insertPlatformCommand = new MySqlCommand(
                        "INSERT INTO GameDraftPlatforms (GameDraftId, PlatformName) VALUES (@GameDraftId, @PlatformName);",
                        connection,
                        transaction);

                    insertPlatformCommand.Parameters.AddWithValue("@GameDraftId", gameDraftId);
                    insertPlatformCommand.Parameters.AddWithValue("@PlatformName", platform);
                    insertPlatformCommand.ExecuteNonQuery();
                }

                transaction.Commit();
                return gameDraftId;
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                throw;
            }
        }

        public void ApprovePendingNewGame(int gameId)
        {
        // yeni oyunu yayina ac
            EnsureSchema();

            if (!PendingGameExists(gameId))
            {
                throw new InvalidOperationException("Onay bekleyen oyun bulunamadı.");
            }

            DatabaseManager.ExecuteNonQuery(
                "UPDATE Games SET ApprovalStatus = 'approved' WHERE GameId = @GameId AND ApprovalStatus = 'pending';",
                new SqlParameter("@GameId", gameId));
        }

        public void RejectPendingNewGame(int gameId)
        {
        // yeni oyun kaydini tumuyle kaldir
            EnsureSchema();

            if (!PendingGameExists(gameId))
            {
                throw new InvalidOperationException("Onay bekleyen oyun bulunamadı.");
            }

            DatabaseManager.ExecuteNonQuery(
                "DELETE FROM Games WHERE GameId = @GameId AND ApprovalStatus = 'pending';",
                new SqlParameter("@GameId", gameId));

            TryDeleteLiveAssets(gameId);
        }

        public void SetGameListedState(int gameId, bool isListed)
        {
        // listelenme durumunu degistir
            if (!ApprovedGameExists(gameId))
            {
                throw new InvalidOperationException("Seçilen oyun bulunamadı.");
            }

            DatabaseManager.ExecuteNonQuery(
                "UPDATE Games SET IsActive = @IsActive WHERE GameId = @GameId AND ApprovalStatus = 'approved';",
                new SqlParameter("@IsActive", isListed ? 1 : 0),
                new SqlParameter("@GameId", gameId));
        }

        public void DeleteGamePermanently(int gameId)
        {
        // oyunu bagli kayitlariyla birlikte sil
            EnsureSchema();

            if (!ApprovedGameExists(gameId))
            {
                throw new InvalidOperationException("Seçilen oyun bulunamadı.");
            }

            if (GameExistsInLibraries(gameId))
            {
                throw new InvalidOperationException("Bu oyun kullanıcı kütüphanelerinde bulunduğu için kalıcı olarak silinemez. Oyunu liste dışı alabilirsiniz.");
            }

            using MySqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();

            using MySqlTransaction transaction = connection.BeginTransaction();

            try
            {
                using MySqlCommand deleteDraftsCommand = new MySqlCommand(
                    "DELETE FROM GameDrafts WHERE GameId = @GameId;",
                    connection,
                    transaction);

                deleteDraftsCommand.Parameters.AddWithValue("@GameId", gameId);
                deleteDraftsCommand.ExecuteNonQuery();

                using MySqlCommand deletePlatformsCommand = new MySqlCommand(
                    "DELETE FROM GamePlatforms WHERE GameId = @GameId;",
                    connection,
                    transaction);

                deletePlatformsCommand.Parameters.AddWithValue("@GameId", gameId);
                deletePlatformsCommand.ExecuteNonQuery();

                using MySqlCommand deleteGameCommand = new MySqlCommand(
                    "DELETE FROM Games WHERE GameId = @GameId AND ApprovalStatus = 'approved';",
                    connection,
                    transaction);

                deleteGameCommand.Parameters.AddWithValue("@GameId", gameId);
                int affectedRowCount = deleteGameCommand.ExecuteNonQuery();

                if (affectedRowCount == 0)
                {
                    throw new InvalidOperationException("Seçilen oyun silinemedi.");
                }

                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                throw;
            }

            TryDeleteDraftAssets(gameId);
            TryDeleteLiveAssets(gameId);
        }

        public void ApprovePendingDraft(int gameId)
        {
        // yeni surumu yayina al
            EnsureSchema();

            DataTable draftTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameDraftId,
                    Title,
                    Description,
                    Price,
                    CoverImagePath,
                    Developer,
                    Publisher,
                    ReleaseDate,
                    TrailerUrl,
                    MinimumRequirements,
                    RecommendedRequirements,
                    SupportedLanguages
                  FROM GameDrafts
                  WHERE GameId = @GameId
                    AND DraftStatus = 'pending'
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            if (draftTable.Rows.Count == 0)
            {
                throw new InvalidOperationException("Onay bekleyen değişiklik bulunamadı.");
            }

            DataRow draftRow = draftTable.Rows[0];
            int draftId = Convert.ToInt32(draftRow["GameDraftId"]);
            List<string> platforms = GetDraftPlatforms(draftId);
            string draftCoverPath = draftRow["CoverImagePath"] == DBNull.Value ? string.Empty : draftRow["CoverImagePath"]?.ToString() ?? string.Empty;
            string liveCoverPath = GameAssetManager.GetPromotedCoverPath(gameId, draftCoverPath);

            using MySqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();

            using MySqlTransaction transaction = connection.BeginTransaction();

            try
            {
                using MySqlCommand updateGameCommand = new MySqlCommand(
                    @"UPDATE Games
                      SET Title = @Title,
                          Description = @Description,
                          Price = @Price,
                          CoverImagePath = @CoverImagePath,
                          Developer = @Developer,
                          Publisher = @Publisher,
                          ReleaseDate = @ReleaseDate,
                          TrailerUrl = @TrailerUrl,
                          MinimumRequirements = @MinimumRequirements,
                          RecommendedRequirements = @RecommendedRequirements,
                          SupportedLanguages = @SupportedLanguages,
                          ApprovalStatus = 'approved'
                      WHERE GameId = @GameId
                        AND ApprovalStatus = 'approved';",
                    connection,
                    transaction);

                updateGameCommand.Parameters.AddWithValue("@Title", draftRow["Title"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@Description", draftRow["Description"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@Price", Convert.ToDecimal(draftRow["Price"], CultureInfo.InvariantCulture));
                updateGameCommand.Parameters.AddWithValue("@CoverImagePath", string.IsNullOrWhiteSpace(liveCoverPath) ? DBNull.Value : liveCoverPath);
                updateGameCommand.Parameters.AddWithValue("@Developer", draftRow["Developer"] == DBNull.Value ? DBNull.Value : draftRow["Developer"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@Publisher", draftRow["Publisher"] == DBNull.Value ? DBNull.Value : draftRow["Publisher"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@ReleaseDate", draftRow["ReleaseDate"] == DBNull.Value ? DBNull.Value : Convert.ToDateTime(draftRow["ReleaseDate"], CultureInfo.InvariantCulture));
                updateGameCommand.Parameters.AddWithValue("@TrailerUrl", draftRow["TrailerUrl"] == DBNull.Value ? DBNull.Value : draftRow["TrailerUrl"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@MinimumRequirements", draftRow["MinimumRequirements"] == DBNull.Value ? DBNull.Value : draftRow["MinimumRequirements"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@RecommendedRequirements", draftRow["RecommendedRequirements"] == DBNull.Value ? DBNull.Value : draftRow["RecommendedRequirements"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@SupportedLanguages", draftRow["SupportedLanguages"] == DBNull.Value ? DBNull.Value : draftRow["SupportedLanguages"]?.ToString() ?? string.Empty);
                updateGameCommand.Parameters.AddWithValue("@GameId", gameId);
                updateGameCommand.ExecuteNonQuery();

                using MySqlCommand clearLivePlatformsCommand = new MySqlCommand(
                    "DELETE FROM GamePlatforms WHERE GameId = @GameId;",
                    connection,
                    transaction);

                clearLivePlatformsCommand.Parameters.AddWithValue("@GameId", gameId);
                clearLivePlatformsCommand.ExecuteNonQuery();

                InsertPlatforms(connection, transaction, gameId, platforms);

                using MySqlCommand deleteDraftCommand = new MySqlCommand(
                    "DELETE FROM GameDrafts WHERE GameDraftId = @GameDraftId;",
                    connection,
                    transaction);

                deleteDraftCommand.Parameters.AddWithValue("@GameDraftId", draftId);
                deleteDraftCommand.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                throw;
            }

            GameAssetManager.PromoteDraftAssets(gameId);
        }

        public void RejectPendingDraft(int gameId)
        {
        // yeni surumu kayitlardan kaldir
            EnsureSchema();

            int draftId = GetPendingDraftId(gameId);

            if (draftId == 0)
            {
                throw new InvalidOperationException("Onay bekleyen değişiklik bulunamadı.");
            }

            DatabaseManager.ExecuteNonQuery(
                "DELETE FROM GameDrafts WHERE GameDraftId = @GameDraftId;",
                new SqlParameter("@GameDraftId", draftId));

            TryDeleteDraftAssets(gameId);
        }

        private string ValidateGameInput(
            string title,
            string priceText,
            string description,
            string developer,
            string publisher,
            IReadOnlyCollection<string> platforms,
            string releaseDateText,
            string trailerUrl,
            string minimumRequirements,
            string recommendedRequirements,
            string supportedLanguages,
            string coverImageSourcePath,
            IReadOnlyCollection<string> galleryImageSourcePaths)
        {
        // zorunlu alanlari ve kurallari denetle
            if (string.IsNullOrWhiteSpace(title))
            {
                return "Oyun adı zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(priceText))
            {
                return "Fiyat bilgisi zorunludur.";
            }

            try
            {
                decimal price = ParsePrice(priceText);

                if (price < 1 || price > 9999)
                {
                    return "Fiyat 1 ile 9999 arasında olmalıdır.";
                }
            }
            catch
            {
                return "Fiyat alanına 1 ile 9999 arasında geçerli bir tutar girin.";
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return "Açıklama alanı zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(developer))
            {
                return "Yapımcı bilgisi zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(publisher))
            {
                return "Yayıncı bilgisi zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(minimumRequirements))
            {
                return "Minimum sistem gereksinimleri zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(recommendedRequirements))
            {
                return "Önerilen sistem gereksinimleri zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(supportedLanguages))
            {
                return "Desteklenen diller alanı zorunludur.";
            }

            if (platforms.Count == 0)
            {
                return "En az bir platform seçmelisiniz.";
            }

            if (!DateTime.TryParseExact(
                releaseDateText.Trim(),
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
            {
                return "Çıkış tarihini GG.AA.YYYY biçiminde girin.";
            }

            string absoluteCoverPath = GameAssetManager.GetAbsoluteAssetPath(coverImageSourcePath);

            if (string.IsNullOrWhiteSpace(absoluteCoverPath) || !File.Exists(absoluteCoverPath))
            {
                return "Ana görsel seçmeniz zorunludur.";
            }

            string coverValidationMessage = GameAssetManager.ValidateCoverImage(absoluteCoverPath);

            if (!string.IsNullOrWhiteSpace(coverValidationMessage))
            {
                return coverValidationMessage;
            }

            if (galleryImageSourcePaths.Count < MinGalleryImageCount)
            {
                return "En az 3 oyun görseli eklemelisiniz.";
            }

            if (galleryImageSourcePaths.Count > MaxGalleryImageCount)
            {
                return "En fazla 8 oyun görseli ekleyebilirsiniz.";
            }

            if (galleryImageSourcePaths.Any(path => !File.Exists(GameAssetManager.GetAbsoluteAssetPath(path))))
            {
                return "Seçilen oyun görsellerinden bazıları bulunamadı.";
            }

            if (!string.IsNullOrWhiteSpace(trailerUrl))
            {
                if (!Uri.TryCreate(trailerUrl.Trim(), UriKind.Absolute, out Uri? uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    return "Fragman bağlantısı geçerli bir adres olmalıdır.";
                }
            }

            return string.Empty;
        }

        private bool ApprovedGameExists(int gameId)
        {
        // oyunun kayitli olup olmadigini denetle
            object result = DatabaseManager.ExecuteScalar(
                "SELECT COUNT(*) FROM Games WHERE GameId = @GameId AND ApprovalStatus = 'approved';",
                new SqlParameter("@GameId", gameId));

            return result != null && Convert.ToInt32(result) > 0;
        }

        private bool PendingGameExists(int gameId)
        {
        // onay bekleyen oyun kaydini denetle
            object result = DatabaseManager.ExecuteScalar(
                "SELECT COUNT(*) FROM Games WHERE GameId = @GameId AND ApprovalStatus = 'pending';",
                new SqlParameter("@GameId", gameId));

            return result != null && Convert.ToInt32(result) > 0;
        }

        private bool GameExistsInLibraries(int gameId)
        {
        // oyunun kutuphanelerde yer alip almadigini denetle
            object result = DatabaseManager.ExecuteScalar(
                "SELECT COUNT(*) FROM Libraries WHERE GameId = @GameId;",
                new SqlParameter("@GameId", gameId));

            return result != null && Convert.ToInt32(result) > 0;
        }

        private bool HasPendingDraft(int gameId)
        {
        // bekleyen guncelleme kaydi var mi denetle
            return GetPendingDraftId(gameId) > 0;
        }

        private int GetPendingDraftId(int gameId)
        {
        // guncelleme kaydinin kimligini bul
            object result = DatabaseManager.ExecuteScalar(
                @"SELECT GameDraftId
                  FROM GameDrafts
                  WHERE GameId = @GameId
                    AND DraftStatus = 'pending'
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private IReadOnlyList<AdminGameListItem> GetApprovedVisibilityGames(string searchText, bool isListed)
        {
        // liste durumuna gore oyunlari sirala
            EnsureSchema();

            string normalizedSearch = searchText?.Trim() ?? string.Empty;
            DataTable gameTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    g.GameId,
                    g.Title,
                    COALESCE(g.Publisher, '') AS Publisher,
                    COALESCE(g.CoverImagePath, '') AS CoverImagePath,
                    EXISTS(
                        SELECT 1
                        FROM GameDrafts gd
                        WHERE gd.GameId = g.GameId
                          AND gd.DraftStatus = 'pending'
                    ) AS HasPendingDraft
                  FROM Games g
                  WHERE g.ApprovalStatus = 'approved'
                    AND g.IsActive = @IsActive
                    AND (
                        @SearchText = ''
                        OR g.Title LIKE CONCAT('%', @SearchText, '%')
                        OR COALESCE(g.Publisher, '') LIKE CONCAT('%', @SearchText, '%')
                    )
                  ORDER BY g.Title ASC;",
                new SqlParameter("@IsActive", isListed ? 1 : 0),
                new SqlParameter("@SearchText", normalizedSearch));

            List<AdminGameListItem> items = new();

            foreach (DataRow row in gameTable.Rows)
            {
                string coverPath = row["CoverImagePath"]?.ToString() ?? string.Empty;
                bool hasPendingDraft = Convert.ToInt32(row["HasPendingDraft"]) > 0;

                items.Add(new AdminGameListItem
                {
                    GameId = Convert.ToInt32(row["GameId"]),
                    GameDraftId = 0,
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Publisher = row["Publisher"]?.ToString() ?? string.Empty,
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath),
                    HasPendingDraft = false,
                    IsPendingNewGame = false,
                    IsListed = isListed,
                    BadgeText = string.Empty,
                    StatusText = isListed ? "Mağazada listeleniyor" : "Mağazada gizli"
                });
            }

            return items;
        }

        private GameEditState? TryGetPendingNewGameState(int gameId)
        {
        // yeni oyun kaydinin ayrintilarini getir
            DataTable gameTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameId,
                    Title,
                    Description,
                    Price,
                    CoverImagePath,
                    Developer,
                    Publisher,
                    ReleaseDate,
                    TrailerUrl,
                    MinimumRequirements,
                    RecommendedRequirements,
                    SupportedLanguages
                  FROM Games
                  WHERE GameId = @GameId
                    AND ApprovalStatus = 'pending'
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            if (gameTable.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = gameTable.Rows[0];

            return CreateGameState(
                row,
                GetGamePlatforms(gameId),
                GameAssetManager.GetGalleryImagePaths(gameId, false).ToList(),
                false);
        }

        private GameEditState? TryGetApprovedGameState(int gameId)
        {
        // yayindaki oyunun ayrintilarini getir
            DataTable gameTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameId,
                    Title,
                    Description,
                    Price,
                    CoverImagePath,
                    Developer,
                    Publisher,
                    ReleaseDate,
                    TrailerUrl,
                    MinimumRequirements,
                    RecommendedRequirements,
                    SupportedLanguages
                  FROM Games
                  WHERE GameId = @GameId
                    AND ApprovalStatus = 'approved'
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            if (gameTable.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = gameTable.Rows[0];

            return CreateGameState(
                row,
                GetGamePlatforms(gameId),
                GameAssetManager.GetGalleryImagePaths(gameId, false).ToList(),
                HasPendingDraft(gameId));
        }

        private GameEditState? TryGetPendingDraftState(int gameId)
        {
        // yeni surumun ayrintilarini getir
            DataTable draftTable = DatabaseManager.ExecuteQuery(
                @"SELECT
                    GameDraftId,
                    GameId,
                    Title,
                    Description,
                    Price,
                    CoverImagePath,
                    Developer,
                    Publisher,
                    ReleaseDate,
                    TrailerUrl,
                    MinimumRequirements,
                    RecommendedRequirements,
                    SupportedLanguages
                  FROM GameDrafts
                  WHERE GameId = @GameId
                    AND DraftStatus = 'pending'
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            if (draftTable.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = draftTable.Rows[0];
            int draftId = Convert.ToInt32(row["GameDraftId"]);

            return CreateGameState(
                row,
                GetDraftPlatforms(draftId),
                GameAssetManager.GetGalleryImagePaths(gameId, true).ToList(),
                true);
        }

        private List<string> GetGamePlatforms(int gameId)
        {
        // yayindaki platform listesini getir
            return GetPlatforms(
                "SELECT PlatformName FROM GamePlatforms WHERE GameId = @Id ORDER BY PlatformName ASC;",
                new SqlParameter("@Id", gameId));
        }

        private List<string> GetDraftPlatforms(int gameDraftId)
        {
        // yeni surum platform listesini getir
            return GetPlatforms(
                "SELECT PlatformName FROM GameDraftPlatforms WHERE GameDraftId = @Id ORDER BY PlatformName ASC;",
                new SqlParameter("@Id", gameDraftId));
        }

        private List<string> GetPlatforms(string query, SqlParameter parameter)
        {
        // platform adlarini sirali halde topla
            DataTable platformTable = DatabaseManager.ExecuteQuery(query, parameter);
            return platformTable.Rows
                .Cast<DataRow>()
                .Select(row => row["PlatformName"]?.ToString() ?? string.Empty)
                .Where(platform => !string.IsNullOrWhiteSpace(platform))
                .ToList();
        }

        private void InsertPlatforms(MySqlConnection connection, MySqlTransaction transaction, int gameId, IReadOnlyCollection<string> platforms)
        {
        // yayindaki platform kayitlarini yaz
            foreach (string platform in platforms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                using MySqlCommand insertPlatformCommand = new MySqlCommand(
                    "INSERT INTO GamePlatforms (GameId, PlatformName) VALUES (@GameId, @PlatformName);",
                    connection,
                    transaction);

                insertPlatformCommand.Parameters.AddWithValue("@GameId", gameId);
                insertPlatformCommand.Parameters.AddWithValue("@PlatformName", platform);
                insertPlatformCommand.ExecuteNonQuery();
            }
        }

        private void ReplaceDraftPlatforms(MySqlConnection connection, MySqlTransaction transaction, int gameDraftId, IReadOnlyCollection<string> platforms)
        {
        // yeni surum platform kayitlarini yenile
            using MySqlCommand clearPlatformsCommand = new MySqlCommand(
                "DELETE FROM GameDraftPlatforms WHERE GameDraftId = @GameDraftId;",
                connection,
                transaction);

            clearPlatformsCommand.Parameters.AddWithValue("@GameDraftId", gameDraftId);
            clearPlatformsCommand.ExecuteNonQuery();

            foreach (string platform in platforms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                using MySqlCommand insertPlatformCommand = new MySqlCommand(
                    "INSERT INTO GameDraftPlatforms (GameDraftId, PlatformName) VALUES (@GameDraftId, @PlatformName);",
                    connection,
                    transaction);

                insertPlatformCommand.Parameters.AddWithValue("@GameDraftId", gameDraftId);
                insertPlatformCommand.Parameters.AddWithValue("@PlatformName", platform);
                insertPlatformCommand.ExecuteNonQuery();
            }
        }

        private GameEditState CreateGameState(DataRow row, IReadOnlyCollection<string> platforms, IReadOnlyCollection<string> galleryPaths, bool hasPendingDraft)
        {
        // satirdan liste karti verisini kur
            string coverPath = row["CoverImagePath"] == DBNull.Value
                ? string.Empty
                : row["CoverImagePath"]?.ToString() ?? string.Empty;

            return new GameEditState
            {
                GameId = Convert.ToInt32(row["GameId"]),
                Title = row["Title"]?.ToString() ?? string.Empty,
                Description = row["Description"]?.ToString() ?? string.Empty,
                PriceText = FormatPriceText(row["Price"]),
                Developer = row["Developer"] == DBNull.Value ? string.Empty : row["Developer"]?.ToString() ?? string.Empty,
                Publisher = row["Publisher"] == DBNull.Value ? string.Empty : row["Publisher"]?.ToString() ?? string.Empty,
                ReleaseDateText = FormatReleaseDate(row["ReleaseDate"]),
                TrailerUrl = row["TrailerUrl"] == DBNull.Value ? string.Empty : row["TrailerUrl"]?.ToString() ?? string.Empty,
                MinimumRequirements = row["MinimumRequirements"] == DBNull.Value ? string.Empty : row["MinimumRequirements"]?.ToString() ?? string.Empty,
                RecommendedRequirements = row["RecommendedRequirements"] == DBNull.Value ? string.Empty : row["RecommendedRequirements"]?.ToString() ?? string.Empty,
                SupportedLanguages = row["SupportedLanguages"] == DBNull.Value ? string.Empty : row["SupportedLanguages"]?.ToString() ?? string.Empty,
                CoverImagePath = coverPath,
                CoverImageSourcePath = GameAssetManager.GetAbsoluteAssetPath(coverPath),
                Platforms = platforms.ToList(),
                GalleryImageSourcePaths = galleryPaths.ToList(),
                HasPendingDraft = hasPendingDraft
            };
        }

        private decimal ParsePrice(string priceText)
        {
        // fiyat metnini sayiya donustur
            string normalized = priceText.Trim().Replace(" ", string.Empty);

            if (!PriceValidationRegex.IsMatch(normalized))
            {
                throw new FormatException("Geçersiz fiyat");
            }

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out decimal price))
            {
                return price;
            }

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
            {
                return price;
            }

            throw new FormatException("Geçersiz fiyat");
        }

        private string FormatPriceText(object value)
        {
        // fiyat bilgisini gosterime hazirla
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            decimal price = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return price % 1 == 0
                ? price.ToString("0", CultureInfo.GetCultureInfo("tr-TR"))
                : price.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"));
        }

        private string FormatReleaseDate(object value)
        {
        // tarihi ekranda gosterilecek bicime cevir
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            DateTime date = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        private void TryDeleteLiveAssets(int gameId)
        {
        // yayindaki gorselleri temizlemeyi dene
            try
            {
                GameAssetManager.DeleteGameFolder(gameId);
            }
            catch
            {
            }
        }

        private void TryDeleteDraftAssets(int gameId)
        {
        // yeni surum gorsellerini temizlemeyi dene
            try
            {
                GameAssetManager.DeleteDraftFolder(gameId);
            }
            catch
            {
            }
        }
    }
}
