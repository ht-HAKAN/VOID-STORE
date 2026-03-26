namespace VOID_STORE.Models
{
    public static class UserSession
    {
        public static string DisplayName { get; private set; } = "Misafir";

        public static string ProfileImagePath { get; private set; } = string.Empty;

        public static bool IsGuest { get; private set; } = true;

        public static void SetAuthenticated(string displayName, string profileImagePath = "")
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Oyuncu" : displayName.Trim();
            ProfileImagePath = profileImagePath?.Trim() ?? string.Empty;
            IsGuest = false;
        }

        public static void SetGuest()
        {
            DisplayName = "Misafir";
            ProfileImagePath = string.Empty;
            IsGuest = true;
        }

        public static void Clear()
        {
            SetGuest();
        }

        public static string GetAvatarLetter()
        {
            if (IsGuest)
            {
                return "?";
            }

            string source = string.IsNullOrWhiteSpace(DisplayName) ? "M" : DisplayName.Trim();
            return source.Substring(0, 1).ToUpperInvariant();
        }
    }
}
