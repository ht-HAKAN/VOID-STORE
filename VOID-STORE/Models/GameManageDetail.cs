namespace VOID_STORE.Models
{
    public class GameManageDetail
    {
    // secilen oyunun kimlik bilgisi
        public int GameId { get; set; }

    // secilen kaydin durum bilgisi
        public bool IsPendingNewGame { get; set; }

        public bool IsPendingDraft { get; set; }

    // ekranda gosterilecek ayrintilar
        public GameEditState CurrentState { get; set; } = new();

        public GameEditState? LiveState { get; set; }
    }
}
