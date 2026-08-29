using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class WalletTransaction
    {
        public int Id { get; set; }

        public int WalletId { get; set; }
        public Wallet? Wallet { get; set; }

        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
    }
}
