using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "Admin")]
    public class UserManagementController : ControllerBase
    {
        private readonly BetLabDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public UserManagementController(BetLabDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// Returnează toți utilizatorii cu sold și status.
        [HttpGet]
        public async Task<ActionResult> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.Wallet)
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();

            var exclusionsList = await _context.UserExclusions
                .Where(e => e.IsActive)
                .Select(e => e.UserId)
                .ToListAsync();
            var exclusions = exclusionsList.ToHashSet();

            var result = new List<object>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                result.Add(new
                {
                    userId   = u.Id,
                    email    = u.Email ?? "",
                    userName = u.UserName ?? "",
                    isActive = u.IsActive,
                    isExcluded = exclusions.Contains(u.Id),
                    balance  = u.Wallet?.Balance ?? 0m,
                    currency = u.Wallet?.Currency ?? "RON",
                    createdAt = u.CreatedAt,
                    roles
                });
            }

            return Ok(result);
        }

        /// Resetează soldul unui utilizator la 100 RON (demo reset).
        [HttpPost("{userId:guid}/reset-balance")]
        public async Task<ActionResult> ResetBalance(Guid userId)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
                return NotFound("Wallet not found.");

            var balanceBefore = wallet.Balance;
            wallet.Balance = 100m;

            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId     = wallet.Id,
                Type         = "AdminReset",
                Amount       = 100m,
                BalanceBefore = balanceBefore,
                BalanceAfter = 100m,
                Description  = "Balance reset by admin"
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Balance reset to 100 RON.", newBalance = 100m });
        }

        ///Blochează un utilizator (IsActive = false)
        [HttpPost("{userId:guid}/block")]
        public async Task<ActionResult> BlockUser(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound("User not found.");

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
                return BadRequest("Nu poți bloca un administrator.");

            user.IsActive = false;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors.Select(e => e.Description));

            return Ok(new { message = "User blocked.", userId });
        }

        /// Deblochează un utilizator (IsActive = true).
        [HttpPost("{userId:guid}/unblock")]
        public async Task<ActionResult> UnblockUser(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound("User not found.");

            user.IsActive = true;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors.Select(e => e.Description));

            return Ok(new { message = "User unblocked.", userId });
        }
    }
}
