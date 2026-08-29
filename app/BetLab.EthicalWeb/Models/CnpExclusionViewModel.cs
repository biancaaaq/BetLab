namespace BetLab.EthicalWeb.Models
{
    public class CnpExclusionViewModel
    {
        public long    Id          { get; set; }
        public string  CnpMasked   { get; set; } = string.Empty;
        public string? UserName    { get; set; }
        public DateTime ExcludedAt { get; set; }
        public string? Reason      { get; set; }
    }
}
