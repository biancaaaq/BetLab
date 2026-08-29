using BetLab.Api.Services;
using BetLab.Application.DTOs;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BetsController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public BetsController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpPost("place")]
        public async Task<ActionResult<PlaceBetResponseDto>> PlaceBet([FromBody] PlaceBetRequestDto request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (request.UserId == Guid.Empty)
                return BadRequest("UserId is required.");

            if (request.Stake <= 0)
                return BadRequest("Stake must be greater than 0.");

            if (request.Selections == null || request.Selections.Count == 0)
                return BadRequest("At least one selection is required.");

            var distinctOutcomeIds = request.Selections
                .Select(s => s.OutcomeId)
                .Distinct()
                .ToList();

            if (distinctOutcomeIds.Count != request.Selections.Count)
                return BadRequest("Duplicate selections are not allowed.");

            var user = await _context.Users
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.Id == request.UserId);

            if (user == null)
                return NotFound("User not found.");

            if (user.Wallet == null)
                return BadRequest("User wallet not found.");

            if (user.Wallet.Balance < request.Stake)
                return BadRequest("Insufficient balance.");

            var outcomes = await _context.Outcomes
                .Include(o => o.Market)
                    .ThenInclude(m => m!.Event)
                        .ThenInclude(e => e!.HomeTeam)
                .Include(o => o.Market)
                    .ThenInclude(m => m!.Event)
                        .ThenInclude(e => e!.AwayTeam)
                .Where(o => distinctOutcomeIds.Contains(o.Id))
                .ToListAsync();

            if (outcomes.Count != distinctOutcomeIds.Count)
                return BadRequest("One or more outcomes do not exist.");

            foreach (var outcome in outcomes)
            {
                // Outcome trebuie să fie deschis (nu Closed/Suspended)
                if (outcome.Status != "Open")
                    return BadRequest($"Cotă suspendată — nu mai poate fi pariată.");

                if (outcome.Market == null)
                    return BadRequest($"Outcome {outcome.Id} has no market.");

                // Piața trebuie deschisă (nu Closed)
                if (outcome.Market.Status != "Open")
                    return BadRequest($"Piață închisă — nu mai poate fi pariată.");

                if (outcome.Market.Event == null)
                    return BadRequest($"Market {outcome.Market.Id} has no event.");

                // Permitem pariere atât pre-meci (Open) cât și LIVE (Live).
                // Doar Finished/Cancelled/Postponed sunt blocate.
                var evStatus = outcome.Market.Event.Status;
                if (evStatus != "Open" && evStatus != "Live")
                    return BadRequest($"Meciul nu mai acceptă pariuri (status: {evStatus}).");
            }

            // Permis: mai multe selecții din acelasi eveniment, dar pe piețe diferite
            // (Bet Builder / Same Game Multi — standard la bookmakerii reali).
            // Blocăm doar dacă două selecții sunt în aceeași piață
            var samesMarket = outcomes
                .GroupBy(o => o.Market!.Id)
                .Any(g => g.Count() > 1);

            if (samesMarket)
                return BadRequest("Nu poți avea două selecții din aceeași piață. Alege doar una per piață.");

            decimal totalOdds = 1m;
            foreach (var outcome in outcomes)
            {
                totalOdds *= outcome.Odds;
            }

            totalOdds = decimal.Round(totalOdds, 2, MidpointRounding.AwayFromZero);
            var potentialPayout = decimal.Round(request.Stake * totalOdds, 2, MidpointRounding.AwayFromZero);

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var balanceBefore = user.Wallet.Balance;
                var balanceAfter = balanceBefore - request.Stake;

                var betSlip = new BetSlip
                {
                    UserId = user.Id,
                    Stake = request.Stake,
                    TotalOdds = totalOdds,
                    PotentialPayout = potentialPayout,
                    Status = "Open",
                    Type = outcomes.Count == 1 ? "Single" : "Multiple",
                    PlacedAt = DateTime.UtcNow
                };

                _context.BetSlips.Add(betSlip);
                await _context.SaveChangesAsync();

                var betSelections = outcomes.Select(outcome => new BetSelection
                {
                    BetSlipId = betSlip.Id,
                    EventId = outcome.Market!.EventId,
                    MarketId = outcome.MarketId,
                    OutcomeId = outcome.Id,
                    SelectedOdds = outcome.Odds,
                    PickName = outcome.Name,
                    ResultStatus = "Pending"
                }).ToList();

                _context.BetSelections.AddRange(betSelections);

                user.Wallet.Balance = balanceAfter;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = user.Wallet.Id,
                    Type = "BetPlaced",
                    Amount = request.Stake,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    ReferenceType = "BetSlip",
                    ReferenceId = betSlip.Id,
                    Description = $"Bet placed with {betSelections.Count} selection(s)"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new PlaceBetResponseDto
                {
                    BetSlipId = betSlip.Id,
                    UserId = user.Id,
                    Stake = betSlip.Stake,
                    TotalOdds = betSlip.TotalOdds,
                    PotentialPayout = betSlip.PotentialPayout,
                    NewBalance = balanceAfter,
                    Status = betSlip.Status,
                    Selections = outcomes.Select(outcome => new PlacedBetSelectionDto
                    {
                        EventId = outcome.Market!.EventId,
                        EventName = $"{outcome.Market.Event!.HomeTeam!.Name} vs {outcome.Market.Event.AwayTeam!.Name}",
                        MarketId = outcome.MarketId,
                        MarketName = outcome.Market.Name,
                        OutcomeId = outcome.Id,
                        OutcomeName = outcome.Name,
                        Odds = outcome.Odds
                    }).ToList()
                };

                return Ok(response);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
      
        /// Preview Cash Out — calculează cât ar primi user-ul fără să încheie biletul.
        /// Folosit de UI pentru a afișa suma actuală pe buton (refresh dinamic).
       
        [HttpGet("{id:int}/cashout-preview")]
        public async Task<ActionResult> CashOutPreview(int id, [FromQuery] Guid userId, [FromQuery] string? variant = null)
        {
            if (userId == Guid.Empty)
                return BadRequest("UserId is required.");

            var betSlip = await LoadBetSlipForCashOutAsync(id, userId);
            if (betSlip == null)
                return NotFound("Biletul nu există.");

            var result = CashOutCalculator.Calculate(betSlip, variant);
            return Ok(result);
        }

      
        /// Cash Out anticipat — folosește formula bookmakerilor reali:
        /// CashOut = (Stake × OriginalOdds / CurrentEstimatedOdds) × (1 - margin)
        ///
        /// Marja variază în funcție de variant:
        ///   - "Ethical" → 5% (standardul industriei)
        ///   - "Dark"    → 30% (predatoriu)
        [HttpPost("{id:int}/cashout")]
        public async Task<ActionResult> CashOut(int id, [FromQuery] Guid userId, [FromQuery] string? variant = null)
        {
            if (userId == Guid.Empty)
                return BadRequest("UserId is required.");

            var betSlip = await LoadBetSlipForCashOutAsync(id, userId);
            if (betSlip == null)
                return NotFound("Biletul nu există sau nu mai este activ.");

            var result = CashOutCalculator.Calculate(betSlip, variant);
            if (!result.Available)
                return BadRequest(new { error = result.UnavailableReason ?? "Cash Out indisponibil." });

            var cashoutValue = result.CashOutValue;

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
                return BadRequest("Portofel negăsit.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var before = wallet.Balance;
                wallet.Balance += cashoutValue;
                betSlip.Status       = "CashedOut";
                betSlip.ActualPayout = cashoutValue;
                betSlip.SettledAt    = DateTime.UtcNow;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId      = wallet.Id,
                    Type          = "CashOut",
                    Amount        = cashoutValue,
                    BalanceBefore = before,
                    BalanceAfter  = wallet.Balance,
                    ReferenceType = "BetSlip",
                    ReferenceId   = betSlip.Id,
                    Description   = $"Cash Out anticipat bilet #{id} (marjă {result.MarginPct:0.#}%)"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    cashoutValue,
                    newBalance      = wallet.Balance,
                    potentialPayout = result.PotentialPayout,
                    marginPct       = result.MarginPct,
                    isPreMatch      = result.IsPreMatch
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<BetSlip?> LoadBetSlipForCashOutAsync(int id, Guid userId)
        {
            return await _context.BetSlips
                .Include(b => b.Selections)
                    .ThenInclude(s => s.Outcome)
                        .ThenInclude(o => o!.Market)
                .Include(b => b.Selections)
                    .ThenInclude(s => s.Event)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId && b.Status == "Open");
        }

        ///Actualizează scorul live al unui eveniment (apelat din admin/sync).
        [HttpPost("{id:int}/score")]
        public async Task<ActionResult> UpdateScore(int id, [FromBody] UpdateScoreRequest req)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();
            ev.HomeScore = req.HomeScore;
            ev.AwayScore = req.AwayScore;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("user/{userId:guid}")]
        public async Task<ActionResult<IEnumerable<BetHistoryItemDto>>> GetUserBets(Guid userId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return NotFound("User not found.");

            var bets = await _context.BetSlips
                .Include(b => b.Selections)
                    .ThenInclude(s => s.Event)
                        .ThenInclude(e => e!.HomeTeam)
                .Include(b => b.Selections)
                    .ThenInclude(s => s.Event)
                        .ThenInclude(e => e!.AwayTeam)
                .Include(b => b.Selections)
                    .ThenInclude(s => s.Market)
                .Include(b => b.Selections)
                    .ThenInclude(s => s.Outcome)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.PlacedAt)
                .ToListAsync();

            var result = bets.Select(b => new BetHistoryItemDto
            {
                BetSlipId = b.Id,
                Stake = b.Stake,
                TotalOdds = b.TotalOdds,
                PotentialPayout = b.PotentialPayout,
                Status = b.Status,
                Type = b.Type,
                PlacedAt = b.PlacedAt,
                ActualPayout = b.ActualPayout,
                Selections = b.Selections.Select(s => new PlacedBetSelectionDto
                {
                    EventId = s.EventId,
                    EventName = $"{s.Event!.HomeTeam!.Name} vs {s.Event.AwayTeam!.Name}",
                    MarketId = s.MarketId,
                    MarketName = s.Market?.Name ?? "",
                    OutcomeId = s.OutcomeId,
                    OutcomeName = s.Outcome?.Name ?? s.PickName,
                    Odds = s.SelectedOdds,
                    EventIsLive = s.Event.IsLive,
                    EventStatus = s.Event.Status
                }).ToList()
            }).ToList();

            return Ok(result);
        }
    }
}