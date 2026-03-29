using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using SqlConnection = MySql.Data.MySqlClient.MySqlConnection;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using SqlTransaction = MySql.Data.MySqlClient.MySqlTransaction;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class DownloadController
    {
        private const string NotInstalledStatus = "not_installed";
        private const string QueuedStatus = "queued";
        private const string DownloadingStatus = "downloading";
        private const string PausedStatus = "paused";
        private const string InstalledStatus = "installed";
        private const long MinimumPackageSizeBytes = 96L * 1024 * 1024;
        private const long MinimumTickBytes = 8L * 1024 * 1024;
        private const long MaximumTickBytes = 96L * 1024 * 1024;

        public DownloadController()
        {
            // indirme alanlarini her acilista hazir tut
            UserCommerceSchemaManager.EnsureSchema();
        }

        public IReadOnlyDictionary<int, DownloadStateItem> GetDownloadStates(int userId)
        {
            // misafirde bos durum don
            if (userId <= 0)
            {
                return new Dictionary<int, DownloadStateItem>();
            }

            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT GameId, InstallStatus, ProgressPercent, DownloadedBytes, TotalBytes, InstallPath
                  FROM UserDownloads
                  WHERE UserId = @UserId;",
                new SqlParameter("@UserId", userId));

            Dictionary<int, DownloadStateItem> states = new();

            foreach (DataRow row in table.Rows)
            {
                int gameId = Convert.ToInt32(row["GameId"], CultureInfo.InvariantCulture);
                states[gameId] = BuildStateItem(row);
            }

            return states;
        }

        public IReadOnlyList<DownloadQueueItem> GetDownloadQueue(int userId)
        {
            // misafirde bos indirme listesi don
            if (userId <= 0)
            {
                return new List<DownloadQueueItem>();
            }

            DataTable table = DatabaseManager.ExecuteQuery(
                @"SELECT
                    g.GameId,
                    g.Title,
                    g.Category,
                    g.CoverImagePath,
                    d.InstallStatus,
                    d.ProgressPercent,
                    d.DownloadedBytes,
                    d.TotalBytes,
                    d.InstallPath
                  FROM UserDownloads d
                  INNER JOIN Games g ON g.GameId = d.GameId
                  WHERE d.UserId = @UserId
                  ORDER BY CASE d.InstallStatus
                      WHEN 'downloading' THEN 0
                      WHEN 'queued' THEN 1
                      WHEN 'paused' THEN 2
                      WHEN 'installed' THEN 3
                      ELSE 4
                  END,
                  d.UpdatedAt DESC,
                  d.DownloadId DESC;",
                new SqlParameter("@UserId", userId));

            List<DownloadQueueItem> items = new();

            foreach (DataRow row in table.Rows)
            {
                string coverPath = row["CoverImagePath"] == DBNull.Value
                    ? string.Empty
                    : row["CoverImagePath"]?.ToString() ?? string.Empty;

                DownloadStateItem state = BuildStateItem(row);

                items.Add(new DownloadQueueItem
                {
                    GameId = Convert.ToInt32(row["GameId"], CultureInfo.InvariantCulture),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    Category = row["Category"]?.ToString() ?? string.Empty,
                    CoverImagePath = coverPath,
                    CoverPreview = GameAssetManager.LoadBitmap(coverPath),
                    InstallStatus = state.InstallStatus,
                    InstallStatusText = state.InstallStatusText,
                    InstallAccent = state.InstallAccent,
                    ShowProgress = state.ShowProgress,
                    ProgressValue = state.ProgressValue,
                    ProgressText = state.ProgressText,
                    SizeText = state.SizeText,
                    PrimaryActionText = state.PrimaryActionText,
                    SecondaryActionText = state.SecondaryActionText,
                    ShowSecondaryAction = state.ShowSecondaryAction,
                    InstallPath = state.InstallPath,
                    DownloadedText = BuildDownloadedText(
                        ReadLong(row, "DownloadedBytes"),
                        ReadLong(row, "TotalBytes"))
                });
            }

            return items;
        }

        public void QueueInstall(int userId, int gameId)
        {
            // kurulum sadece oturumla ilerler
            if (userId <= 0)
            {
                throw new InvalidOperationException("Kurulum için giriş yapmanız gerekiyor");
            }

            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            EnsureOwnedGame(connection, transaction, userId, gameId);

            DataRow? currentRow = GetDownloadRow(connection, transaction, userId, gameId, includeTitle: true);
            string title = currentRow?["Title"]?.ToString() ?? GetGameTitle(connection, transaction, gameId);

            if (currentRow != null && GetInstallStatus(currentRow) == InstalledStatus)
            {
                transaction.Commit();
                return;
            }

            long totalBytes = ResolvePackageSizeBytes(gameId);
            string installPath = BuildInstallPath(userId, gameId, title);
            bool hasActiveDownload = HasActiveDownload(connection, transaction, userId, gameId);
            string nextStatus = hasActiveDownload ? QueuedStatus : DownloadingStatus;

            if (currentRow == null)
            {
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"INSERT INTO UserDownloads
                        (UserId, GameId, InstallStatus, ProgressPercent, DownloadedBytes, TotalBytes, InstallPath)
                      VALUES
                        (@UserId, @GameId, @InstallStatus, 0.00, 0, @TotalBytes, @InstallPath);",
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@GameId", gameId),
                    new SqlParameter("@InstallStatus", nextStatus),
                    new SqlParameter("@TotalBytes", totalBytes),
                    new SqlParameter("@InstallPath", installPath));
            }
            else
            {
                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"UPDATE UserDownloads
                      SET InstallStatus = @InstallStatus,
                          ProgressPercent = 0.00,
                          DownloadedBytes = 0,
                          TotalBytes = @TotalBytes,
                          InstallPath = @InstallPath,
                          CompletedAt = NULL
                      WHERE UserId = @UserId
                        AND GameId = @GameId;",
                    new SqlParameter("@InstallStatus", nextStatus),
                    new SqlParameter("@TotalBytes", totalBytes),
                    new SqlParameter("@InstallPath", installPath),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@GameId", gameId));
            }

            transaction.Commit();
        }

        public void PauseDownload(int userId, int gameId)
        {
            // aktif veya siradaki indirmeyi duraklat
            UpdateDownloadStatus(userId, gameId, currentStatus =>
            {
                return currentStatus == DownloadingStatus || currentStatus == QueuedStatus
                    ? PausedStatus
                    : currentStatus;
            });
        }

        public void ResumeDownload(int userId, int gameId)
        {
            // duran indirmeyi uygun slota geri al
            if (userId <= 0)
            {
                throw new InvalidOperationException("İndirme için giriş yapmanız gerekiyor");
            }

            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            DataRow? currentRow = GetDownloadRow(connection, transaction, userId, gameId, includeTitle: false);

            if (currentRow == null)
            {
                transaction.Commit();
                return;
            }

            string status = GetInstallStatus(currentRow);

            if (status != PausedStatus)
            {
                transaction.Commit();
                return;
            }

            bool hasActiveDownload = HasActiveDownload(connection, transaction, userId, gameId);
            string nextStatus = hasActiveDownload ? QueuedStatus : DownloadingStatus;

            ExecuteNonQuery(
                connection,
                transaction,
                @"UPDATE UserDownloads
                  SET InstallStatus = @InstallStatus
                  WHERE UserId = @UserId
                    AND GameId = @GameId;",
                new SqlParameter("@InstallStatus", nextStatus),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));

            transaction.Commit();
        }

        public void CancelDownload(int userId, int gameId)
        {
            // tamamlanmayan indirmeyi listeden cikar
            if (userId <= 0)
            {
                return;
            }

            string? installPath = GetInstallPath(userId, gameId);

            DatabaseManager.ExecuteNonQuery(
                @"DELETE FROM UserDownloads
                  WHERE UserId = @UserId
                    AND GameId = @GameId
                    AND InstallStatus <> 'installed';",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));

            DeleteInstallFolder(installPath);
        }

        public void Uninstall(int userId, int gameId)
        {
            // kurulu oyunu sistemden kaldir
            if (userId <= 0)
            {
                return;
            }

            string? installPath = GetInstallPath(userId, gameId);
            DeleteInstallFolder(installPath);

            DatabaseManager.ExecuteNonQuery(
                @"DELETE FROM UserDownloads
                  WHERE UserId = @UserId
                    AND GameId = @GameId;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));
        }

        public bool ProcessDownloadQueue(int userId)
        {
            // misafirde arka plan indirmesi olmaz
            if (userId <= 0)
            {
                return false;
            }

            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();

            DataRow? workingRow;

            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                workingRow = GetActiveOrQueuedRow(connection, transaction, userId);

                if (workingRow == null)
                {
                    transaction.Commit();
                    return false;
                }

                int gameId = Convert.ToInt32(workingRow["GameId"], CultureInfo.InvariantCulture);
                long totalBytes = Math.Max(ReadLong(workingRow, "TotalBytes"), MinimumPackageSizeBytes);
                long downloadedBytes = ReadLong(workingRow, "DownloadedBytes");
                long tickBytes = ResolveTickBytes(totalBytes);
                long nextDownloadedBytes = Math.Min(totalBytes, downloadedBytes + tickBytes);

                ExecuteNonQuery(
                    connection,
                    transaction,
                    @"UPDATE UserDownloads
                      SET InstallStatus = @InstallStatus,
                          DownloadedBytes = @DownloadedBytes,
                          TotalBytes = @TotalBytes,
                          ProgressPercent = @ProgressPercent
                      WHERE UserId = @UserId
                        AND GameId = @GameId;",
                    new SqlParameter("@InstallStatus", DownloadingStatus),
                    new SqlParameter("@DownloadedBytes", nextDownloadedBytes),
                    new SqlParameter("@TotalBytes", totalBytes),
                    new SqlParameter("@ProgressPercent", BuildProgressPercent(nextDownloadedBytes, totalBytes)),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@GameId", gameId));

                transaction.Commit();

                if (nextDownloadedBytes < totalBytes)
                {
                    return true;
                }
            }

            FinalizeInstall(userId, workingRow);
            return true;
        }

        private void FinalizeInstall(int userId, DataRow workingRow)
        {
            // indirme tamamlandiginda dosyalari kurulum klasorune yaz
            int gameId = Convert.ToInt32(workingRow["GameId"], CultureInfo.InvariantCulture);
            string title = workingRow["Title"]?.ToString() ?? string.Empty;
            long totalBytes = Math.Max(ReadLong(workingRow, "TotalBytes"), MinimumPackageSizeBytes);
            string installPath = workingRow["InstallPath"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(installPath))
            {
                installPath = BuildInstallPath(userId, gameId, title);
            }

            try
            {
                PrepareInstallFolder(installPath);
                WriteInstallPayload(gameId, title, installPath, totalBytes);

                DatabaseManager.ExecuteNonQuery(
                    @"UPDATE UserDownloads
                      SET InstallStatus = @InstallStatus,
                          ProgressPercent = 100.00,
                          DownloadedBytes = @TotalBytes,
                          TotalBytes = @TotalBytes,
                          InstallPath = @InstallPath,
                          CompletedAt = NOW()
                      WHERE UserId = @UserId
                        AND GameId = @GameId;",
                    new SqlParameter("@InstallStatus", InstalledStatus),
                    new SqlParameter("@TotalBytes", totalBytes),
                    new SqlParameter("@InstallPath", installPath),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@GameId", gameId));
            }
            catch
            {
                // sorunlu kurulum tekrar devam ettirilebilsin
                DatabaseManager.ExecuteNonQuery(
                    @"UPDATE UserDownloads
                      SET InstallStatus = @InstallStatus,
                          ProgressPercent = 96.00,
                          DownloadedBytes = @DownloadedBytes,
                          TotalBytes = @TotalBytes
                      WHERE UserId = @UserId
                        AND GameId = @GameId;",
                    new SqlParameter("@InstallStatus", PausedStatus),
                    new SqlParameter("@DownloadedBytes", Math.Max(totalBytes - MinimumTickBytes, 0)),
                    new SqlParameter("@TotalBytes", totalBytes),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@GameId", gameId));

                throw;
            }
        }

        private void WriteInstallPayload(int gameId, string title, string installPath, long totalBytes)
        {
            // hedef klasore gercek dosyalari ve manifesti yaz
            string contentFolder = Path.Combine(installPath, "content");
            string sourceFolder = GameAssetManager.GetGameFolder(gameId);
            Directory.CreateDirectory(contentFolder);

            if (Directory.Exists(sourceFolder))
            {
                CopyDirectory(sourceFolder, contentFolder);
            }

            string manifestPath = Path.Combine(installPath, "install_manifest.json");
            string manifestJson = JsonSerializer.Serialize(
                new
                {
                    gameId,
                    title,
                    installedAt = DateTime.Now,
                    totalBytes,
                    contentFolder
                },
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(manifestPath, manifestJson);
        }

        private void PrepareInstallFolder(string installPath)
        {
            // temiz kurulum icin klasoru bastan kur
            if (Directory.Exists(installPath))
            {
                Directory.Delete(installPath, true);
            }

            Directory.CreateDirectory(installPath);
        }

        private DownloadStateItem BuildStateItem(DataRow row)
        {
            // ham tablo verisini ekrana uygun duruma cevir
            string status = GetInstallStatus(row);
            long downloadedBytes = ReadLong(row, "DownloadedBytes");
            long totalBytes = ReadLong(row, "TotalBytes");
            double progressValue = Convert.ToDouble(row["ProgressPercent"], CultureInfo.InvariantCulture);

            DownloadStateItem item = new()
            {
                GameId = Convert.ToInt32(row["GameId"], CultureInfo.InvariantCulture),
                InstallStatus = status,
                ProgressValue = progressValue,
                ProgressText = BuildProgressText(status, progressValue, downloadedBytes, totalBytes),
                SizeText = totalBytes > 0 ? FormatBytes(totalBytes) : string.Empty,
                InstallPath = row["InstallPath"] == DBNull.Value
                    ? string.Empty
                    : row["InstallPath"]?.ToString() ?? string.Empty
            };

            switch (status)
            {
                case DownloadingStatus:
                    item.InstallStatusText = "İndiriliyor";
                    item.InstallAccent = "#6FCBFF";
                    item.ShowProgress = true;
                    item.PrimaryActionText = "Duraklat";
                    item.SecondaryActionText = "İptal";
                    item.ShowSecondaryAction = true;
                    break;

                case QueuedStatus:
                    item.InstallStatusText = "Sırada";
                    item.InstallAccent = "#F5D174";
                    item.ShowProgress = totalBytes > 0;
                    item.PrimaryActionText = "Duraklat";
                    item.SecondaryActionText = "İptal";
                    item.ShowSecondaryAction = true;
                    break;

                case PausedStatus:
                    item.InstallStatusText = "Duraklatıldı";
                    item.InstallAccent = "#F3A761";
                    item.ShowProgress = totalBytes > 0;
                    item.PrimaryActionText = "Devam Et";
                    item.SecondaryActionText = "İptal";
                    item.ShowSecondaryAction = true;
                    break;

                case InstalledStatus:
                    item.InstallStatusText = "Kurulu";
                    item.InstallAccent = "#82E4B0";
                    item.ShowProgress = false;
                    item.PrimaryActionText = "Dosyaları Aç";
                    item.SecondaryActionText = "Kaldır";
                    item.ShowSecondaryAction = true;
                    break;

                default:
                    item.InstallStatus = NotInstalledStatus;
                    item.InstallStatusText = "Kurulu değil";
                    item.InstallAccent = "#8F98A5";
                    item.ShowProgress = false;
                    item.PrimaryActionText = "Kurulumu Başlat";
                    item.SecondaryActionText = string.Empty;
                    item.ShowSecondaryAction = false;
                    item.ProgressValue = 0;
                    item.ProgressText = string.Empty;
                    break;
            }

            return item;
        }

        private string BuildProgressText(string status, double progressValue, long downloadedBytes, long totalBytes)
        {
            // ilerleme satirini tek dilde kur
            if (status == InstalledStatus)
            {
                return "Kurulum tamamlandı";
            }

            if (totalBytes <= 0)
            {
                return string.Empty;
            }

            return $"%{progressValue:0}  {FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}";
        }

        private string BuildDownloadedText(long downloadedBytes, long totalBytes)
        {
            // indirme satirini kisa tut
            if (totalBytes <= 0)
            {
                return string.Empty;
            }

            return $"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}";
        }

        private void UpdateDownloadStatus(int userId, int gameId, Func<string, string> resolver)
        {
            // kucuk durum guncellemelerini ortaklastir
            if (userId <= 0)
            {
                return;
            }

            using SqlConnection connection = DatabaseManager.GetConnection();
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();

            DataRow? row = GetDownloadRow(connection, transaction, userId, gameId, includeTitle: false);

            if (row == null)
            {
                transaction.Commit();
                return;
            }

            string currentStatus = GetInstallStatus(row);
            string nextStatus = resolver(currentStatus);

            if (nextStatus == currentStatus)
            {
                transaction.Commit();
                return;
            }

            ExecuteNonQuery(
                connection,
                transaction,
                @"UPDATE UserDownloads
                  SET InstallStatus = @InstallStatus
                  WHERE UserId = @UserId
                    AND GameId = @GameId;",
                new SqlParameter("@InstallStatus", nextStatus),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));

            transaction.Commit();
        }

        private void EnsureOwnedGame(SqlConnection connection, SqlTransaction transaction, int userId, int gameId)
        {
            // sadece sahip olunan oyun kurulabilir
            object? result = ExecuteScalar(
                connection,
                transaction,
                @"SELECT COUNT(*)
                  FROM UserLibrary
                  WHERE UserId = @UserId
                    AND GameId = @GameId;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));

            if (result == null || Convert.ToInt32(result, CultureInfo.InvariantCulture) <= 0)
            {
                throw new InvalidOperationException("Kurulum için önce oyunu satın almalısınız");
            }
        }

        private DataRow? GetDownloadRow(SqlConnection connection, SqlTransaction transaction, int userId, int gameId, bool includeTitle)
        {
            // tek oyun için indirme kaydını getir
            string selectTitle = includeTitle ? ", g.Title" : string.Empty;
            string joinTitle = includeTitle ? " INNER JOIN Games g ON g.GameId = d.GameId" : string.Empty;

            DataTable table = ExecuteQuery(
                connection,
                transaction,
                $@"SELECT d.GameId, d.InstallStatus, d.ProgressPercent, d.DownloadedBytes, d.TotalBytes, d.InstallPath{selectTitle}
                   FROM UserDownloads d{joinTitle}
                   WHERE d.UserId = @UserId
                     AND d.GameId = @GameId
                   LIMIT 1;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));

            return table.Rows.Count == 0 ? null : table.Rows[0];
        }

        private DataRow? GetActiveOrQueuedRow(SqlConnection connection, SqlTransaction transaction, int userId)
        {
            // tek aktif indirme yoksa siradaki oyunu cek
            DataTable activeTable = ExecuteQuery(
                connection,
                transaction,
                @"SELECT d.GameId, d.InstallStatus, d.ProgressPercent, d.DownloadedBytes, d.TotalBytes, d.InstallPath, g.Title
                  FROM UserDownloads d
                  INNER JOIN Games g ON g.GameId = d.GameId
                  WHERE d.UserId = @UserId
                    AND d.InstallStatus = 'downloading'
                  ORDER BY d.UpdatedAt ASC
                  LIMIT 1;",
                new SqlParameter("@UserId", userId));

            if (activeTable.Rows.Count > 0)
            {
                return activeTable.Rows[0];
            }

            DataTable queuedTable = ExecuteQuery(
                connection,
                transaction,
                @"SELECT d.GameId, d.InstallStatus, d.ProgressPercent, d.DownloadedBytes, d.TotalBytes, d.InstallPath, g.Title
                  FROM UserDownloads d
                  INNER JOIN Games g ON g.GameId = d.GameId
                  WHERE d.UserId = @UserId
                    AND d.InstallStatus = 'queued'
                  ORDER BY d.CreatedAt ASC, d.DownloadId ASC
                  LIMIT 1;",
                new SqlParameter("@UserId", userId));

            if (queuedTable.Rows.Count == 0)
            {
                return null;
            }

            return queuedTable.Rows[0];
        }

        private bool HasActiveDownload(SqlConnection connection, SqlTransaction transaction, int userId, int ignoreGameId)
        {
            // ayni anda tek aktif indirme kalsin
            object? result = ExecuteScalar(
                connection,
                transaction,
                @"SELECT COUNT(*)
                  FROM UserDownloads
                  WHERE UserId = @UserId
                    AND InstallStatus = 'downloading'
                    AND GameId <> @GameId;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", ignoreGameId));

            return result != null && Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
        }

        private string GetGameTitle(SqlConnection connection, SqlTransaction transaction, int gameId)
        {
            // kurulum klasoru icin oyun adini cek
            object? result = ExecuteScalar(
                connection,
                transaction,
                @"SELECT Title
                  FROM Games
                  WHERE GameId = @GameId
                  LIMIT 1;",
                new SqlParameter("@GameId", gameId));

            return result?.ToString() ?? $"game_{gameId}";
        }

        private long ResolvePackageSizeBytes(int gameId)
        {
            // oyun varliklarindan gercek boyut tahmini cikar
            string sourceFolder = GameAssetManager.GetGameFolder(gameId);
            long folderSize = Directory.Exists(sourceFolder) ? GetDirectorySize(sourceFolder) : 0;
            return Math.Max(folderSize, MinimumPackageSizeBytes);
        }

        private string BuildInstallPath(int userId, int gameId, string title)
        {
            // kurulum klasorunu kullanici ve oyuna gore ayir
            string installRoot = GetInstallRoot();
            string slug = BuildSlug(title);
            return Path.Combine(installRoot, $"user_{userId}", $"{gameId}_{slug}");
        }

        private string GetInstallRoot()
        {
            // kurulumlari uygulama veri klasorunde tut
            string appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string installRoot = Path.Combine(appDataRoot, "VOID STORE", "Installs");
            Directory.CreateDirectory(installRoot);
            return installRoot;
        }

        private string BuildSlug(string title)
        {
            // klasor adini temiz ve guvenli tut
            if (string.IsNullOrWhiteSpace(title))
            {
                return "game";
            }

            char[] buffer = title
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray();

            string slug = new string(buffer);

            while (slug.Contains("--", StringComparison.Ordinal))
            {
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            }

            return slug.Trim('-');
        }

        private long ResolveTickBytes(long totalBytes)
        {
            // ilerleme hizi oyun boyutuna gore dengelensin
            long sliceBytes = totalBytes / 28;
            return Math.Max(MinimumTickBytes, Math.Min(sliceBytes, MaximumTickBytes));
        }

        private decimal BuildProgressPercent(long downloadedBytes, long totalBytes)
        {
            // yuzde hesabini tek noktada sabitle
            if (totalBytes <= 0)
            {
                return 0;
            }

            decimal ratio = (decimal)downloadedBytes / totalBytes;
            return Math.Min(100m, Math.Round(ratio * 100m, 2));
        }

        private string? GetInstallPath(int userId, int gameId)
        {
            // kayitli klasor yolunu oku
            object? result = DatabaseManager.ExecuteScalar(
                @"SELECT InstallPath
                  FROM UserDownloads
                  WHERE UserId = @UserId
                    AND GameId = @GameId
                  LIMIT 1;",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));

            return result == null || result == DBNull.Value
                ? null
                : result.ToString();
        }

        private void DeleteInstallFolder(string? installPath)
        {
            // sistemden kaldirilan klasoru temizle
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            {
                return;
            }

            Directory.Delete(installPath, true);
        }

        private long ReadLong(DataRow row, string columnName)
        {
            // buyuk sayisal alanlari guvenli oku
            return row[columnName] == DBNull.Value
                ? 0
                : Convert.ToInt64(row[columnName], CultureInfo.InvariantCulture);
        }

        private string GetInstallStatus(DataRow row)
        {
            // eksik durumda varsayilan degeri koru
            string rawStatus = row["InstallStatus"]?.ToString() ?? string.Empty;
            return string.IsNullOrWhiteSpace(rawStatus) ? NotInstalledStatus : rawStatus.Trim().ToLowerInvariant();
        }

        private string FormatBytes(long value)
        {
            // byte degerini okunur sekilde yaz
            if (value <= 0)
            {
                return "0 MB";
            }

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = value;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            string format = unitIndex <= 1 ? "0" : "0.##";
            return $"{size.ToString(format, CultureInfo.InvariantCulture)} {units[unitIndex]}";
        }

        private long GetDirectorySize(string directoryPath)
        {
            // klasor boyutunu alt klasorlerle topla
            DirectoryInfo root = new(directoryPath);

            if (!root.Exists)
            {
                return 0;
            }

            return root
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }

        private void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            // klasoru tum dosyalariyla hedefe kopyala
            Directory.CreateDirectory(targetDirectory);

            foreach (string filePath in Directory.GetFiles(sourceDirectory))
            {
                string targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(filePath));
                File.Copy(filePath, targetFilePath, true);
            }

            foreach (string childDirectory in Directory.GetDirectories(sourceDirectory))
            {
                string childTargetDirectory = Path.Combine(targetDirectory, Path.GetFileName(childDirectory));
                CopyDirectory(childDirectory, childTargetDirectory);
            }
        }

        private DataTable ExecuteQuery(SqlConnection connection, SqlTransaction transaction, string query, params SqlParameter[] parameters)
        {
            // acik baglantida sorgu calistir
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = query;

            if (parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            using var adapter = new MySql.Data.MySqlClient.MySqlDataAdapter(command);
            DataTable table = new();
            adapter.Fill(table);
            return table;
        }

        private object? ExecuteScalar(SqlConnection connection, SqlTransaction transaction, string query, params SqlParameter[] parameters)
        {
            // tek degerli sorguyu ortak noktadan calistir
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = query;

            if (parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            return command.ExecuteScalar();
        }

        private void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string query, params SqlParameter[] parameters)
        {
            // yazma sorgularini tekrar kullan
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = query;

            if (parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            command.ExecuteNonQuery();
        }
    }
}
