using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/transactions")]
    [Authorize(Roles = "Admin")]
    public class TransactionsController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public TransactionsController(BetLabDbContext context)
        {
            _context = context;
        }

       
        /// Returnează tranzacțiile wallet cu informații despre utilizator.
        /// Filtrare opțională după tip: Deposit | Withdrawal | BetPlaced | BetWon | AdminReset | InitialCredit
      
        [HttpGet]
        public async Task<ActionResult> GetTransactions(
            [FromQuery] string? type    = null,
            [FromQuery] int     page    = 1,
            [FromQuery] int     pageSize = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            page     = Math.Max(1, page);

            var query = _context.WalletTransactions
                .Include(t => t.Wallet)
                    .ThenInclude(w => w!.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.Type == type);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.Type,
                    t.Amount,
                    t.BalanceBefore,
                    t.BalanceAfter,
                    t.CreatedAt,
                    t.Description,
                    t.ReferenceType,
                    t.ReferenceId,
                    userId    = t.Wallet != null ? t.Wallet.UserId : (Guid?)null,
                    userEmail = t.Wallet != null && t.Wallet.User != null ? t.Wallet.User.Email : null,
                    userName  = t.Wallet != null && t.Wallet.User != null ? t.Wallet.User.UserName : null
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                items
            });
        }

        /// Sumar rapid: total depus, total pariuri, total câștiguri.
        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary()
        {
            var totalDeposited = await _context.WalletTransactions
                .Where(t => t.Type == "Deposit" || t.Type == "InitialCredit")
                .SumAsync(t => t.Amount);

            var totalBetPlaced = await _context.WalletTransactions
                .Where(t => t.Type == "BetPlaced")
                .SumAsync(t => t.Amount);

            var totalBetWon = await _context.WalletTransactions
                .Where(t => t.Type == "BetWon")
                .SumAsync(t => t.Amount);

            var totalCasinoPlaced = await _context.WalletTransactions
                .Where(t => t.Type == "CasinoSpin" || t.Type == "RouletteSpinDebit" || t.Type == "BlackjackStake")
                .SumAsync(t => t.Amount);

            return Ok(new
            {
                totalDeposited,
                totalBetPlaced,
                totalBetWon,
                totalCasinoPlaced,
                margin = totalBetPlaced - totalBetWon
            });
        }
    }
}
