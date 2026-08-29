namespace BetLab.EthicalWeb.Models
{
    public class CasinoGameViewModel
    {
        public int CasinoGameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal RtpPercent { get; set; }
        public string Volatility { get; set; } = string.Empty;
    }
}