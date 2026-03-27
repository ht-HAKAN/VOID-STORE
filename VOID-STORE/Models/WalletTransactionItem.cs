namespace VOID_STORE.Models
{
    public class WalletTransactionItem
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string AmountText { get; set; } = string.Empty;

        public string CreatedAtText { get; set; } = string.Empty;

        public bool IsPositive { get; set; }
    }
}
