namespace VOID_STORE.Models
{
    public static class UserSession
    {
        public static int UserId { get; private set; }

        public static string Username { get; private set; } = "Misafir";

        public static string DisplayName { get; private set; } = "Misafir";

        public static string ProfileImagePath { get; private set; } = string.Empty;

        public static decimal Balance { get; private set; }

        public static bool IsGuest { get; private set; } = true;

        public static void SetAuthenticated(int userId, string username, decimal balance, string profileImagePath = "")
        {
            UserId = userId;
            Username = string.IsNullOrWhiteSpace(username) ? "Oyuncu" : username.Trim();
            DisplayName = Username;
            ProfileImagePath = profileImagePath?.Trim() ?? string.Empty;
            Balance = balance;
            IsGuest = false;
        }

        public static void SetGuest()
        {
            UserId = 0;
            Username = "Misafir";
            DisplayName = "Misafir";
            ProfileImagePath = string.Empty;
            Balance = 0;
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

        public static void UpdateBalance(decimal balance)
        {
            Balance = balance < 0 ? 0 : balance;
        }
    }
}
