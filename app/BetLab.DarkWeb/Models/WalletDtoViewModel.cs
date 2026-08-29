namespace BetLab.DarkWeb.Models
{
    public class WalletDtoViewModel
    {
        public int WalletId { get; set; }
        public Guid UserId { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
    }
}