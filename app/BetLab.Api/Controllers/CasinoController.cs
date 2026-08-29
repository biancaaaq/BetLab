using System.Security.Claims;
using System.Text.Json;
using BetLab.Application.DTOs;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CasinoController : ControllerBase
    {
        private readonly BetLabDbContext _context;
        private readonly Random _random = new();

        public CasinoController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpGet("games")]
        public async Task<ActionResult<IEnumerable<CasinoGameDto>>> GetGames()
        {
            var games = await _context.CasinoGames
                .OrderBy(g => g.Name)
                .Select(g => new CasinoGameDto
                {
                    CasinoGameId = g.Id,
                    Name = g.Name,
                    Type = g.Type,
                    IsActive = g.IsActive,
                    RtpPercent = g.RtpPercent,
                    Volatility = g.Volatility
                })
                .ToListAsync();

            return Ok(games);
        }

        [HttpPost("spin")]
        public async Task<ActionResult<CasinoSpinResponseDto>> Spin([FromBody] CasinoSpinRequestDto request)
        {
            if (request.Stake <= 0)
                return BadRequest("Stake must be greater than 0.");

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var game = await _context.CasinoGames.FirstOrDefaultAsync(g => g.Id == request.CasinoGameId);
            if (game == null)
                return NotFound("Casino game not found.");

            if (!game.IsActive)
                return BadRequest("Casino game is inactive.");

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId.Value);
            if (wallet == null)
                return NotFound("Wallet not found.");

            if (wallet.Balance < request.Stake)
                return BadRequest("Insufficient balance.");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var balanceBeforeDebit = wallet.Balance;
                wallet.Balance -= request.Stake;
                var balanceAfterDebit = wallet.Balance;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Type = "CasinoSpinPlaced",
                    Amount = request.Stake,
                    BalanceBefore = balanceBeforeDebit,
                    BalanceAfter = balanceAfterDebit,
                    ReferenceType = "CasinoGame",
                    ReferenceId = game.Id,
                    Description = $"Casino spin placed for game {game.Name}"
                });

                var (grid, multiplier, resultType) = GenerateThreeRowFiveReelSpinResult();
                var payout = decimal.Round(request.Stake * multiplier, 2, MidpointRounding.AwayFromZero);

                if (payout > 0)
                {
                    var balanceBeforeCredit = wallet.Balance;
                    wallet.Balance += payout;
                    var balanceAfterCredit = wallet.Balance;

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        Type = "CasinoWin",
                        Amount = payout,
                        BalanceBefore = balanceBeforeCredit,
                        BalanceAfter = balanceAfterCredit,
                        ReferenceType = "CasinoGame",
                        ReferenceId = game.Id,
                        Description = $"Casino payout for game {game.Name}"
                    });
                }

                var round = new CasinoRound
                {
                    UserId = userId.Value,
                    CasinoGameId = game.Id,
                    CasinoSessionId = request.CasinoSessionId,
                    Stake = request.Stake,
                    Multiplier = multiplier,
                    Payout = payout,
                    ResultType = resultType,
                    SymbolsJson = JsonSerializer.Serialize(grid),
                    CreatedAt = DateTime.UtcNow
                };

                _context.CasinoRounds.Add(round);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new CasinoSpinResponseDto
                {
                    CasinoRoundId = round.Id,
                    CasinoGameId = round.CasinoGameId,
                    Stake = round.Stake,
                    Multiplier = round.Multiplier,
                    Payout = round.Payout,
                    ResultType = round.ResultType,
                    SymbolsJson = round.SymbolsJson,
                    NewBalance = wallet.Balance,
                    CreatedAt = round.CreatedAt
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpGet("history/me")]
        public async Task<ActionResult<IEnumerable<CasinoRoundHistoryDto>>> GetMyHistory()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var rounds = await _context.CasinoRounds
                .Include(r => r.CasinoGame)
                .Where(r => r.UserId == userId.Value)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new CasinoRoundHistoryDto
                {
                    CasinoRoundId = r.Id,
                    GameName = r.CasinoGame != null ? r.CasinoGame.Name : "",
                    Stake = r.Stake,
                    Multiplier = r.Multiplier,
                    Payout = r.Payout,
                    ResultType = r.ResultType,
                    SymbolsJson = r.SymbolsJson,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(rounds);
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return null;

            return Guid.Parse(userIdClaim);
        }

        private (string[][] Grid, decimal Multiplier, string ResultType) GenerateThreeRowFiveReelSpinResult()
        {
            var availableSymbols = new[] { "Cherry", "Seven", "Bell", "Star", "Diamond", "Clover", "Crown" };

            string RandomSymbol() => availableSymbols[_random.Next(availableSymbols.Length)];

            var grid = new[]
            {
                new[] { RandomSymbol(), RandomSymbol(), RandomSymbol(), RandomSymbol(), RandomSymbol() },
                new[] { RandomSymbol(), RandomSymbol(), RandomSymbol(), RandomSymbol(), RandomSymbol() },
                new[] { RandomSymbol(), RandomSymbol(), RandomSymbol(), RandomSymbol(), RandomSymbol() }
            };

            var payline = grid[1]; // doar linia din mijloc plătește

            var grouped = payline
                .GroupBy(x => x)
                .Select(g => g.Count())
                .OrderByDescending(x => x)
                .ToList();

            var maxCount = grouped.First();

            decimal multiplier;
            string resultType;

            if (maxCount == 5)
            {
                multiplier = 12m;
                resultType = "BigWin";
            }
            else if (maxCount == 4)
            {
                multiplier = 6m;
                resultType = "Win";
            }
            else if (maxCount == 3)
            {
                multiplier = 2m;
                resultType = "SmallWin";
            }
            else
            {
                multiplier = 0m;
                resultType = "Loss";
            }

            return (grid, multiplier, resultType);
        }
    }
}