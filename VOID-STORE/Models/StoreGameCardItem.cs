using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class StoreGameCardItem
    {
        public int GameId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string PriceText { get; set; } = string.Empty;

        public string CoverImagePath { get; set; } = string.Empty;

        public BitmapImage? CoverPreview { get; set; }
    }
}
