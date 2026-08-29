namespace BetLab.EthicalWeb.Models
{
    public class CashOutPreviewViewModel
    {
        public bool    Available          { get; set; }
        public decimal CashOutValue       { get; set; }
        public decimal PotentialPayout    { get; set; }
        public decimal Stake              { get; set; }
        public decimal MarginPct          { get; set; }
        public bool    IsPreMatch         { get; set; }
        public int     LiveSelections     { get; set; }
        public int     LockedSelections   { get; set; }
        public int     PreMatchSelections { get; set; }
        public string? UnavailableReason  { get; set; }
    }
}
