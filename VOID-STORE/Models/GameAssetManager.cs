using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public static class GameAssetManager
    {
        private const int MinCoverWidth = 600;
        private const int MinCoverHeight = 800;

        public static string GetAssetRoot()
        {
        // oyun gorsellerinin ana klasorunu belirle
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string projectDirectory = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\.."));

            if (Directory.Exists(Path.Combine(projectDirectory, "voidstoreimages")))
            {
                return Path.Combine(projectDirectory, "voidstoregames");
            }

            return Path.Combine(baseDirectory, "voidstoregames");
        }

        public static string GetGameFolder(int gameId)
        {
        // yayindaki surumun klasor yolunu hazirla
            return Path.Combine(GetAssetRoot(), gameId.ToString());
        }

        public static string GetDraftGameFolder(int gameId)
        {
        // bekleyen surumun klasor yolunu hazirla
            return Path.Combine(GetAssetRoot(), "drafts", gameId.ToString());
        }

        public static string GetAbsoluteAssetPath(string assetPath)
        {
        // kayitli yolu gercek dosya yoluna cevir
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(assetPath))
            {
                return assetPath;
            }

            string normalizedPath = assetPath.Replace("/", Path.DirectorySeparatorChar.ToString());
            return Path.Combine(Path.GetDirectoryName(GetAssetRoot()) ?? GetAssetRoot(), normalizedPath);
        }

        public static IReadOnlyList<string> GetGalleryImagePaths(int gameId, bool useDraftFolder)
        {
        // galeri dosyalarini sirasiyla topla
            string targetFolder = useDraftFolder ? GetDraftGameFolder(gameId) : GetGameFolder(gameId);

            if (!Directory.Exists(targetFolder))
            {
                return Array.Empty<string>();
            }

            return Directory
                .GetFiles(targetFolder, "gallery_*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string SaveGameAssets(int gameId, string coverSourcePath, IReadOnlyList<string> gallerySourcePaths)
        {
        // yayindaki dosyalari ilgili klasore kopyala
            return SaveAssets(gameId, coverSourcePath, gallerySourcePaths, false);
        }

        public static string SaveDraftAssets(int gameId, string coverSourcePath, IReadOnlyList<string> gallerySourcePaths)
        {
        // bekleyen surum dosyalarini ilgili klasore kopyala
            return SaveAssets(gameId, coverSourcePath, gallerySourcePaths, true);
        }

        public static void DeleteGameFolder(int gameId)
        {
        // yayindaki klasoru temizlemeyi dene
            string gameFolder = GetGameFolder(gameId);

            if (Directory.Exists(gameFolder))
            {
                Directory.Delete(gameFolder, true);
            }
        }

        public static void DeleteDraftFolder(int gameId)
        {
        // bekleyen surum klasorunu temizlemeyi dene
            string draftFolder = GetDraftGameFolder(gameId);

            if (Directory.Exists(draftFolder))
            {
                Directory.Delete(draftFolder, true);
            }
        }

        public static string GetPromotedCoverPath(int gameId, string draftCoverPath)
        {
        // bekleyen kapak yolunu yayin yoluna uyarla
            if (string.IsNullOrWhiteSpace(draftCoverPath))
            {
                return string.Empty;
            }

            string draftPrefix = $"voidstoregames/drafts/{gameId}/";
            string livePrefix = $"voidstoregames/{gameId}/";

            return draftCoverPath.Replace(draftPrefix, livePrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static void PromoteDraftAssets(int gameId)
        {
        // bekleyen klasoru yayin klasorune tasimaya calis
            string draftFolder = GetDraftGameFolder(gameId);

            if (!Directory.Exists(draftFolder))
            {
                return;
            }

            string liveFolder = GetGameFolder(gameId);
            string parentFolder = Path.GetDirectoryName(liveFolder) ?? GetAssetRoot();
            Directory.CreateDirectory(parentFolder);

            if (Directory.Exists(liveFolder))
            {
                Directory.Delete(liveFolder, true);
            }

            Directory.Move(draftFolder, liveFolder);
        }

        public static BitmapImage? LoadBitmap(string imagePath)
        {
        // onizleme gorselini guvenli bicimde yukle
            string absolutePath = GetAbsoluteAssetPath(imagePath);

            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return null;
            }

            byte[] imageBytes = File.ReadAllBytes(absolutePath);

            using MemoryStream stream = new MemoryStream(imageBytes);
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public static string ValidateCoverImage(string imagePath)
        {
        // kapak gorselinin boyutunu ve oranini denetle
            string absolutePath = GetAbsoluteAssetPath(imagePath);

            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return "Ana görsel bulunamadı.";
            }

            try
            {
                using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);

                if (decoder.Frames.Count == 0)
                {
                    return "Ana görsel okunamadı.";
                }

                BitmapFrame frame = decoder.Frames[0];

                if (frame.PixelWidth < MinCoverWidth || frame.PixelHeight < MinCoverHeight)
                {
                    return $"Ana görsel en az {MinCoverWidth} x {MinCoverHeight} çözünürlüğünde olmalıdır.";
                }

                if (frame.PixelWidth >= frame.PixelHeight)
                {
                    return "Ana görsel dikey kapak formatına uygun olmalıdır.";
                }
            }
            catch
            {
                return "Ana görsel okunamadı.";
            }

            return string.Empty;
        }

        private static string SaveAssets(int gameId, string coverSourcePath, IReadOnlyList<string> gallerySourcePaths, bool useDraftFolder)
        {
        // secilen gorselleri hedef klasore yerlestir
            string targetFolder = useDraftFolder ? GetDraftGameFolder(gameId) : GetGameFolder(gameId);
            string parentFolder = Path.GetDirectoryName(targetFolder) ?? GetAssetRoot();
            string relativeFolder = useDraftFolder
                ? $"voidstoregames/drafts/{gameId}"
                : $"voidstoregames/{gameId}";

            Directory.CreateDirectory(parentFolder);

            string tempFolder = Path.Combine(parentFolder, $"{gameId}_tmp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);

            try
            {
                string coverRelativePath = CopyAsset(coverSourcePath, tempFolder, "cover", relativeFolder);

                for (int i = 0; i < gallerySourcePaths.Count; i++)
                {
                    CopyAsset(gallerySourcePaths[i], tempFolder, $"gallery_{i + 1:00}", relativeFolder);
                }

                if (Directory.Exists(targetFolder))
                {
                    Directory.Delete(targetFolder, true);
                }

                Directory.Move(tempFolder, targetFolder);
                return coverRelativePath;
            }
            catch
            {
                if (Directory.Exists(tempFolder))
                {
                    try
                    {
                        Directory.Delete(tempFolder, true);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        private static string CopyAsset(string sourcePath, string destinationFolder, string fileNameWithoutExtension, string relativeFolder)
        {
        // tek gorseli hedef klasore aktar
            string absoluteSourcePath = GetAbsoluteAssetPath(sourcePath);
            string extension = Path.GetExtension(absoluteSourcePath);

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            string destinationFileName = $"{fileNameWithoutExtension}{extension.ToLowerInvariant()}";
            string destinationPath = Path.Combine(destinationFolder, destinationFileName);

            File.Copy(absoluteSourcePath, destinationPath, true);
            return $"{relativeFolder}/{destinationFileName}";
        }
    }
}
