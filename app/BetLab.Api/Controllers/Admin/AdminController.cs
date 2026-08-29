using BetLab.Api.Services;
using BetLab.Application.DTOs;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace BetLab.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly BetLabDbContext _context;
        private readonly MarketGeneratorService _marketGen;
        private readonly ApiFootballImportService _apiFootballImport;

        public AdminController(BetLabDbContext context,
                               MarketGeneratorService marketGen,
                               ApiFootballImportService apiFootballImport)
        {
            _context           = context;
            _marketGen         = marketGen;
            _apiFootballImport = apiFootballImport;
        }

        [HttpPost("events")]
        public async Task<ActionResult> CreateEvent([FromBody] CreateEventRequestDto request)
        {
            if (request.HomeTeamId == request.AwayTeamId)
                return BadRequest("HomeTeamId and AwayTeamId must be different.");

            var sportExists = await _context.Sports.AnyAsync(x => x.Id == request.SportId);
            var competitionExists = await _context.Competitions.AnyAsync(x => x.Id == request.CompetitionId);
            var homeTeamExists = await _context.Teams.AnyAsync(x => x.Id == request.HomeTeamId);
            var awayTeamExists = await _context.Teams.AnyAsync(x => x.Id == request.AwayTeamId);

            if (!sportExists) return BadRequest("Sport not found.");
            if (!competitionExists) return BadRequest("Competition not found.");
            if (!homeTeamExists) return BadRequest("Home team not found.");
            if (!awayTeamExists) return BadRequest("Away team not found.");

            var entity = new Event
            {
                SportId = request.SportId,
                CompetitionId = request.CompetitionId,
                HomeTeamId = request.HomeTeamId,
                AwayTeamId = request.AwayTeamId,
                StartTimeUtc = request.StartTimeUtc,
                Status = request.Status,
                IsLive = request.IsLive
            };

            _context.Events.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Event created successfully.",
                eventId = entity.Id
            });
        }

        [HttpPost("markets")]
        public async Task<ActionResult> CreateMarket([FromBody] CreateMarketRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name is required.");

            if (string.IsNullOrWhiteSpace(request.MarketType))
                return BadRequest("MarketType is required.");

            var eventExists = await _context.Events.AnyAsync(x => x.Id == request.EventId);
            if (!eventExists)
                return BadRequest("Event not found.");

            var entity = new Market
            {
                EventId = request.EventId,
                Name = request.Name,
                MarketType = request.MarketType,
                Status = request.Status,
                IsMain = request.IsMain
            };

            _context.Markets.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Market created successfully.",
                marketId = entity.Id
            });
        }

        [HttpPost("outcomes")]
        public async Task<ActionResult> CreateOutcome([FromBody] CreateOutcomeRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name is required.");

            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest("Code is required.");

            if (request.Odds <= 1)
                return BadRequest("Odds must be greater than 1.");

            var marketExists = await _context.Markets.AnyAsync(x => x.Id == request.MarketId);
            if (!marketExists)
                return BadRequest("Market not found.");

            var entity = new Outcome
            {
                MarketId = request.MarketId,
                Name = request.Name,
                Code = request.Code,
                Odds = request.Odds,
                Status = request.Status,
                IsWinning = false
            };

            _context.Outcomes.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Outcome created successfully.",
                outcomeId = entity.Id
            });
        }

        [HttpPut("outcomes/{id:int}/odds")]
        public async Task<ActionResult> UpdateOutcomeOdds(int id, [FromBody] UpdateOutcomeOddsRequestDto request)
        {
            if (request.NewOdds <= 1)
                return BadRequest("NewOdds must be greater than 1.");

            var outcome = await _context.Outcomes.FirstOrDefaultAsync(x => x.Id == id);
            if (outcome == null)
                return NotFound("Outcome not found.");

            var oldOdds = outcome.Odds;
            outcome.Odds = request.NewOdds;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Outcome odds updated successfully.",
                outcomeId = outcome.Id,
                oldOdds,
                newOdds = outcome.Odds
            });
        }

        [HttpPut("events/{id:int}/status")]
        public async Task<ActionResult> UpdateEventStatus(int id, [FromBody] UpdateEventStatusRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Status))
                return BadRequest("Status is required.");

            var eventEntity = await _context.Events.FirstOrDefaultAsync(x => x.Id == id);
            if (eventEntity == null)
                return NotFound("Event not found.");

            eventEntity.Status = request.Status;

            if (request.IsLive.HasValue)
            {
                eventEntity.IsLive = request.IsLive.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Event status updated successfully.",
                eventId = eventEntity.Id,
                status = eventEntity.Status,
                isLive = eventEntity.IsLive
            });
        }

        /// <summary>Statistici generale pentru dashboard admin.</summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult> GetDashboard()
        {
            var totalUsers    = await _context.Users.CountAsync();
            var activeUsers   = await _context.Users.CountAsync(u => u.IsActive);
            var blockedUsers  = totalUsers - activeUsers;
            var activeEvents  = await _context.Events.CountAsync(e => e.Status == "Open" || e.Status == "Live");
            var openBets      = await _context.BetSlips.CountAsync(b => b.Status == "Open");
            var wonBets       = await _context.BetSlips.CountAsync(b => b.Status == "Won");
            var lostBets      = await _context.BetSlips.CountAsync(b => b.Status == "Lost");
            var activeExclusions = await _context.UserExclusions.CountAsync(e => e.IsActive);
            var todayCasinoRounds = await _context.CasinoRounds
                .CountAsync(r => r.CreatedAt >= DateTime.UtcNow.Date);

            return Ok(new
            {
                totalUsers,
                activeUsers,
                blockedUsers,
                activeEvents,
                openBets,
                wonBets,
                lostBets,
                activeExclusions,
                todayCasinoRounds
            });
        }

        /// <summary>Listează toate evenimentele pentru panoul admin (fără filtru de timp).</summary>
        [HttpGet("events")]
        public async Task<ActionResult> GetAdminEvents()
        {
            var events = await _context.Events
                .Include(e => e.HomeTeam)
                .Include(e => e.AwayTeam)
                .Include(e => e.Competition)
                .Include(e => e.Markets)
                    .ThenInclude(m => m.Outcomes)
                .OrderByDescending(e => e.StartTimeUtc)
                .ToListAsync();

            var betCounts = await _context.BetSelections
                .GroupBy(s => s.EventId)
                .Select(g => new { EventId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EventId, x => x.Count);

            var result = events.Select(e => new
            {
                e.Id,
                homeTeam     = e.HomeTeam?.Name ?? "",
                awayTeam     = e.AwayTeam?.Name ?? "",
                competition  = e.Competition?.Name ?? "",
                e.StartTimeUtc,
                e.Status,
                e.IsLive,
                betCount     = betCounts.GetValueOrDefault(e.Id, 0),
                markets      = e.Markets.Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.MarketType,
                    m.Status,
                    outcomes = m.Outcomes.Select(o => new
                    {
                        o.Id,
                        o.Name,
                        o.Code,
                        o.Odds,
                        o.IsWinning,
                        o.Status
                    })
                })
            });

            return Ok(result);
        }

        
        /// Import meciuri din API-FOOTBALL (unicul provider).
      
        [HttpPost("import-apifootball")]
        public async Task<ActionResult> ImportApiFootball(
            [FromQuery] int leagueId,
            [FromQuery] int season,
            [FromQuery] string name,
            [FromQuery] string country = "Europe",
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] bool onlyUpcoming = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { error = "Parametrul 'name' e obligatoriu." });

            try
            {
                var result = await _apiFootballImport.ImportFixturesAsync(
                    leagueId, season, name, country, dateFrom, dateTo, onlyUpcoming);
                return Ok(new { message = $"Import API-FOOTBALL: {result}" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// Importă un singur fixture după ID-ul API-FOOTBALL.
        /// Util când știi exact ce meci vrei.
      
        [HttpPost("import-apifootball-fixture/{fixtureId:int}")]
        public async Task<ActionResult> ImportApiFootballFixture(int fixtureId)
        {
            try
            {
                var result = await _apiFootballImport.ImportSingleFixtureByIdAsync(fixtureId);
                return Ok(new { message = $"Fixture {fixtureId}: {result}" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        
        /// Sincronizează scorul + statusul tuturor meciurilor importate din API-FOOTBALL.
        /// Folosește pentru live updates (scorul în timp real, status Live/Finished).
       
        [HttpPost("sync-live-apifootball")]
        public async Task<ActionResult> SyncLiveApiFootball()
        {
            try
            {
                var result = await _apiFootballImport.SyncLiveFixturesAsync();
                return Ok(new
                {
                    message = result.Message,
                    markedLive = result.MarkedLive,
                    markedFinished = result.MarkedFinished,
                    errors = result.Errors
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

      
        /// Sincronizează statusul + scorul evenimentelor cu API-FOOTBALL.
        /// Marchează LIVE / Finished, salvează scorul curent și determină câștigătorul din scorul final.
        /// Dacă autoSettle=true, decontează automat meciurile finalizate.
    
        [HttpPost("sync-events")]
        public async Task<ActionResult> SyncEvents([FromQuery] bool autoSettle = false)
        {
            var report = await _apiFootballImport.SyncLiveFixturesAsync();

            int settled = 0;
            if (autoSettle && report.MarkedFinished > 0)
            {
                // Găsim meciurile cu câștigător determinat dar nedecontate încă
                var candidates = await _context.Events
                    .Where(e => e.Status == "Finished"
                                && e.Markets.Any(m => m.MarketType == "1X2"
                                                       && m.Outcomes.Any(o => o.IsWinning)))
                    .Select(e => e.Id)
                    .ToListAsync();

                foreach (var eventId in candidates)
                {
                    try { await SettleEventInternal(eventId); settled++; }
                    catch { /* continuăm cu restul */ }
                }
                report.Message += $" Auto-decontate: {settled} evenimente.";
            }

            return Ok(new
            {
                report.Message,
                report.MarkedLive,
                report.MarkedFinished,
                autoSettled = settled
            });
        }

     
        /// Generează matematic piețele adiționale (Dublă șansă, BTTS, Over/Under,
        /// Scor exact, HT/FT) pentru un eveniment care are deja 1X2.
        
        [HttpPost("events/{id:int}/generate-markets")]
        public async Task<ActionResult> GenerateMarketsForEvent(int id)
        {
            var created = await _marketGen.GenerateMarketsForEventAsync(id);
            return Ok(new { eventId = id, marketsCreated = created });
        }

      
        /// Aplică generatorul de piețe pentru toate evenimentele viitoare
        /// care încă nu au piețe adiționale.
       
        [HttpPost("events/generate-all-markets")]
        public async Task<ActionResult> GenerateAllMarkets()
        {
            var (processed, total) = await _marketGen.GenerateForAllOpenEventsAsync();
            return Ok(new
            {
                eventsProcessed = processed,
                marketsCreated  = total,
                message         = $"Procesate {processed} evenimente, create {total} piețe noi."
            });
        }

      
        /// Decontează toate biletele deschise pentru evenimentele deja Finished.
        /// Util când meciul era deja marcat Finished înainte de a apela sync-events?autoSettle=true.
       
        [HttpPost("settle-pending")]
        public async Task<ActionResult> SettlePending()
        {
            var candidates = await _context.Events
                .Where(e => e.Status == "Finished"
                            && e.Markets.Any(m => m.MarketType == "1X2"
                                                   && m.Outcomes.Any(o => o.IsWinning))
                            && _context.BetSelections.Any(s => s.EventId == e.Id
                                && _context.BetSlips.Any(b => b.Id == s.BetSlipId && b.Status == "Open")))
                .Select(e => e.Id)
                .ToListAsync();

            int settled = 0;
            var errors = new List<string>();

            foreach (var eventId in candidates)
            {
                try { await SettleEventInternal(eventId); settled++; }
                catch (Exception ex) { errors.Add($"Event {eventId}: {ex.Message}"); }
            }

            return Ok(new
            {
                eventsProcessed = candidates.Count,
                eventsSettled   = settled,
                errors,
                message = $"Decontate {settled}/{candidates.Count} evenimente."
            });
        }

        private async Task SettleEventInternal(int eventId)
        {
            var eventEntity = await _context.Events
                .Include(e => e.Markets).ThenInclude(m => m.Outcomes)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null) return;

            var relatedBetSlips = await _context.BetSlips
                .Include(b => b.Selections)
                .Where(b => b.Status == "Open" && b.Selections.Any(s => s.EventId == eventId))
                .ToListAsync();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var betSlip in relatedBetSlips)
                {
                    foreach (var selection in betSlip.Selections.Where(s => s.EventId == eventId))
                    {
                        var outcome = await _context.Outcomes.FindAsync(selection.OutcomeId);
                        if (outcome == null) continue;
                        selection.ResultStatus = outcome.IsWinning ? "Won" : "Lost";
                    }

                    var allSelections = await _context.BetSelections
                        .Where(s => s.BetSlipId == betSlip.Id).ToListAsync();

                    if (allSelections.All(s => s.ResultStatus == "Won"))
                    {
                        betSlip.Status = "Won";
                        betSlip.ActualPayout = betSlip.PotentialPayout;
                        betSlip.SettledAt = DateTime.UtcNow;

                        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == betSlip.UserId);
                        if (wallet != null)
                        {
                            var before = wallet.Balance;
                            wallet.Balance += betSlip.PotentialPayout;
                            _context.WalletTransactions.Add(new BetLab.Domain.Entities.WalletTransaction
                            {
                                WalletId = wallet.Id,
                                Type = "BetWon",
                                Amount = betSlip.PotentialPayout,
                                BalanceBefore = before,
                                BalanceAfter = wallet.Balance,
                                ReferenceType = "BetSlip",
                                ReferenceId = betSlip.Id,
                                Description = $"Auto-settlement for event {eventId}"
                            });
                        }
                    }
                    else if (allSelections.Any(s => s.ResultStatus == "Lost"))
                    {
                        betSlip.Status = "Lost";
                        betSlip.ActualPayout = 0;
                        betSlip.SettledAt = DateTime.UtcNow;
                    }
                }

                eventEntity.Status = "Finished";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

       
        /// Returnează toate CNP-urile din Registrul de Autoexcludere (simulare ONJN).
        /// Utilizat în panoul admin pentru audit și supraveghere.
        
        [HttpGet("cnp-exclusions")]
        public async Task<ActionResult> GetCnpExclusions()
        {
            var entries = await _context.CnpExclusions
                .OrderByDescending(e => e.ExcludedAt)
                .Select(e => new
                {
                    e.Id,
                    Cnp        = "***" + e.Cnp.Substring(10),   // mascare parțială
                    CnpFull    = e.Cnp,                          // complet pentru admin
                    e.ExcludedAt,
                    e.Reason,
                    // Unim cu userul curent care are acel CNP (dacă există)
                    UserName = _context.Users
                        .Where(u => u.Cnp == e.Cnp)
                        .Select(u => u.UserName)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(entries);
        }

       
        /// Ridică un CNP din Registrul de Autoexcludere (acțiune admin justificată).
        
        [HttpDelete("cnp-exclusions/{id:long}")]
        public async Task<ActionResult> RemoveCnpExclusion(long id)
        {
            var entry = await _context.CnpExclusions.FindAsync(id);
            if (entry == null)
                return NotFound("Înregistrare inexistentă.");

            _context.CnpExclusions.Remove(entry);
            await _context.SaveChangesAsync();

            return Ok(new { message = "CNP eliminat din registru." });
        }
    }
}