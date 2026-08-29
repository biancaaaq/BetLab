namespace BetLab.EthicalWeb.Models
{
    public class AuthResponseViewModel
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}