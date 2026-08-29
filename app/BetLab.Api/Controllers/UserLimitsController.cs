using System.Security.Claims;
using BetLab.Application.DTOs;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    
    /// Limite de responsible gambling și auto-excludere.
    /// Fiecare modificare este o entry nouă în UserLimits (audit trail).
    /// Regula 24h: scăderea limitei se aplică imediat, creșterea (sau eliminarea) după 24h.
 
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserLimitsController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public UserLimitsController(BetLabDbContext context)
        {
            _context = context;
        }

        private Guid? GetUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var id) ? id : null;
        }

        private async Task<UserLimit?> GetCurrentLimitAsync(Guid userId)
        {
            return await _context.UserLimits
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private static UserLimitDto ToDto(UserLimit? l, Guid userId)
        {
            if (l == null)
            {
                return new UserLimitDto
                {
                    UserId = userId,
                    RealityCheckIntervalMinutes = 30,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            var isPermanent = l.SelfExcludedUntilUtc.HasValue
                && l.SelfExcludedUntilUtc.Value >= DateTime.UtcNow.AddYears(50);

            return new UserLimitDto
            {
                UserId = l.UserId,
                DailyDepositLimit = l.DailyDepositLimit,
                DailyLossLimit = l.DailyLossLimit,
                DailySessionMinutesLimit = l.DailySessionMinutesLimit,
                RealityCheckIntervalMinutes = l.RealityCheckIntervalMinutes,
                SelfExcludedUntilUtc = isPermanent ? null : l.SelfExcludedUntilUtc,
                IsPermanentlyExcluded = isPermanent,
                UpdatedAt = l.CreatedAt
            };
        }

        
        /// Determină dacă o tranziție de la o limită la alta înseamnă "relaxare"
        /// (creștere sau eliminare). Aplicăm cooldown 24h pentru relaxări.
        
        private static bool IsRelaxation(decimal? oldVal, decimal? newVal)
        {
            if (oldVal == null && newVal == null) return false;
            if (oldVal == null) return false;            // pune limită nouă unde nu era — strict, ok
            if (newVal == null) return true;             // elimină limită — relaxare
            return newVal.Value > oldVal.Value;
        }

        private static bool IsRelaxationInt(int? oldVal, int? newVal)
        {
            if (oldVal == null && newVal == null) return false;
            if (oldVal == null) return false;
            if (newVal == null) return true;
            return newVal.Value > oldVal.Value;
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserLimitDto>> GetMine()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var current = await GetCurrentLimitAsync(userId.Value);
            return Ok(ToDto(current, userId.Value));
        }

        [HttpPost("me")]
        public async Task<ActionResult<UserLimitDto>> SetMine([FromBody] SetUserLimitRequestDto request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var current = await GetCurrentLimitAsync(userId.Value);

            // Dacă utilizatorul e auto-exclus, nu poate modifica limite.
            if (current?.SelfExcludedUntilUtc != null && current.SelfExcludedUntilUtc > DateTime.UtcNow)
            {
                return BadRequest("Contul tău este în perioada de auto-excludere. Nu poți modifica limitele acum.");
            }

            // Validare valori
            if (request.DailyDepositLimit.HasValue && request.DailyDepositLimit.Value < 0)
                return BadRequest("Limita de depunere nu poate fi negativă.");
            if (request.DailyLossLimit.HasValue && request.DailyLossLimit.Value < 0)
                return BadRequest("Limita de pierdere nu poate fi negativă.");
            if (request.DailySessionMinutesLimit.HasValue && request.DailySessionMinutesLimit.Value < 1)
                return BadRequest("Limita de timp trebuie să fie de cel puțin 1 minut.");
            if (request.RealityCheckIntervalMinutes.HasValue &&
                (request.RealityCheckIntervalMinutes.Value < 5 || request.RealityCheckIntervalMinutes.Value > 240))
                return BadRequest("Intervalul reality check trebuie să fie între 5 și 240 minute.");

            // Regula 24h pentru creșteri/eliminări. Doar dacă există o limită anterioară setată recent.
            if (current != null)
            {
                var hoursSinceLast = (DateTime.UtcNow - current.CreatedAt).TotalHours;
                if (hoursSinceLast < 24)
                {
                    if (IsRelaxation(current.DailyDepositLimit, request.DailyDepositLimit) ||
                        IsRelaxation(current.DailyLossLimit, request.DailyLossLimit) ||
                        IsRelaxationInt(current.DailySessionMinutesLimit, request.DailySessionMinutesLimit))
                    {
                        var remaining = TimeSpan.FromHours(24 - hoursSinceLast);
                        return BadRequest(
                            $"Pentru siguranța ta, creșterea sau eliminarea unei limite se aplică după 24h. " +
                            $"Mai sunt {(int)remaining.TotalHours}h {remaining.Minutes}m până la următoarea modificare permisă.");
                    }
                }
            }

            var newLimit = new UserLimit
            {
                UserId = userId.Value,
                DailyDepositLimit = request.DailyDepositLimit ?? current?.DailyDepositLimit,
                DailyLossLimit = request.DailyLossLimit ?? current?.DailyLossLimit,
                DailySessionMinutesLimit = request.DailySessionMinutesLimit ?? current?.DailySessionMinutesLimit,
                RealityCheckIntervalMinutes = request.RealityCheckIntervalMinutes ?? current?.RealityCheckIntervalMinutes ?? 30,
                SelfExcludedUntilUtc = current?.SelfExcludedUntilUtc,
                CreatedAt = DateTime.UtcNow
            };

            // Cazul în care utilizatorul vrea explicit să elimine o limită (pasează 0 sau valoare specială).
            // Convenție: dacă request-ul a setat explicit câmpul, îl folosim ca atare (inclusiv 0 = elimină).
            // Aici păstrăm comportament conservator: dacă request.X != null, folosește X (chiar dacă e 0).
            if (request.DailyDepositLimit.HasValue) newLimit.DailyDepositLimit = request.DailyDepositLimit.Value == 0 ? null : request.DailyDepositLimit.Value;
            if (request.DailyLossLimit.HasValue) newLimit.DailyLossLimit = request.DailyLossLimit.Value == 0 ? null : request.DailyLossLimit.Value;
            if (request.DailySessionMinutesLimit.HasValue) newLimit.DailySessionMinutesLimit = request.DailySessionMinutesLimit.Value == 0 ? null : request.DailySessionMinutesLimit.Value;

            _context.UserLimits.Add(newLimit);
            await _context.SaveChangesAsync();

            return Ok(ToDto(newLimit, userId.Value));
        }

        [HttpPost("me/self-exclude")]
        public async Task<ActionResult<UserLimitDto>> SelfExclude([FromBody] SelfExcludeRequestDto request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            DateTime? until = request.Duration?.Trim().ToLowerInvariant() switch
            {
                "day1" => DateTime.UtcNow.AddDays(1),
                "day7" => DateTime.UtcNow.AddDays(7),
                "day30" => DateTime.UtcNow.AddDays(30),
                "permanent" => DateTime.UtcNow.AddYears(100),
                _ => null
            };

            if (until == null)
                return BadRequest("Durata trebuie să fie: Day1, Day7, Day30 sau Permanent.");

            var current = await GetCurrentLimitAsync(userId.Value);

            var newLimit = new UserLimit
            {
                UserId = userId.Value,
                DailyDepositLimit = current?.DailyDepositLimit,
                DailyLossLimit = current?.DailyLossLimit,
                DailySessionMinutesLimit = current?.DailySessionMinutesLimit,
                RealityCheckIntervalMinutes = current?.RealityCheckIntervalMinutes ?? 30,
                SelfExcludedUntilUtc = until.Value,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserLimits.Add(newLimit);
            await _context.SaveChangesAsync();

            return Ok(ToDto(newLimit, userId.Value));
        }

        [HttpPost("me/cancel-self-exclude")]
        public async Task<ActionResult<UserLimitDto>> CancelSelfExclude()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var current = await GetCurrentLimitAsync(userId.Value);
            if (current == null || current.SelfExcludedUntilUtc == null)
                return BadRequest("Nu există o auto-excludere activă de anulat.");

            var isPermanent = current.SelfExcludedUntilUtc.Value >= DateTime.UtcNow.AddYears(50);
            if (isPermanent)
                return BadRequest("Auto-excluderea permanentă nu poate fi anulată din aplicație. Contactează suportul.");

            var newLimit = new UserLimit
            {
                UserId = userId.Value,
                DailyDepositLimit = current.DailyDepositLimit,
                DailyLossLimit = current.DailyLossLimit,
                DailySessionMinutesLimit = current.DailySessionMinutesLimit,
                RealityCheckIntervalMinutes = current.RealityCheckIntervalMinutes,
                SelfExcludedUntilUtc = null,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserLimits.Add(newLimit);
            await _context.SaveChangesAsync();

            return Ok(ToDto(newLimit, userId.Value));
        }
    }
}