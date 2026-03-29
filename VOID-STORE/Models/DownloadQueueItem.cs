using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class DownloadQueueItem : DownloadStateItem
    {
        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string CoverImagePath { get; set; } = string.Empty;

        public BitmapImage? CoverPreview { get; set; }

        public string DownloadedText { get; set; } = string.Empty;
    }
}
