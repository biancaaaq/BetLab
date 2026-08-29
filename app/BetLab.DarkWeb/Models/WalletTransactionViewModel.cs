namespace BetLab.DarkWeb.Models
{
    public class WalletTransactionViewModel
    {
        public int TransactionId { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}