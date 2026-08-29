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
    public class BlackjackController : ControllerBase
    {
        private readonly BetLabDbContext _context;
        private readonly Random _rng = new();

        private static readonly string[] Ranks = { "A","2","3","4","5","6","7","8","9","10","J","Q","K" };
        private static readonly string[] Suits = { "♠","♥","♦","♣" };

        public BlackjackController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpPost("start")]
        public async Task<ActionResult<BlackjackStateDto>> Start([FromBody] BlackjackStartRequestDto request)
        {
            if (request.Stake <= 0)
                return BadRequest("Miza trebuie să fie mai mare decât 0.");

            var game = await _context.CasinoGames.FirstOrDefaultAsync(g => g.Type == "Blackjack");
            if (game != null && !game.IsActive)
                return BadRequest("Blackjack este momentan indisponibil.");

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId.Value);
            if (wallet == null) return NotFound("Wallet negăsit.");
            if (wallet.Balance < request.Stake) return BadRequest("Sold insuficient.");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var balanceBefore = wallet.Balance;
                wallet.Balance -= request.Stake;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Type = "BlackjackStake",
                    Amount = request.Stake,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = wallet.Balance,
                    ReferenceType = "Blackjack",
                    ReferenceId = 0,
                    Description = "Miză blackjack"
                });

                var deck = CreateShuffledDeck();
                var playerCards = new List<int> { Draw(deck), Draw(deck) };
                var dealerCards = new List<int> { Draw(deck), Draw(deck) };

                var playerTotal = HandTotal(playerCards);
                var dealerTotal = HandTotal(dealerCards);

                var status = "Active";
                var result = (string?)null;
                var payout = 0m;

                // Blackjack natural (21 cu 2 cărți)
                if (playerTotal == 21)
                {
                    if (dealerTotal == 21)
                    {
                        status = "Finished";
                        result = "Push";
                        payout = request.Stake;
                    }
                    else
                    {
                        status = "Finished";
                        result = "Blackjack";
                        payout = decimal.Round(request.Stake * 2.5m, 2);
                    }
                }

                if (payout > 0)
                {
                    var bb = wallet.Balance;
                    wallet.Balance += payout;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        Type = "BlackjackWin",
                        Amount = payout,
                        BalanceBefore = bb,
                        BalanceAfter = wallet.Balance,
                        ReferenceType = "Blackjack",
                        ReferenceId = 0,
                        Description = $"Câștig blackjack: {result}"
                    });
                }

                var session = new BlackjackSession
                {
                    UserId = userId.Value,
                    DeckJson = JsonSerializer.Serialize(deck),
                    PlayerCardsJson = JsonSerializer.Serialize(playerCards),
                    DealerCardsJson = JsonSerializer.Serialize(dealerCards),
                    Stake = request.Stake,
                    Status = status,
                    Result = result,
                    Payout = payout,
                    NewBalance = wallet.Balance
                };

                _context.BlackjackSessions.Add(session);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(BuildState(session, playerCards, dealerCards, status == "Finished"));
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        [HttpPost("hit")]
        public async Task<ActionResult<BlackjackStateDto>> Hit([FromBody] BlackjackActionRequestDto request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var session = await _context.BlackjackSessions.FindAsync(request.SessionId);
            if (session == null || session.UserId != userId.Value)
                return NotFound();
            if (session.Status != "Active")
                return BadRequest("Sesiunea nu mai este activă.");

            var deck = JsonSerializer.Deserialize<List<int>>(session.DeckJson)!;
            var playerCards = JsonSerializer.Deserialize<List<int>>(session.PlayerCardsJson)!;
            var dealerCards = JsonSerializer.Deserialize<List<int>>(session.DealerCardsJson)!;

            playerCards.Add(Draw(deck));
            var playerTotal = HandTotal(playerCards);

            var status = "Active";
            var result = (string?)null;
            var payout = 0m;

            if (playerTotal > 21)
            {
                status = "Finished";
                result = "PlayerBust";
                payout = 0m;
            }
            else if (playerTotal == 21)
            {
                // Auto-stand la 21
                (status, result, payout) = await ResolveStand(session, deck, playerCards, dealerCards);
            }

            session.DeckJson = JsonSerializer.Serialize(deck);
            session.PlayerCardsJson = JsonSerializer.Serialize(playerCards);
            session.Status = status;
            session.Result = result;
            session.Payout = payout;
            session.UpdatedAt = DateTime.UtcNow;

            if (status == "Finished" && payout > 0)
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId.Value);
                if (wallet != null)
                {
                    var bb = wallet.Balance;
                    wallet.Balance += payout;
                    session.NewBalance = wallet.Balance;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        Type = "BlackjackWin",
                        Amount = payout,
                        BalanceBefore = bb,
                        BalanceAfter = wallet.Balance,
                        ReferenceType = "Blackjack",
                        ReferenceId = (int)session.Id,
                        Description = $"Câștig blackjack: {result}"
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(BuildState(session, playerCards, dealerCards, status == "Finished"));
        }

        [HttpPost("stand")]
        public async Task<ActionResult<BlackjackStateDto>> Stand([FromBody] BlackjackActionRequestDto request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var session = await _context.BlackjackSessions.FindAsync(request.SessionId);
            if (session == null || session.UserId != userId.Value)
                return NotFound();
            if (session.Status != "Active")
                return BadRequest("Sesiunea nu mai este activă.");

            var deck = JsonSerializer.Deserialize<List<int>>(session.DeckJson)!;
            var playerCards = JsonSerializer.Deserialize<List<int>>(session.PlayerCardsJson)!;
            var dealerCards = JsonSerializer.Deserialize<List<int>>(session.DealerCardsJson)!;

            var (status, result, payout) = await ResolveStand(session, deck, playerCards, dealerCards);

            session.DeckJson = JsonSerializer.Serialize(deck);
            session.DealerCardsJson = JsonSerializer.Serialize(dealerCards);
            session.Status = status;
            session.Result = result;
            session.Payout = payout;
            session.UpdatedAt = DateTime.UtcNow;

            if (payout > 0)
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId.Value);
                if (wallet != null)
                {
                    var bb = wallet.Balance;
                    wallet.Balance += payout;
                    session.NewBalance = wallet.Balance;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        Type = "BlackjackWin",
                        Amount = payout,
                        BalanceBefore = bb,
                        BalanceAfter = wallet.Balance,
                        ReferenceType = "Blackjack",
                        ReferenceId = (int)session.Id,
                        Description = $"Câștig blackjack: {result}"
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(BuildState(session, playerCards, dealerCards, true));
        }

        // ---------------------------------------------------------------

        private (string status, string result, decimal payout) ResolveStandSync(
            BlackjackSession session, List<int> deck, List<int> playerCards, List<int> dealerCards)
        {
            while (HandTotal(dealerCards) < 17)
                dealerCards.Add(Draw(deck));

            var pt = HandTotal(playerCards);
            var dt = HandTotal(dealerCards);

            if (dt > 21) return ("Finished", "DealerBust", session.Stake * 2);
            if (pt > dt)  return ("Finished", "PlayerWin", session.Stake * 2);
            if (dt > pt)  return ("Finished", "DealerWin", 0m);
            return ("Finished", "Push", session.Stake);
        }

        private Task<(string status, string result, decimal payout)> ResolveStand(
            BlackjackSession session, List<int> deck, List<int> playerCards, List<int> dealerCards)
            => Task.FromResult(ResolveStandSync(session, deck, playerCards, dealerCards));

        private BlackjackStateDto BuildState(BlackjackSession session,
            List<int> playerCards, List<int> dealerCards, bool finished)
        {
            var playerStrings = playerCards.Select(CardToString).ToArray();
            string[] dealerStrings;

            if (finished)
                dealerStrings = dealerCards.Select(CardToString).ToArray();
            else
                dealerStrings = new[] { CardToString(dealerCards[0]), "?" };

            var playerTotal = HandTotal(playerCards);
            var dealerTotal = finished
                ? HandTotal(dealerCards)
                : CardValue(dealerCards[0]);

            return new BlackjackStateDto
            {
                SessionId = session.Id,
                PlayerCards = playerStrings,
                DealerCards = dealerStrings,
                PlayerTotal = playerTotal,
                DealerTotal = dealerTotal,
                Status = session.Result ?? session.Status,
                IsFinished = finished,
                Stake = session.Stake,
                Payout = session.Payout,
                NewBalance = session.NewBalance
            };
        }

        private static List<int> CreateShuffledDeck()
        {
            var deck = Enumerable.Range(0, 52).ToList();
            var rng = new Random();
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return deck;
        }

        private static int Draw(List<int> deck)
        {
            var card = deck[0];
            deck.RemoveAt(0);
            return card;
        }

        private static string CardToString(int card) =>
            Ranks[card % 13] + Suits[card / 13];

        private static int CardValue(int card)
        {
            var rank = card % 13;
            if (rank == 0) return 11;
            if (rank >= 10) return 10;
            return rank + 1;
        }

        private static int HandTotal(List<int> cards)
        {
            int total = cards.Sum(CardValue);
            int aces = cards.Count(c => c % 13 == 0);
            while (total > 21 && aces > 0) { total -= 10; aces--; }
            return total;
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
