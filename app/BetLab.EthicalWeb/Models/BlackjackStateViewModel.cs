namespace BetLab.EthicalWeb.Models
{
    public class BlackjackStateViewModel
    {
        public long SessionId { get; set; }
        public string[] PlayerCards { get; set; } = [];
        public string[] DealerCards { get; set; } = [];
        public int PlayerTotal { get; set; }
        public int DealerTotal { get; set; }
        public string Status { get; set; } = "Active";
        public bool IsFinished { get; set; }
        public decimal Stake { get; set; }
        public decimal Payout { get; set; }
        public decimal NewBalance { get; set; }
    }
}
