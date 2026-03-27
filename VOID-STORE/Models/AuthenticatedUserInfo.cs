namespace VOID_STORE.Models
{
    public class AuthenticatedUserInfo
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public string ProfileImagePath { get; set; } = string.Empty;
    }
}
