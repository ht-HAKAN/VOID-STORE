using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;
using VOID_STORE.Models;

namespace VOID_STORE.Controllers
{
    public class LaunchController
    {
        private readonly Dictionary<int, Process> _trackedProcesses = new();
        private readonly Dictionary<int, DateTime> _startedAtByGameId = new();

        public bool IsRunning(int gameId)
        {
            // oyun acik mi tek noktadan kontrol et
            return TryGetTrackedProcess(gameId, out Process? process) && process is { HasExited: false };
        }

        public int GetCurrentSessionSeconds(int gameId)
        {
            // aktif oturum suresini saniye olarak ver
            return _startedAtByGameId.TryGetValue(gameId, out DateTime startedAt)
                ? Math.Max(0, (int)(DateTime.Now - startedAt).TotalSeconds)
                : 0;
        }

        public void StartGame(int userId, int gameId, string title, string installPath)
        {
            // kurulumu olmayan oyunu baslatma
            if (userId <= 0)
            {
                throw new InvalidOperationException("Oyunu başlatmak için giriş yapmanız gerekiyor");
            }

            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            {
                throw new InvalidOperationException("Oyun dosyaları hazır değil");
            }

            if (IsRunning(gameId))
            {
                return;
            }

            string launchScriptPath = Path.Combine(installPath, "voidstore_launch_test.cmd");
            WriteLaunchScript(launchScriptPath, title);

            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{launchScriptPath}\"",
                WorkingDirectory = installPath,
                UseShellExecute = true
            };

            Process? process = Process.Start(startInfo);

            if (process == null)
            {
                throw new InvalidOperationException("Oyun penceresi açılamadı");
            }

            _trackedProcesses[gameId] = process;
            _startedAtByGameId[gameId] = DateTime.Now;
        }

        public bool StopGame(int userId, int gameId)
        {
            // acik oyunu durdur ve sureyi kaydet
            if (!TryGetTrackedProcess(gameId, out Process? process))
            {
                return false;
            }

            if (process == null)
            {
                return false;
            }

            if (!process.HasExited)
            {
                process.Kill(true);
                process.WaitForExit(2000);
            }

            FinalizeSession(userId, gameId);
            return true;
        }

        public bool SyncExitedGames(int userId)
        {
            // elle kapanan cmd pencerelerini yakala
            bool changed = false;

            foreach (int gameId in _trackedProcesses.Keys.ToList())
            {
                if (!TryGetTrackedProcess(gameId, out Process? process))
                {
                    continue;
                }

                if (process == null || !process.HasExited)
                {
                    continue;
                }

                FinalizeSession(userId, gameId);
                changed = true;
            }

            return changed;
        }

        private void FinalizeSession(int userId, int gameId)
        {
            // sureyi kaydet ve takibi kapat
            int playedSeconds = GetCurrentSessionSeconds(gameId);

            _trackedProcesses.Remove(gameId);
            _startedAtByGameId.Remove(gameId);

            if (userId <= 0 || playedSeconds <= 0)
            {
                return;
            }

            DatabaseManager.ExecuteNonQuery(
                @"UPDATE UserLibrary
                  SET TotalPlaySeconds = TotalPlaySeconds + @PlayedSeconds,
                      LastPlayedAt = @LastPlayedAt
                  WHERE UserId = @UserId
                    AND GameId = @GameId;",
                new SqlParameter("@PlayedSeconds", playedSeconds),
                new SqlParameter("@LastPlayedAt", DateTime.Now),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@GameId", gameId));
        }

        private bool TryGetTrackedProcess(int gameId, out Process? process)
        {
            // izlenen sureci guvenli al
            if (_trackedProcesses.TryGetValue(gameId, out Process? trackedProcess))
            {
                process = trackedProcess;
                return process != null;
            }

            process = null;
            return false;
        }

        private void WriteLaunchScript(string scriptPath, string title)
        {
            // test penceresini sade bir cmd ile ac
            string safeTitle = string.IsNullOrWhiteSpace(title)
                ? "VOID STORE GAME"
                : string.Concat(title.Where(character => !char.IsControl(character)));

            string[] lines =
            {
                "@echo off",
                "chcp 65001 >nul",
                $"title VOID STORE OYUN BAŞLATMA TEST - {safeTitle}",
                "color 0F",
                "echo ================================================",
                "echo VOID STORE OYUN BAŞLATMA TEST",
                "echo ================================================",
                $"echo Oyun          : {safeTitle}",
                "echo Durum         : başlatıldı",
                "echo.",
                "echo Bu ekran oyunu başlatmayı simüle eder",
                "echo Komut penceresi açık kaldığı süre oyun süresi olarak hesaplanır",
                "echo.",
                ":loop",
                "timeout /t 5 /nobreak >nul",
                "goto loop"
            };

            // cmd dosyasini bom olmadan yaz
            File.WriteAllLines(scriptPath, lines, new UTF8Encoding(false));
        }
    }
}
