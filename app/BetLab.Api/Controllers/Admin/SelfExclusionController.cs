using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/self-exclusion")]
    [Authorize(Roles = "Admin")]
    public class SelfExclusionController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public SelfExclusionController(BetLabDbContext context)
        {
            _context = context;
        }

        /// Returnează toate excluderile (active și inactive).
        [HttpGet]
        public async Task<ActionResult> GetExclusions()
        {
            var exclusions = await _context.UserExclusions
                .Include(e => e.User)
                .OrderByDescending(e => e.ExcludedAt)
                .Select(e => new
                {
                    e.Id,
                    userId       = e.UserId,
                    userEmail    = e.User != null ? e.User.Email : null,
                    userName     = e.User != null ? e.User.UserName : null,
                    e.ExcludedAt,
                    e.ExcludedUntil,
                    e.Reason,
                    e.ImposedBy,
                    e.IsActive,
                    e.LiftedAt,
                    e.LiftedByAdminEmail
                })
                .ToListAsync();

            return Ok(exclusions);
        }

        /// Impune o excludere unui utilizator.
        [HttpPost("impose")]
        public async Task<ActionResult> ImposeExclusion([FromBody] ImposeExclusionRequest request)
        {
            if (request.UserId == Guid.Empty)
                return BadRequest("UserId este obligatoriu.");

            // Dezactivează excluderile active existente pentru același user
            var existing = await _context.UserExclusions
                .Where(e => e.UserId == request.UserId && e.IsActive)
                .ToListAsync();

            foreach (var ex in existing)
                ex.IsActive = false;

            var adminEmail = User.Identity?.Name ?? "admin";

            var exclusion = new UserExclusion
            {
                UserId        = request.UserId,
                ExcludedAt    = DateTime.UtcNow,
                ExcludedUntil = request.PermanentExclusion ? null : request.ExcludedUntil,
                Reason        = request.Reason ?? string.Empty,
                ImposedBy     = "Admin",
                IsActive      = true
            };

            _context.UserExclusions.Add(exclusion);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Excludere impusă cu succes.", exclusionId = exclusion.Id });
        }

        /// Ridică o excludere activă.
        [HttpPost("{id:long}/lift")]
        public async Task<ActionResult> LiftExclusion(long id)
        {
            var exclusion = await _context.UserExclusions.FindAsync(id);
            if (exclusion == null)
                return NotFound("Excludere negăsită.");

            if (!exclusion.IsActive)
                return BadRequest("Excluderea nu este activă.");

            exclusion.IsActive          = false;
            exclusion.LiftedAt          = DateTime.UtcNow;
            exclusion.LiftedByAdminEmail = User.Identity?.Name ?? "admin";

            await _context.SaveChangesAsync();

            return Ok(new { message = "Excludere ridicată cu succes." });
        }
    }

    public class ImposeExclusionRequest
    {
        public Guid UserId { get; set; }
        public string? Reason { get; set; }
        public bool PermanentExclusion { get; set; } = false;
        public DateTime? ExcludedUntil { get; set; }
    }
}
