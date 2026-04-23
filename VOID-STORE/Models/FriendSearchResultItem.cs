using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class FriendSearchResultItem
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string AvatarLetter { get; set; } = "?";

        public string AvatarImagePath { get; set; } = string.Empty;

        public BitmapImage? AvatarPreview { get; set; }

        public FriendshipRelationshipStatus RelationshipStatus { get; set; }

        // kart ust bolgesindeki durum metni ve vurgu rengi
        public string StatusText { get; set; } = string.Empty;

        public string StatusAccent { get; set; } = "#8F98A5";

        // aksiyon butonu icin metin ve tema renkleri
        public string ActionButtonText { get; set; } = string.Empty;

        public string ActionButtonAccent { get; set; } = "#82E4B0";

        public string ActionButtonBackground { get; set; } = "#FFFFFF";

        public string ActionButtonForeground { get; set; } = "#0A0A0C";

        public bool IsActionEnabled { get; set; } = true;
    }
}
