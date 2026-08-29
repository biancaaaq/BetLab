namespace BetLab.DarkWeb.Models
{
    public class DemoUserViewModel
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
    }
}