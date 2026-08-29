using BetLab.Application.DTOs;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/lookups")]
    [Authorize(Roles = "Admin")]
    public class LookupsController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public LookupsController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpGet("sports")]
        public async Task<ActionResult<IEnumerable<LookupItemDto>>> GetSports()
        {
            var result = await _context.Sports
                .OrderBy(x => x.Name)
                .Select(x => new LookupItemDto
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("competitions")]
        public async Task<ActionResult<IEnumerable<CompetitionLookupDto>>> GetCompetitions()
        {
            var result = await _context.Competitions
                .OrderBy(x => x.Name)
                .Select(x => new CompetitionLookupDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Country = x.Country,
                    SportId = x.SportId
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("teams")]
        public async Task<ActionResult<IEnumerable<LookupItemDto>>> GetTeams()
        {
            var result = await _context.Teams
                .OrderBy(x => x.Name)
                .Select(x => new LookupItemDto
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync();

            return Ok(result);
        }
    }
}