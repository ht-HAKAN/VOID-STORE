namespace VOID_STORE.Models
{
    public class DownloadStateItem
    {
        public int GameId { get; set; }

        public string InstallStatus { get; set; } = "not_installed";

        public string InstallStatusText { get; set; } = "Yüklü değil";

        public string InstallAccent { get; set; } = "#8F98A5";

        public bool ShowProgress { get; set; }

        public double ProgressValue { get; set; }

        public string ProgressText { get; set; } = string.Empty;

        public string SizeText { get; set; } = string.Empty;

        public string PrimaryActionText { get; set; } = "Yükle";

        public string SecondaryActionText { get; set; } = string.Empty;

        public bool ShowSecondaryAction { get; set; }

        public string InstallPath { get; set; } = string.Empty;
    }
}
