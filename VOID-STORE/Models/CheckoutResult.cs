namespace VOID_STORE.Models
{
    public class CheckoutResult
    {
        public int ItemCount { get; set; }

        public decimal TotalAmount { get; set; }

        public string TotalText { get; set; } = string.Empty;

        public decimal BalanceAfter { get; set; }

        public string BalanceAfterText { get; set; } = string.Empty;
    }
}
