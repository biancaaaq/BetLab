namespace BetLab.EthicalWeb.Models
{
    public class CurrentUserViewModel
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}