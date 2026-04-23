namespace VOID_STORE.Models
{
    // iki kullanici arasindaki arkadaslik durumunu tanimlar
    public enum FriendshipRelationshipStatus
    {
        None = 0,
        Self = 1,
        PendingSent = 2,
        PendingReceived = 3,
        Friends = 4
    }
}
