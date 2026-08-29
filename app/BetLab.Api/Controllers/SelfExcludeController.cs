using System.Security.Claims;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
  
    /// Endpoint pentru auto-excludere inițiată de utilizator.Cererea se aprobă automat și este vizibilă în panoul admin.
   
    [ApiController]
    [Route("api/me/self-exclude")]
    [Authorize]
    public class SelfExcludeController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public SelfExcludeController(BetLabDbContext context)
        {
            _context = context;
        }

      
        /// Utilizatorul solicită auto-excluderea. Cererea este aprobată imediat.
      
        [HttpPost]
        public async Task<ActionResult> RequestSelfExclusion([FromBody] SelfExcludeRequest request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized();

            var userId = Guid.Parse(userIdClaim);

            // Dezactivează eventuale excluderi active existente ale acestui user
            var existing = await _context.UserExclusions
                .Where(e => e.UserId == userId && e.IsActive)
                .ToListAsync();

            foreach (var ex in existing)
                ex.IsActive = false;

            DateTime? excludedUntil = request.Duration?.ToLowerInvariant() switch
            {
                "1month"   => DateTime.UtcNow.AddMonths(1),
                "3months"  => DateTime.UtcNow.AddMonths(3),
                "6months"  => DateTime.UtcNow.AddMonths(6),
                "1year"    => DateTime.UtcNow.AddYears(1),
                "permanent" or _ => null
            };

            var exclusion = new UserExclusion
            {
                UserId        = userId,
                ExcludedAt    = DateTime.UtcNow,
                ExcludedUntil = excludedUntil,
                Reason        = request.Reason ?? "Auto-excludere solicitată de utilizator",
                ImposedBy     = "User",
                IsActive      = true
            };

            _context.UserExclusions.Add(exclusion);
            await _context.SaveChangesAsync();

            // Adăugăm CNP-ul în Registrul de Autoexcludere (simulare ONJN)
            // Orice cont nou cu același CNP va fi blocat la înregistrare.
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (!string.IsNullOrWhiteSpace(appUser?.Cnp))
            {
                var cnpAlreadyRegistered = await _context.CnpExclusions
                    .AnyAsync(e => e.Cnp == appUser.Cnp);

                if (!cnpAlreadyRegistered)
                {
                    _context.CnpExclusions.Add(new CnpExclusionEntry
                    {
                        Cnp        = appUser.Cnp,
                        ExcludedAt = DateTime.UtcNow,
                        UserId     = userId,
                        Reason     = "SelfExclusion"
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new
            {
                message       = "Auto-excludere activată. Vei fi contactat dacă dorești să ridici excluderea.",
                excludedUntil = excludedUntil?.ToString("yyyy-MM-dd"),
                permanent     = excludedUntil == null
            });
        }

        /// Returnează starea excluderii active a utilizatorului curent.
        [HttpGet]
        public async Task<ActionResult> GetMyExclusion()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim)) return Unauthorized();

            var userId = Guid.Parse(userIdClaim);

            var exclusion = await _context.UserExclusions
                .Where(e => e.UserId == userId && e.IsActive)
                .OrderByDescending(e => e.ExcludedAt)
                .FirstOrDefaultAsync();

            if (exclusion == null)
                return Ok(new { isExcluded = false });

            return Ok(new
            {
                isExcluded    = true,
                excludedAt    = exclusion.ExcludedAt,
                excludedUntil = exclusion.ExcludedUntil,
                permanent     = exclusion.ExcludedUntil == null,
                reason        = exclusion.Reason
            });
        }
    }

    public class SelfExcludeRequest
    {
        /// <summary>"1month" | "3months" | "6months" | "1year" | "permanent"</summary>
        public string? Duration { get; set; }
        public string? Reason   { get; set; }
    }
}
