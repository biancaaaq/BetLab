namespace BetLab.Application.DTOs
{
    public class RouletteSpinRequestDto
    {
        public string BetType { get; set; } = string.Empty;
        public string BetValue { get; set; } = string.Empty;
        public decimal Stake { get; set; }
    }
}
