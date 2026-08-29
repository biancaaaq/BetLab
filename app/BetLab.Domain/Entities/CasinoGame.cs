using System.Collections.Generic;

namespace BetLab.Domain.Entities
{
    public class CasinoGame
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Slot";
        public bool IsActive { get; set; } = true;

        public decimal RtpPercent { get; set; } = 96.00m;

        public string Volatility { get; set; } = "Medium";

        public ICollection<CasinoRound> Rounds { get; set; } = new List<CasinoRound>();
    }
}