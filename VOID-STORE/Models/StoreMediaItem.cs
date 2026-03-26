using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class StoreMediaItem
    {
        public string Name { get; set; } = string.Empty;

        public string MediaUrl { get; set; } = string.Empty;

        public string ImagePath { get; set; } = string.Empty;

        public BitmapImage? Preview { get; set; }

        public bool IsTrailer { get; set; }

        public bool IsSelected { get; set; }
    }
}
