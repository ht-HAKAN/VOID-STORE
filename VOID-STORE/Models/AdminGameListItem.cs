using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class AdminGameListItem
    {
    // kayda ait kimlik bilgileri
        public int GameId { get; set; }

        public int GameDraftId { get; set; }

    // listede gosterilen ana bilgiler
        public string Title { get; set; } = string.Empty;

        public string Publisher { get; set; } = string.Empty;

    // kapak gorseline ait bilgiler
        public string CoverImagePath { get; set; } = string.Empty;

        public BitmapImage? CoverPreview { get; set; }

    // yayin durumuna ait bilgiler
        public bool HasPendingDraft { get; set; }

        public bool IsPendingNewGame { get; set; }

        public bool IsListed { get; set; }

        public string BadgeText { get; set; } = string.Empty;

        public string StatusText { get; set; } = string.Empty;
    }
}
