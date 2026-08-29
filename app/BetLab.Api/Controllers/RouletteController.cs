using System.Security.Claims;
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
    public class RouletteController : ControllerBase
    {
        private readonly BetLabDbContext _context;
        private readonly Random _rng = new();

        
        private static readonly HashSet<int> RedNumbers = new()
        {
            1, 3, 5, 7, 9, 12, 14, 16, 18,
            19, 21, 23, 25, 27, 30, 32, 34, 36
        };

        public RouletteController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpPost("spin")]
        public async Task<ActionResult<RouletteSpinResponseDto>> Spin([FromBody] RouletteSpinRequestDto request)
        {
            if (request.Stake <= 0)
                return BadRequest("Miza trebuie să fie mai mare decât 0.");

            if (string.IsNullOrWhiteSpace(request.BetType) || string.IsNullOrWhiteSpace(request.BetValue))
                return BadRequest("BetType și BetValue sunt obligatorii.");

            var game = await _context.CasinoGames.FirstOrDefaultAsync(g => g.Type == "Roulette");
            if (game != null && !game.IsActive)
                return BadRequest("Rouleta este momentan indisponibilă.");

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId.Value);
            if (wallet == null) return NotFound("Wallet negăsit.");
            if (wallet.Balance < request.Stake) return BadRequest("Sold insuficient.");

            // Validare bet
            if (!IsValidBet(request.BetType, request.BetValue))
                return BadRequest("Tip de pariu invalid.");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Scade miza
                var balanceBefore = wallet.Balance;
                wallet.Balance -= request.Stake;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Type = "RouletteSpinPlaced",
                    Amount = request.Stake,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = wallet.Balance,
                    ReferenceType = "Roulette",
                    ReferenceId = 0,
                    Description = $"Pariu ruletă: {request.BetType} / {request.BetValue}"
                });

                // Rulează roulette
                var resultNumber = _rng.Next(0, 37); // 0-36
                var resultColor = GetColor(resultNumber);
                var (isWin, multiplier) = EvaluateBet(request.BetType, request.BetValue, resultNumber);
                var payout = isWin ? decimal.Round(request.Stake * multiplier, 2) : 0m;

                if (payout > 0)
                {
                    var balanceBeforeCredit = wallet.Balance;
                    wallet.Balance += payout;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        Type = "RouletteWin",
                        Amount = payout,
                        BalanceBefore = balanceBeforeCredit,
                        BalanceAfter = wallet.Balance,
                        ReferenceType = "Roulette",
                        ReferenceId = 0,
                        Description = $"Câștig ruletă: {resultNumber} {resultColor}"
                    });
                }

                var round = new RouletteRound
                {
                    UserId = userId.Value,
                    BetType = request.BetType,
                    BetValue = request.BetValue,
                    Stake = request.Stake,
                    ResultNumber = resultNumber,
                    ResultColor = resultColor,
                    IsWin = isWin,
                    Payout = payout,
                    NewBalance = wallet.Balance,
                    CreatedAt = DateTime.UtcNow
                };

                _context.RouletteRounds.Add(round);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new RouletteSpinResponseDto
                {
                    RouletteRoundId = round.Id,
                    BetType = round.BetType,
                    BetValue = round.BetValue,
                    Stake = round.Stake,
                    ResultNumber = round.ResultNumber,
                    ResultColor = round.ResultColor,
                    IsWin = round.IsWin,
                    Payout = round.Payout,
                    NewBalance = round.NewBalance,
                    CreatedAt = round.CreatedAt
                });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        [HttpGet("history/me")]
        public async Task<ActionResult<IEnumerable<RouletteHistoryItemDto>>> GetMyHistory()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var rounds = await _context.RouletteRounds
                .Where(r => r.UserId == userId.Value)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .Select(r => new RouletteHistoryItemDto
                {
                    RouletteRoundId = r.Id,
                    BetType = r.BetType,
                    BetValue = r.BetValue,
                    Stake = r.Stake,
                    ResultNumber = r.ResultNumber,
                    ResultColor = r.ResultColor,
                    IsWin = r.IsWin,
                    Payout = r.Payout,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(rounds);
        }

  

        private static string GetColor(int number)
        {
            if (number == 0) return "Green";
            return RedNumbers.Contains(number) ? "Red" : "Black";
        }

        private static bool IsValidBet(string betType, string betValue)
        {
            return betType switch
            {
                "RedBlack" => betValue == "Red" || betValue == "Black",
                "OddEven"  => betValue == "Odd" || betValue == "Even",
                "LowHigh"  => betValue == "Low" || betValue == "High",
                "Dozen"    => betValue == "1" || betValue == "2" || betValue == "3",
                "Column"   => betValue == "1" || betValue == "2" || betValue == "3",
                "Straight" => int.TryParse(betValue, out var n) && n >= 0 && n <= 36,
                _ => false
            };
        }

        // Returnează (isWin, multiplier) — multiplier include miza înapoi
        private static (bool isWin, decimal multiplier) EvaluateBet(string betType, string betValue, int number)
        {
            return betType switch
            {
                "RedBlack" => betValue == "Red"
                    ? (RedNumbers.Contains(number), 2m)
                    : (!RedNumbers.Contains(number) && number != 0, 2m),

                "OddEven" => betValue == "Odd"
                    ? (number != 0 && number % 2 == 1, 2m)
                    : (number != 0 && number % 2 == 0, 2m),

                "LowHigh" => betValue == "Low"
                    ? (number >= 1 && number <= 18, 2m)
                    : (number >= 19 && number <= 36, 2m),

                "Dozen" => betValue switch
                {
                    "1" => (number >= 1  && number <= 12, 3m),
                    "2" => (number >= 13 && number <= 24, 3m),
                    "3" => (number >= 25 && number <= 36, 3m),
                    _   => (false, 0m)
                },

                "Column" => betValue switch
                {
                    "1" => (number != 0 && number % 3 == 1, 3m),
                    "2" => (number != 0 && number % 3 == 2, 3m),
                    "3" => (number != 0 && number % 3 == 0, 3m),
                    _   => (false, 0m)
                },

                "Straight" => int.TryParse(betValue, out var target)
                    ? (number == target, 36m)
                    : (false, 0m),

                _ => (false, 0m)
            };
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
