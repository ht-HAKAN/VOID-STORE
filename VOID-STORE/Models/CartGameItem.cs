using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class CartGameItem
    {
        public int GameId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public decimal PriceAmount { get; set; }

        public string PriceText { get; set; } = string.Empty;

        public string CoverImagePath { get; set; } = string.Empty;

        public BitmapImage? CoverPreview { get; set; }
    }
}
