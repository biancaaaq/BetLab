using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class Wallet
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public decimal Balance { get; set; }
        public string Currency { get; set; } = "RON";

        public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
    }
}
