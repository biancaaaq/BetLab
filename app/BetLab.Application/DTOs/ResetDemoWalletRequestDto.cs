using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class ResetDemoWalletRequestDto
    {
        public decimal NewBalance { get; set; } = 1000m;
    }
}