using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class WalletDto
    {
        public int WalletId { get; set; }
        public Guid UserId { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
    }
}
