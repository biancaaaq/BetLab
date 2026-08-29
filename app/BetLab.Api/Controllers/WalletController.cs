using System.Security.Claims;
using BetLab.Application.DTOs;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public WalletController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        public async Task<ActionResult<WalletDto>> GetMyWallet()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId.Value);

            if (wallet == null)
                return NotFound("Wallet not found.");

            return Ok(new WalletDto
            {
                WalletId = wallet.Id,
                UserId = wallet.UserId,
                Balance = wallet.Balance,
                Currency = wallet.Currency
            });
        }

        [HttpGet("transactions")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetMyTransactions()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var wallet = await _context.Wallets
                .Include(w => w.Transactions)
                .FirstOrDefaultAsync(w => w.UserId == userId.Value);

            if (wallet == null)
                return NotFound("Wallet not found.");

            var result = wallet.Transactions
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new WalletTransactionDto
                {
                    TransactionId = t.Id,
                    Type = t.Type,
                    Amount = t.Amount,
                    BalanceBefore = t.BalanceBefore,
                    BalanceAfter = t.BalanceAfter,
                    ReferenceType = t.ReferenceType,
                    ReferenceId = t.ReferenceId,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt
                })
                .ToList();

            return Ok(result);
        }

        [HttpPost("topup-demo")]
        public async Task<ActionResult<WalletDto>> TopUpDemo([FromBody] TopUpDemoRequestDto request)
        {
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than 0.");

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId.Value);

            if (wallet == null)
                return NotFound("Wallet not found.");

            var balanceBefore = wallet.Balance;
            wallet.Balance += request.Amount;
            var balanceAfter = wallet.Balance;

            _context.WalletTransactions.Add(new Domain.Entities.WalletTransaction
            {
                WalletId = wallet.Id,
                Type = "DemoTopUp",
                Amount = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                Description = "Demo wallet top-up"
            });

            await _context.SaveChangesAsync();

            return Ok(new WalletDto
            {
                WalletId = wallet.Id,
                UserId = wallet.UserId,
                Balance = wallet.Balance,
                Currency = wallet.Currency
            });
        }

        [HttpPost("reset-demo")]
        public async Task<ActionResult<WalletDto>> ResetDemo([FromBody] ResetDemoWalletRequestDto request)
        {
            if (request.NewBalance < 0)
                return BadRequest("NewBalance must be >= 0.");

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId.Value);

            if (wallet == null)
                return NotFound("Wallet not found.");

            var balanceBefore = wallet.Balance;
            wallet.Balance = request.NewBalance;
            var balanceAfter = wallet.Balance;

            _context.WalletTransactions.Add(new Domain.Entities.WalletTransaction
            {
                WalletId = wallet.Id,
                Type = "DemoReset",
                Amount = balanceAfter - balanceBefore,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                Description = "Demo wallet reset"
            });

            await _context.SaveChangesAsync();

            return Ok(new WalletDto
            {
                WalletId = wallet.Id,
                UserId = wallet.UserId,
                Balance = wallet.Balance,
                Currency = wallet.Currency
            });
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return null;

            return Guid.Parse(userIdClaim);
        }
    }
}