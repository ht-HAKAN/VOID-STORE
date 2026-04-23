using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class FriendListItem
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Bio { get; set; } = string.Empty;

        public string AvatarLetter { get; set; } = "?";

        public string AvatarImagePath { get; set; } = string.Empty;

        public BitmapImage? AvatarPreview { get; set; }

        public string FriendsSinceText { get; set; } = string.Empty;
    }
}
