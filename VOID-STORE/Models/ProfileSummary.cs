using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class ProfileSummary
    {
        // oturum ve profil kimligini tasir
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Bio { get; set; } = string.Empty;

        // secilen medya yollarini saklar
        public string ProfileImagePath { get; set; } = string.Empty;

        public string BannerImagePath { get; set; } = string.Empty;

        // ekranda kullanilan hazir gorseli tutar
        public BitmapImage? AvatarPreview { get; set; }

        public BitmapImage? BannerPreview { get; set; }

        // ust bolumde gosterilen sayaclari tutar
        public int OwnedGameCount { get; set; }

        public int WishlistCount { get; set; }

        public int TotalPlaySeconds { get; set; }

        public int FriendsCount { get; set; }

        // baska profilin goruntulendigi durumda mevcut durumunu tasir
        public FriendshipRelationshipStatus ViewerRelationship { get; set; } = FriendshipRelationshipStatus.Self;
    }
}
