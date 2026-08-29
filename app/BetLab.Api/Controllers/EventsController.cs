using BetLab.Api.Services;
using BetLab.Application.DTOs;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly BetLabDbContext _context;
        private readonly ApiFootballImportService _analytics;

        public EventsController(BetLabDbContext context, ApiFootballImportService analytics)
        {
            _context   = context;
            _analytics = analytics;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventListItemDto>>> GetAll()
        {
            var cutoff = DateTime.UtcNow.AddHours(-3);
            var events = await _context.Events
                .Include(e => e.Sport)
                .Include(e => e.Competition)
                .Include(e => e.HomeTeam)
                .Include(e => e.AwayTeam)
                .Where(e => e.Status != "Finished" && e.StartTimeUtc > cutoff)
                .OrderBy(e => e.StartTimeUtc)
                .Select(e => new EventListItemDto
                {
                    EventId = e.Id,
                    SportName = e.Sport != null ? e.Sport.Name : "",
                    CompetitionName = e.Competition != null ? e.Competition.Name : "",
                    HomeTeam = e.HomeTeam != null ? e.HomeTeam.Name : "",
                    AwayTeam = e.AwayTeam != null ? e.AwayTeam.Name : "",
                    StartTimeUtc    = e.StartTimeUtc,
                    Status          = e.Status,
                    IsLive          = e.IsLive,
                    MarketsCount    = e.Markets.Count(m => m.Status == "Open"),
                    HomeScore       = e.HomeScore,
                    AwayScore       = e.AwayScore,
                    CurrentMinute   = e.CurrentMinute
                })
                .ToListAsync();

            return Ok(events);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<EventDetailsDto>> GetById(int id)
        {
            var eventEntity = await _context.Events
                .Include(e => e.Sport)
                .Include(e => e.Competition)
                .Include(e => e.HomeTeam)
                .Include(e => e.AwayTeam)
                .Include(e => e.Markets)
                    .ThenInclude(m => m.Outcomes)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eventEntity == null)
                return NotFound();

            var result = new EventDetailsDto
            {
                EventId = eventEntity.Id,
                SportName = eventEntity.Sport?.Name ?? "",
                CompetitionName = eventEntity.Competition?.Name ?? "",
                HomeTeam = eventEntity.HomeTeam?.Name ?? "",
                AwayTeam = eventEntity.AwayTeam?.Name ?? "",
                StartTimeUtc = eventEntity.StartTimeUtc,
                Status = eventEntity.Status,
                IsLive = eventEntity.IsLive,
                HomeScore = eventEntity.HomeScore,
                AwayScore = eventEntity.AwayScore,
                CurrentMinute = eventEntity.CurrentMinute,
                Markets = eventEntity.Markets.Select(m => new MarketDto
                {
                    MarketId = m.Id,
                    Name = m.Name,
                    MarketType = m.MarketType,
                    Status = m.Status,
                    IsMain = m.IsMain,
                    Outcomes = m.Outcomes.Select(o => new OutcomeDto
                    {
                        OutcomeId = o.Id,
                        Name = o.Name,
                        Code = o.Code,
                        Odds = o.Odds,
                        Status = o.Status
                    }).ToList()
                }).ToList()
            };

            return Ok(result);
        }

        /// Statistici reale (H2H + formă) preluate de la football-data.org.
        [HttpGet("{id:int}/analytics")]
        public async Task<ActionResult> GetAnalytics(int id)
        {
            var data = await _analytics.GetAnalyticsAsync(id);
            if (data == null) return NotFound();
            return Ok(data);
        }

    
        /// Popularitate cote: pentru fiecare outcome al meciului, % din selecțiile existente pe acel eveniment care l-au ales.
        /// Util pentru "X% au ales acest pariu".
       
        [HttpGet("{id:int}/popularity")]
        public async Task<ActionResult> GetPopularity(int id)
        {
            // Numără selecțiile per outcome pentru acest event
            var selections = await _context.BetSelections
                .Where(s => s.EventId == id)
                .GroupBy(s => s.OutcomeId)
                .Select(g => new { OutcomeId = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalSelections = selections.Sum(s => s.Count);

            var data = selections.Select(s => new
            {
                outcomeId  = s.OutcomeId,
                count      = s.Count,
                percentage = totalSelections > 0
                    ? Math.Round((double)s.Count / totalSelections * 100, 1)
                    : 0.0
            });

            return Ok(new
            {
                totalSelections,
                outcomes = data
            });
        }
    }
}