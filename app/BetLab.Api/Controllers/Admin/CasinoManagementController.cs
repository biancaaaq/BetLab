using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/casino-games")]
    [Authorize(Roles = "Admin")]
    public class CasinoManagementController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public CasinoManagementController(BetLabDbContext context)
        {
            _context = context;
        }

        ///listează toate jocurile casino cu status.
        [HttpGet]
        public async Task<ActionResult> GetGames()
        {
            var games = await _context.CasinoGames
                .OrderBy(g => g.Name)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Type,
                    g.RtpPercent,
                    g.Volatility,
                    g.IsActive
                })
                .ToListAsync();

            return Ok(games);
        }

        /// Activează/dezactivează un joc casino.
        [HttpPut("{id:int}/toggle")]
        public async Task<ActionResult> ToggleGame(int id)
        {
            var game = await _context.CasinoGames.FindAsync(id);
            if (game == null)
                return NotFound("Joc negăsit.");

            game.IsActive = !game.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message   = game.IsActive ? "Joc activat." : "Joc dezactivat.",
                gameId    = game.Id,
                isActive  = game.IsActive
            });
        }
    }
}
