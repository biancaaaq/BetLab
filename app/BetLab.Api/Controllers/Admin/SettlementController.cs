using BetLab.Application.DTOs;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace BetLab.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/settlement")]
    [Authorize(Roles = "Admin")]
    public class SettlementController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public SettlementController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpPost("mark-winning-outcome")]
        public async Task<ActionResult> MarkWinningOutcome([FromBody] MarkWinningOutcomeRequestDto request)
        {
            var outcome = await _context.Outcomes
                .Include(o => o.Market)
                .FirstOrDefaultAsync(o => o.Id == request.OutcomeId);

            if (outcome == null)
                return NotFound("Outcome not found.");

            if (outcome.Market == null)
                return BadRequest("Outcome has no market.");

            var allMarketOutcomes = await _context.Outcomes
                .Where(o => o.MarketId == outcome.MarketId)
                .ToListAsync();

            foreach (var item in allMarketOutcomes)
            {
                item.IsWinning = item.Id == request.OutcomeId;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Winning outcome marked successfully.",
                marketId = outcome.MarketId,
                winningOutcomeId = outcome.Id
            });
        }

        [HttpPost("event/{eventId:int}")]
        public async Task<ActionResult> SettleEvent(int eventId)
        {
            var eventEntity = await _context.Events
                .Include(e => e.Markets)
                    .ThenInclude(m => m.Outcomes)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null)
                return NotFound("Event not found.");

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
                        var selectedOutcome = await _context.Outcomes
                            .FirstOrDefaultAsync(o => o.Id == selection.OutcomeId);

                        if (selectedOutcome == null)
                            continue;

                        selection.ResultStatus = selectedOutcome.IsWinning ? "Won" : "Lost";
                    }

                    var allSelections = await _context.BetSelections
                        .Where(s => s.BetSlipId == betSlip.Id)
                        .ToListAsync();

                    if (allSelections.All(s => s.ResultStatus == "Won"))
                    {
                        betSlip.Status = "Won";
                        betSlip.ActualPayout = betSlip.PotentialPayout;
                        betSlip.SettledAt = DateTime.UtcNow;

                        var wallet = await _context.Wallets
                            .FirstOrDefaultAsync(w => w.UserId == betSlip.UserId);

                        if (wallet != null)
                        {
                            var balanceBefore = wallet.Balance;
                            wallet.Balance += betSlip.PotentialPayout;
                            var balanceAfter = wallet.Balance;

                            _context.WalletTransactions.Add(new WalletTransaction
                            {
                                WalletId = wallet.Id,
                                Type = "BetWon",
                                Amount = betSlip.PotentialPayout,
                                BalanceBefore = balanceBefore,
                                BalanceAfter = balanceAfter,
                                ReferenceType = "BetSlip",
                                ReferenceId = betSlip.Id,
                                Description = $"Settlement payout for bet slip {betSlip.Id}"
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

                return Ok(new
                {
                    message = "Event settled successfully.",
                    eventId = eventId,
                    settledBetCount = relatedBetSlips.Count
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}