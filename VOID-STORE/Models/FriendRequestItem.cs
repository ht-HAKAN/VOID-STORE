using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public enum FriendRequestDirection
    {
        Incoming = 0,
        Outgoing = 1
    }

    public class FriendRequestItem
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string AvatarLetter { get; set; } = "?";

        public string AvatarImagePath { get; set; } = string.Empty;

        public BitmapImage? AvatarPreview { get; set; }

        public string SentAtText { get; set; } = string.Empty;

        public FriendRequestDirection Direction { get; set; }
    }
}
