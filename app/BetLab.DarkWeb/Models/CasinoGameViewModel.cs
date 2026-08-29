namespace BetLab.DarkWeb.Models
{
    public class CasinoGameViewModel
    {
        public int CasinoGameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}