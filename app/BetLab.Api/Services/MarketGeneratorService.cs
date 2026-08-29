using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Services
{
    
    /// Generează matematic markets adiționale pornind de la cotele 1X2 existente. Folosește
    /// distribuție Poisson pentru goluri.
    
    public class MarketGeneratorService
    {
        private readonly BetLabDbContext _context;

        // Marja casei pentru fiecare tip de piață (Superbet are ~5-12% după piață)
        private const double Margin1X2          = 1.05;
        private const double MarginDoubleChance = 1.05;
        private const double MarginBtts         = 1.06;
        private const double MarginOverUnder    = 1.06;
        private const double MarginCorrectScore = 1.18;
        private const double MarginHtFt         = 1.20;

        public MarketGeneratorService(BetLabDbContext context) => _context = context;

       
        /// Generează markets adiționale pentru un singur eveniment.
        /// Returnează numărul de piețe noi create.
        
        public async Task<int> GenerateMarketsForEventAsync(int eventId)
        {
            var ev = await _context.Events
                .Include(e => e.Markets).ThenInclude(m => m.Outcomes)
                .Include(e => e.HomeTeam)
                .Include(e => e.AwayTeam)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (ev == null) return 0;

            var mainMarket = ev.Markets.FirstOrDefault(m => m.MarketType == "1X2");
            if (mainMarket == null) return 0;

            var home = mainMarket.Outcomes.FirstOrDefault(o => o.Code == "1");
            var draw = mainMarket.Outcomes.FirstOrDefault(o => o.Code == "X");
            var away = mainMarket.Outcomes.FirstOrDefault(o => o.Code == "2");
            if (home == null || draw == null || away == null) return 0;

            // Probabilități "fair" — scoatem marja casei normalizând la 1.0
            double pHome = 1.0 / (double)home.Odds;
            double pDraw = 1.0 / (double)draw.Odds;
            double pAway = 1.0 / (double)away.Odds;
            double total = pHome + pDraw + pAway;
            pHome /= total; pDraw /= total; pAway /= total;

            // Estimează expected goals (xG) pentru fiecare echipă
            // - dacă meciul e echilibrat (probabilități apropiate) → mai multe goluri
            // - dacă există favorit clar → diferență mare de putere ofensivă
            double favStrength    = Math.Max(pHome, pAway);
            double lambdaTotal    = Math.Clamp(2.7 - (favStrength - 0.33) * 1.2, 1.8, 3.4);
            double homeAttack     = Math.Sqrt(pHome / Math.Max(pAway, 0.05));
            double awayAttack     = 1.0 / homeAttack;
            double sumAttack      = homeAttack + awayAttack;
            double lambdaHome     = lambdaTotal * (homeAttack / sumAttack);
            double lambdaAway     = lambdaTotal * (awayAttack / sumAttack);

            string homeName = ev.HomeTeam?.Name ?? "Acasă";
            string awayName = ev.AwayTeam?.Name ?? "Deplasare";
            int created     = 0;

          //sansa dubla
            if (!ev.Markets.Any(m => m.MarketType == "DoubleChance"))
            {
                var dc = new Market
                {
                    EventId    = eventId,
                    Name       = "Dublă șansă",
                    MarketType = "DoubleChance",
                    Status     = "Open",
                    IsMain     = false
                };
                _context.Markets.Add(dc);
                await _context.SaveChangesAsync();

                _context.Outcomes.AddRange(new[]
                {
                    new Outcome { MarketId = dc.Id, Name = $"{homeName} sau Egal",
                                  Code = "1X", Odds = ProbToOdds((pHome + pDraw) * MarginDoubleChance) },
                    new Outcome { MarketId = dc.Id, Name = $"{homeName} sau {awayName}",
                                  Code = "12", Odds = ProbToOdds((pHome + pAway) * MarginDoubleChance) },
                    new Outcome { MarketId = dc.Id, Name = $"Egal sau {awayName}",
                                  Code = "X2", Odds = ProbToOdds((pDraw + pAway) * MarginDoubleChance) },
                });
                created++;
            }

            // Ambele echipe marchează 
            if (!ev.Markets.Any(m => m.MarketType == "BTTS"))
            {
                double pBttsYes = (1 - Math.Exp(-lambdaHome)) * (1 - Math.Exp(-lambdaAway));
                double pBttsNo  = 1 - pBttsYes;

                var btts = new Market
                {
                    EventId    = eventId,
                    Name       = "Ambele echipe marchează",
                    MarketType = "BTTS",
                    Status     = "Open",
                    IsMain     = false
                };
                _context.Markets.Add(btts);
                await _context.SaveChangesAsync();

                _context.Outcomes.AddRange(new[]
                {
                    new Outcome { MarketId = btts.Id, Name = "Da (GG)", Code = "GG",
                                  Odds = ProbToOdds(pBttsYes * MarginBtts) },
                    new Outcome { MarketId = btts.Id, Name = "Nu (NG)", Code = "NG",
                                  Odds = ProbToOdds(pBttsNo  * MarginBtts) },
                });
                created++;
            }

            // over/under pentru linii multiple
            foreach (var line in new[] { 0.5, 1.5, 2.5, 3.5, 4.5 })
            {
                string mt = $"OU_{line.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_')}";
                if (ev.Markets.Any(m => m.MarketType == mt)) continue;

                double pOver  = ProbTotalOver(lambdaHome + lambdaAway, line);
                double pUnder = 1 - pOver;

                var ou = new Market
                {
                    EventId    = eventId,
                    Name       = $"Total goluri {line:0.0}",
                    MarketType = mt,
                    Status     = "Open",
                    IsMain     = false
                };
                _context.Markets.Add(ou);
                await _context.SaveChangesAsync();

                _context.Outcomes.AddRange(new[]
                {
                    new Outcome { MarketId = ou.Id, Name = $"Peste {line:0.0}", Code = $"O{line:0.0}",
                                  Odds = ProbToOdds(pOver  * MarginOverUnder) },
                    new Outcome { MarketId = ou.Id, Name = $"Sub {line:0.0}",   Code = $"U{line:0.0}",
                                  Odds = ProbToOdds(pUnder * MarginOverUnder) },
                });
                created++;
            }

            //Scor exact (top 10 cele mai probabile)
            if (!ev.Markets.Any(m => m.MarketType == "CorrectScore"))
            {
                var cs = new Market
                {
                    EventId    = eventId,
                    Name       = "Scor exact",
                    MarketType = "CorrectScore",
                    Status     = "Open",
                    IsMain     = false
                };
                _context.Markets.Add(cs);
                await _context.SaveChangesAsync();

                var scores = new List<(int h, int a, double p)>();
                for (int h2 = 0; h2 <= 5; h2++)
                    for (int a2 = 0; a2 <= 5; a2++)
                        scores.Add((h2, a2, Poisson(h2, lambdaHome) * Poisson(a2, lambdaAway)));

                var top = scores.OrderByDescending(s => s.p).Take(10).ToList();
                _context.Outcomes.AddRange(top.Select(s => new Outcome
                {
                    MarketId = cs.Id,
                    Name     = $"{s.h} - {s.a}",
                    Code     = $"CS_{s.h}_{s.a}",
                    Odds     = ProbToOdds(s.p * MarginCorrectScore)
                }));
                created++;
            }

            //5. Pauză / Final 
            if (!ev.Markets.Any(m => m.MarketType == "HtFt"))
            {
                // Prima repriză = ~40% din goluri totale (statistică reală fotbal)
                double htLambdaHome = lambdaHome * 0.42;
                double htLambdaAway = lambdaAway * 0.42;

                // Probabilități HT
                double[] htProbs = new double[3]; // [home, draw, away]
                for (int h3 = 0; h3 <= 4; h3++)
                    for (int a3 = 0; a3 <= 4; a3++)
                    {
                        double p = Poisson(h3, htLambdaHome) * Poisson(a3, htLambdaAway);
                        if      (h3 > a3) htProbs[0] += p;
                        else if (h3 < a3) htProbs[2] += p;
                        else              htProbs[1] += p;
                    }

                // 9 combinații HT/FT — folosim probabilitățile fair de pe 1X2 ca FT
                double[] ftProbs = { pHome, pDraw, pAway };
                var labels   = new[] { "1", "X", "2" };

                var htft = new Market
                {
                    EventId    = eventId,
                    Name       = "Pauză / Final",
                    MarketType = "HtFt",
                    Status     = "Open",
                    IsMain     = false
                };
                _context.Markets.Add(htft);
                await _context.SaveChangesAsync();

                var outcomes = new List<Outcome>();
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        // Combinație simplistă — independență aproximativă
                        double pComb = htProbs[i] * ftProbs[j];
                        outcomes.Add(new Outcome
                        {
                            MarketId = htft.Id,
                            Name     = $"{labels[i]} / {labels[j]}",
                            Code     = $"HTFT_{labels[i]}_{labels[j]}",
                            Odds     = ProbToOdds(pComb * MarginHtFt)
                        });
                    }
                _context.Outcomes.AddRange(outcomes);
                created++;
            }

            await _context.SaveChangesAsync();
            return created;
        }

        
        /// Aplică generatorul pentru toate evenimentele Open care încă au doar 1X2.
       
        public async Task<(int eventsProcessed, int marketsCreated)> GenerateForAllOpenEventsAsync()
        {
            var events = await _context.Events
                .Include(e => e.Markets)
                .Where(e => (e.Status == "Open" || e.Status == "Scheduled") && e.StartTimeUtc > DateTime.UtcNow)
                .ToListAsync();

            int processed = 0;
            int total     = 0;

            foreach (var ev in events)
            {
                // Doar dacă are 1X2 dar nu și altele
                if (ev.Markets.Any(m => m.MarketType == "1X2") &&
                    ev.Markets.Count(m => m.MarketType != "1X2") == 0)
                {
                    var n = await GenerateMarketsForEventAsync(ev.Id);
                    if (n > 0) { processed++; total += n; }
                }
            }

            return (processed, total);
        }

        
        private static decimal ProbToOdds(double prob)
        {
            prob = Math.Clamp(prob, 0.01, 0.99);
            double odds = 1.0 / prob;
            return Math.Max(1.01m, decimal.Round((decimal)odds, 2));
        }

        private static double Poisson(int k, double lambda)
        {
            if (lambda <= 0) return k == 0 ? 1 : 0;
            return Math.Exp(-lambda + k * Math.Log(lambda) - LogFactorial(k));
        }

        private static double LogFactorial(int n)
        {
            double s = 0;
            for (int i = 2; i <= n; i++) s += Math.Log(i);
            return s;
        }

        private static double ProbTotalOver(double lambda, double line)
        {
            int k      = (int)Math.Floor(line);
            double cum = 0;
            for (int i = 0; i <= k; i++) cum += Poisson(i, lambda);
            return Math.Max(0, Math.Min(1, 1 - cum));
        }
    }
}
