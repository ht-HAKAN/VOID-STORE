using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class StoreGameDetail
    {
        public int GameId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PriceText { get; set; } = string.Empty;

        public decimal PriceAmount { get; set; }

        public string CoverImagePath { get; set; } = string.Empty;

        public BitmapImage? CoverPreview { get; set; }

        public string Developer { get; set; } = string.Empty;

        public string Publisher { get; set; } = string.Empty;

        public string ReleaseDateText { get; set; } = string.Empty;

        public string TrailerVideoPath { get; set; } = string.Empty;

        public string MinimumRequirements { get; set; } = string.Empty;

        public string RecommendedRequirements { get; set; } = string.Empty;

        public string SupportedLanguages { get; set; } = string.Empty;

        public bool IsOwned { get; set; }

        public bool IsInCart { get; set; }

        public string InstallStatus { get; set; } = "not_installed";

        public string InstallStatusText { get; set; } = string.Empty;

        public string InstallAccent { get; set; } = "#8F98A5";

        public bool ShowInstallProgress { get; set; }

        public double InstallProgressValue { get; set; }

        public string InstallProgressText { get; set; } = string.Empty;

        public string PrimaryInstallActionText { get; set; } = "Kurulumu Başlat";

        public string SecondaryInstallActionText { get; set; } = string.Empty;

        public bool ShowSecondaryInstallAction { get; set; }

        public string InstallPath { get; set; } = string.Empty;

        public List<string> Platforms { get; set; } = new();

        public List<string> Features { get; set; } = new();

        public List<StoreMediaItem> MediaItems { get; set; } = new();
    }
}
