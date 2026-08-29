using System.Text.Json;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BetLab.Api.Services
{
    
    /// Import meciuri + cote multiple + analytics de la API-FOOTBALL
    
    public class ApiFootballImportService
    {
        private readonly HttpClient _http;
        private readonly BetLabDbContext _context;
        private readonly MarketGeneratorService _marketGen;
        private readonly IMemoryCache _cache;

        public ApiFootballImportService(
            HttpClient http,
            BetLabDbContext context,
            MarketGeneratorService marketGen,
            IMemoryCache cache)
        {
            _http      = http;
            _context   = context;
            _marketGen = marketGen;
            _cache     = cache;
        }

        
        /// Importă meciurile din liga + sezonul dat.
         
        public async Task<ImportResult> ImportFixturesAsync(
            int leagueId,
            int season,
            string competitionName,
            string country = "Romania",
            string? dateFromIso = null,
            string? dateToIso   = null,
            bool onlyUpcoming  = true)
        {
            // 1. Construim URL pentru fixtures (subdomeniul are deja "v3" → nu adăugăm prefix)
            string url = $"fixtures?league={leagueId}&season={season}";
            if (!string.IsNullOrWhiteSpace(dateFromIso)) url += $"&from={dateFromIso}";
            if (!string.IsNullOrWhiteSpace(dateToIso))   url += $"&to={dateToIso}";
            // status=NS → doar meciuri programate (nu cele jucate sau live)
            if (onlyUpcoming) url += "&status=NS";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"API-FOOTBALL fixtures a returnat {(int)response.StatusCode}: {err}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // Verifică dacă API-ul a întors erori
            if (doc.RootElement.TryGetProperty("errors", out var errorsEl))
            {
                string? errMsg = null;
                if (errorsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in errorsEl.EnumerateObject())
                    {
                        errMsg = $"{prop.Name}: {prop.Value}";
                        break;
                    }
                }
                else if (errorsEl.ValueKind == JsonValueKind.Array && errorsEl.GetArrayLength() > 0)
                {
                    errMsg = errorsEl[0].ToString();
                }
                if (!string.IsNullOrWhiteSpace(errMsg))
                    throw new InvalidOperationException($"API-FOOTBALL: {errMsg}");
            }

            // Verifică numărul de rezultate raportate
            int resultsCount = 0;
            if (doc.RootElement.TryGetProperty("results", out var resultsEl) && resultsEl.ValueKind == JsonValueKind.Number)
                resultsCount = resultsEl.GetInt32();

            if (!doc.RootElement.TryGetProperty("response", out var fixtures) ||
                fixtures.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"Răspuns API-FOOTBALL invalid (lipsește 'response'). Raw: {json.Substring(0, Math.Min(500, json.Length))}");
            }

            if (resultsCount == 0)
            {
                throw new InvalidOperationException(
                    $"API-FOOTBALL: 0 meciuri găsite pentru leagueId={leagueId}, season={season}. " +
                    "Verifică dacă liga e disponibilă în planul tău și dacă sezonul e corect (ex: '2024' pentru sezonul 2024-25).");
            }

            // 2. Asigurăm sportul + competiția
            var sport = await _context.Sports.FirstOrDefaultAsync(s => s.Name == "Football")
                        ?? await EnsureSportAsync("Football");

            var competition = await _context.Competitions.FirstOrDefaultAsync(c => c.Name == competitionName)
                              ?? await EnsureCompetitionAsync(competitionName, country, sport.Id);

            var result = new ImportResult();

            // 3. Iterăm fixtures, salvăm Events
            foreach (var fix in fixtures.EnumerateArray())
            {
                try
                {
                    var fixture = fix.GetProperty("fixture");
                    var fixtureId = fixture.GetProperty("id").GetInt32();
                    var kickoffStr = fixture.GetProperty("date").GetString();
                    if (!DateTime.TryParse(kickoffStr, out var kickoff)) continue;

                    var teams = fix.GetProperty("teams");
                    var homeName = teams.GetProperty("home").GetProperty("name").GetString() ?? "";
                    var awayName = teams.GetProperty("away").GetProperty("name").GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(homeName) || string.IsNullOrWhiteSpace(awayName))
                        continue;

                    var homeTeam = await EnsureTeamAsync(homeName, country);
                    var awayTeam = await EnsureTeamAsync(awayName, country);

                    var existing = await _context.Events.FirstOrDefaultAsync(e =>
                        e.HomeTeamId == homeTeam.Id &&
                        e.AwayTeamId == awayTeam.Id &&
                        e.StartTimeUtc.Date == kickoff.ToUniversalTime().Date);

                    Event ev;
                    if (existing != null)
                    {
                        if (existing.ExternalMatchId == null)
                        {
                            existing.ExternalMatchId = fixtureId;
                            await _context.SaveChangesAsync();
                        }
                        ev = existing;
                        result.Skipped++;
                    }
                    else
                    {
                        ev = new Event
                        {
                            SportId = sport.Id,
                            CompetitionId = competition.Id,
                            HomeTeamId = homeTeam.Id,
                            AwayTeamId = awayTeam.Id,
                            StartTimeUtc = kickoff.ToUniversalTime(),
                            Status = "Open",
                            IsLive = false,
                            ExternalMatchId = fixtureId
                        };
                        _context.Events.Add(ev);
                        await _context.SaveChangesAsync();
                        result.Imported++;
                    }

                    // 4. Importăm cotele pentru acest fixture (best effort, nu blocăm)
                    if (existing == null || !await _context.Markets.AnyAsync(m => m.EventId == ev.Id))
                    {
                        var oddsOk = await TryImportOddsForFixtureAsync(ev, fixtureId, homeName, awayName);
                        if (oddsOk) result.OddsImported++;
                        else        result.OddsFailed++;
                    }
                }
                catch
                {
                    result.Errors++;
                }
            }

            return result;
        }

       
        /// Importă cotele pentru un fixture specific. Mapăm doar piețele suportate de aplicație.
        
        private async Task<bool> TryImportOddsForFixtureAsync(Event ev, int fixtureId, string homeName, string awayName)
        {
            try
            {
                var resp = await _http.GetAsync($"odds?fixture={fixtureId}&bookmaker=8"); // bookmaker 8 = Bet365 (cote stabile)
                if (!resp.IsSuccessStatusCode) return false;

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("response", out var responseArr) ||
                    responseArr.ValueKind != JsonValueKind.Array ||
                    responseArr.GetArrayLength() == 0)
                {
                    // Nu există cote — generăm cote inventate (1X2 random + piețe auxiliare)
                    await EnsureFallbackMarketsAsync(ev, homeName, awayName);
                    return false;
                }

                // Luăm primul bookmaker disponibil
                var first = responseArr[0];
                if (!first.TryGetProperty("bookmakers", out var bookmakers) ||
                    bookmakers.ValueKind != JsonValueKind.Array ||
                    bookmakers.GetArrayLength() == 0)
                {
                    await _marketGen.GenerateMarketsForEventAsync(ev.Id);
                    return false;
                }

                var bookmaker = bookmakers[0];
                var bets = bookmaker.GetProperty("bets");

                int importedMarkets = 0;

                foreach (var bet in bets.EnumerateArray())
                {
                    var betName = bet.GetProperty("name").GetString() ?? "";
                    var values = bet.GetProperty("values");

                    // Mapăm piețele cunoscute
                    switch (betName)
                    {
                        case "Match Winner":
                            await Create1X2Market(ev.Id, homeName, awayName, values);
                            importedMarkets++;
                            break;

                        case "Both Teams Score":
                            await CreateBttsMarket(ev.Id, values);
                            importedMarkets++;
                            break;

                        case "Goals Over/Under":
                            await CreateOverUnderMarkets(ev.Id, values);
                            importedMarkets++;
                            break;

                        case "Double Chance":
                            await CreateDoubleChanceMarket(ev.Id, homeName, awayName, values);
                            importedMarkets++;
                            break;

                        case "Exact Score":
                            await CreateCorrectScoreMarket(ev.Id, values);
                            importedMarkets++;
                            break;
                    }
                }

                // Dacă API nu a returnat piețe utile, generăm noi cote
                if (importedMarkets == 0)
                {
                    await EnsureFallbackMarketsAsync(ev, homeName, awayName);
                    return false;
                }

                // Dacă API ne-a dat alte piețe dar lipsește 1X2-ul, generăm 1X2 random
                // ca să se poată calcula piețele auxiliare matematic.
                if (!await _context.Markets.AnyAsync(m => m.EventId == ev.Id && m.MarketType == "1X2"))
                {
                    await CreateRandom1X2Async(ev.Id, homeName, awayName);
                }

                return true;
            }
            catch
            {
                // Fallback: piețe auto-generate dacă a eșuat orice
                try { await EnsureFallbackMarketsAsync(ev, homeName, awayName); } catch { }
                return false;
            }
        }

    
        /// Fallback complet — creează 1X2 random + generează piețele auxiliare matematic.
        /// Folosit când API-FOOTBALL nu are cote (ex: World Cup prea departe în viitor).
        
        private async Task EnsureFallbackMarketsAsync(Event ev, string homeName, string awayName)
        {
            // Dacă deja există 1X2 (ex: import parțial reușit), nu-l recreem
            var has1X2 = await _context.Markets.AnyAsync(m => m.EventId == ev.Id && m.MarketType == "1X2");
            if (!has1X2)
                await CreateRandom1X2Async(ev.Id, homeName, awayName);

            await _marketGen.GenerateMarketsForEventAsync(ev.Id);
        }

        
        /// Creează o piață 1X2 cu cote realiste random (folosit ca seed pentru piețele matematice).
        
        private async Task CreateRandom1X2Async(int eventId, string homeName, string awayName)
        {
            var rng = new Random();
            // Distribuție realistă: gazda ușor favorizată, draw în medie ~25%
            double pHome = 0.30 + rng.NextDouble() * 0.25; // 30-55%
            double pDraw = 0.20 + rng.NextDouble() * 0.10; // 20-30%
            double pAway = Math.Max(0.15, 1.0 - pHome - pDraw);
            double total = pHome + pDraw + pAway;
            double margin = 1.05;
            pHome /= total / margin; pDraw /= total / margin; pAway /= total / margin;

            decimal oddHome = Math.Round((decimal)(1.0 / pHome), 2);
            decimal oddDraw = Math.Round((decimal)(1.0 / pDraw), 2);
            decimal oddAway = Math.Round((decimal)(1.0 / pAway), 2);

            var market = new Market
            {
                EventId    = eventId,
                Name       = "Rezultat final",
                MarketType = "1X2",
                Status     = "Open",
                IsMain     = true
            };
            _context.Markets.Add(market);
            await _context.SaveChangesAsync();

            _context.Outcomes.AddRange(
                new Outcome { MarketId = market.Id, Name = homeName, Code = "1", Odds = oddHome, Status = "Open" },
                new Outcome { MarketId = market.Id, Name = "Egal",   Code = "X", Odds = oddDraw, Status = "Open" },
                new Outcome { MarketId = market.Id, Name = awayName, Code = "2", Odds = oddAway, Status = "Open" }
            );
            await _context.SaveChangesAsync();
        }

        

        private async Task Create1X2Market(int eventId, string homeName, string awayName, JsonElement values)
        {
            var market = new Market
            {
                EventId    = eventId,
                Name       = "Rezultat final",
                MarketType = "1X2",
                Status     = "Open",
                IsMain     = true
            };
            _context.Markets.Add(market);
            await _context.SaveChangesAsync();

            foreach (var v in values.EnumerateArray())
            {
                var name = v.GetProperty("value").GetString() ?? "";
                var oddStr = v.GetProperty("odd").GetString() ?? "1.00";
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                                       System.Globalization.CultureInfo.InvariantCulture, out var odd))
                    odd = 1.0m;

                string code; string displayName;
                if (name == "Home") { code = "1"; displayName = homeName; }
                else if (name == "Draw") { code = "X"; displayName = "Egal"; }
                else if (name == "Away") { code = "2"; displayName = awayName; }
                else continue;

                _context.Outcomes.Add(new Outcome
                {
                    MarketId = market.Id, Name = displayName, Code = code, Odds = odd, Status = "Open"
                });
            }
            await _context.SaveChangesAsync();
        }

        private async Task CreateBttsMarket(int eventId, JsonElement values)
        {
            var market = new Market
            {
                EventId = eventId, Name = "Ambele echipe marchează",
                MarketType = "BTTS", Status = "Open", IsMain = false
            };
            _context.Markets.Add(market);
            await _context.SaveChangesAsync();

            foreach (var v in values.EnumerateArray())
            {
                var name = v.GetProperty("value").GetString() ?? "";
                var oddStr = v.GetProperty("odd").GetString() ?? "1.00";
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                                       System.Globalization.CultureInfo.InvariantCulture, out var odd))
                    odd = 1.0m;

                string code; string displayName;
                if (name == "Yes") { code = "GG"; displayName = "Da"; }
                else if (name == "No") { code = "NG"; displayName = "Nu"; }
                else continue;

                _context.Outcomes.Add(new Outcome
                {
                    MarketId = market.Id, Name = displayName, Code = code, Odds = odd, Status = "Open"
                });
            }
            await _context.SaveChangesAsync();
        }

        private async Task CreateOverUnderMarkets(int eventId, JsonElement values)
        {
            // Grupăm valorile pe prag (1.5, 2.5, 3.5)
            var byThreshold = new Dictionary<string, (decimal? over, decimal? under)>();

            foreach (var v in values.EnumerateArray())
            {
                var name = v.GetProperty("value").GetString() ?? "";
                var oddStr = v.GetProperty("odd").GetString() ?? "1.00";
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                                       System.Globalization.CultureInfo.InvariantCulture, out var odd))
                    continue;

                // Format: "Over 2.5" sau "Under 2.5"
                var parts = name.Split(' ');
                if (parts.Length != 2) continue;
                var direction = parts[0]; // "Over" / "Under"
                var threshold = parts[1]; // "2.5"

                if (!byThreshold.ContainsKey(threshold))
                    byThreshold[threshold] = (null, null);

                var cur = byThreshold[threshold];
                if (direction == "Over") cur.over = odd;
                else if (direction == "Under") cur.under = odd;
                byThreshold[threshold] = cur;
            }

            // Salvăm doar pragurile 1.5 și 2.5 (cele mai populare)
            foreach (var threshold in new[] { "1.5", "2.5" })
            {
                if (!byThreshold.TryGetValue(threshold, out var pair)) continue;
                if (pair.over == null || pair.under == null) continue;

                var marketType = threshold == "1.5" ? "OU_1_5" : "OU_2_5";
                var market = new Market
                {
                    EventId = eventId,
                    Name = $"Total goluri {threshold}",
                    MarketType = marketType,
                    Status = "Open",
                    IsMain = false
                };
                _context.Markets.Add(market);
                await _context.SaveChangesAsync();

                _context.Outcomes.AddRange(
                    new Outcome { MarketId = market.Id, Name = $"Peste {threshold}", Code = $"O{threshold}", Odds = pair.over.Value, Status = "Open" },
                    new Outcome { MarketId = market.Id, Name = $"Sub {threshold}",   Code = $"U{threshold}", Odds = pair.under.Value, Status = "Open" }
                );
                await _context.SaveChangesAsync();
            }
        }

        private async Task CreateDoubleChanceMarket(int eventId, string homeName, string awayName, JsonElement values)
        {
            var market = new Market
            {
                EventId = eventId, Name = "Șansă dublă",
                MarketType = "DoubleChance", Status = "Open", IsMain = false
            };
            _context.Markets.Add(market);
            await _context.SaveChangesAsync();

            foreach (var v in values.EnumerateArray())
            {
                var name = v.GetProperty("value").GetString() ?? "";
                var oddStr = v.GetProperty("odd").GetString() ?? "1.00";
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                                       System.Globalization.CultureInfo.InvariantCulture, out var odd))
                    odd = 1.0m;

                string code; string displayName;
                if (name == "Home/Draw") { code = "1X"; displayName = $"{homeName} sau Egal"; }
                else if (name == "Home/Away") { code = "12"; displayName = $"{homeName} sau {awayName}"; }
                else if (name == "Draw/Away") { code = "X2"; displayName = $"Egal sau {awayName}"; }
                else continue;

                _context.Outcomes.Add(new Outcome
                {
                    MarketId = market.Id, Name = displayName, Code = code, Odds = odd, Status = "Open"
                });
            }
            await _context.SaveChangesAsync();
        }

        private async Task CreateCorrectScoreMarket(int eventId, JsonElement values)
        {
            var market = new Market
            {
                EventId = eventId, Name = "Scor exact",
                MarketType = "CorrectScore", Status = "Open", IsMain = false
            };
            _context.Markets.Add(market);
            await _context.SaveChangesAsync();

            int added = 0;
            foreach (var v in values.EnumerateArray())
            {
                if (added >= 12) break; // Limităm la 12 scoruri populare
                var name = v.GetProperty("value").GetString() ?? "";
                var oddStr = v.GetProperty("odd").GetString() ?? "1.00";
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                                       System.Globalization.CultureInfo.InvariantCulture, out var odd))
                    continue;

                // Format api-football: "1:0", "2:1" etc. → convertim la "1 - 0"
                var parts = name.Split(':');
                if (parts.Length != 2) continue;
                var displayName = $"{parts[0].Trim()} - {parts[1].Trim()}";
                var code = $"CS_{parts[0].Trim()}_{parts[1].Trim()}";

                _context.Outcomes.Add(new Outcome
                {
                    MarketId = market.Id, Name = displayName, Code = code, Odds = odd, Status = "Open"
                });
                added++;
            }
            await _context.SaveChangesAsync();
        }

    
        /// Importă un singur fixture după ID-ul lui API-FOOTBALL (util când știi exact meciul dorit).
        
        public async Task<ImportResult> ImportSingleFixtureByIdAsync(int fixtureId)
        {
            var result = new ImportResult();

            var resp = await _http.GetAsync($"fixtures?id={fixtureId}");
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"API-FOOTBALL fixture {fixtureId}: status {(int)resp.StatusCode}. {err}");
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var responseArr) ||
                responseArr.ValueKind != JsonValueKind.Array ||
                responseArr.GetArrayLength() == 0)
            {
                throw new InvalidOperationException($"Fixture {fixtureId} nu a fost găsit la API-FOOTBALL.");
            }

            var fix = responseArr[0];
            var fixture = fix.GetProperty("fixture");
            var kickoffStr = fixture.GetProperty("date").GetString();
            if (!DateTime.TryParse(kickoffStr, out var kickoff))
                throw new InvalidOperationException("Data fixture-ului invalidă.");

            // Extragem info competiție din răspuns
            var leagueEl = fix.GetProperty("league");
            var competitionName = leagueEl.GetProperty("name").GetString() ?? "Unknown League";
            var country = leagueEl.TryGetProperty("country", out var cEl) ? (cEl.GetString() ?? "Unknown") : "Unknown";

            var teams = fix.GetProperty("teams");
            var homeName = teams.GetProperty("home").GetProperty("name").GetString() ?? "";
            var awayName = teams.GetProperty("away").GetProperty("name").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(homeName) || string.IsNullOrWhiteSpace(awayName))
                throw new InvalidOperationException("Numele echipelor lipsesc.");

            var sport = await _context.Sports.FirstOrDefaultAsync(s => s.Name == "Football")
                        ?? await EnsureSportAsync("Football");

            var competition = await _context.Competitions.FirstOrDefaultAsync(c => c.Name == competitionName)
                              ?? await EnsureCompetitionAsync(competitionName, country, sport.Id);

            var homeTeam = await EnsureTeamAsync(homeName, country);
            var awayTeam = await EnsureTeamAsync(awayName, country);

            // Verificăm dacă există deja (după external ID sau combinație teams+date)
            var existing = await _context.Events.FirstOrDefaultAsync(e =>
                e.ExternalMatchId == fixtureId ||
                (e.HomeTeamId == homeTeam.Id && e.AwayTeamId == awayTeam.Id &&
                 e.StartTimeUtc.Date == kickoff.ToUniversalTime().Date));

            Event ev;
            if (existing != null)
            {
                // Actualizăm cu datele oficiale (poate user-ul a băgat invers home/away)
                existing.HomeTeamId      = homeTeam.Id;
                existing.AwayTeamId      = awayTeam.Id;
                existing.CompetitionId   = competition.Id;
                existing.StartTimeUtc    = kickoff.ToUniversalTime();
                existing.ExternalMatchId = fixtureId;
                await _context.SaveChangesAsync();
                ev = existing;
                result.Skipped = 1;
            }
            else
            {
                ev = new Event
                {
                    SportId          = sport.Id,
                    CompetitionId    = competition.Id,
                    HomeTeamId       = homeTeam.Id,
                    AwayTeamId       = awayTeam.Id,
                    StartTimeUtc     = kickoff.ToUniversalTime(),
                    Status           = "Open",
                    IsLive           = false,
                    ExternalMatchId  = fixtureId
                };
                _context.Events.Add(ev);
                await _context.SaveChangesAsync();
                result.Imported = 1;
            }

            // Importăm cotele dacă nu există deja piețe
            if (!await _context.Markets.AnyAsync(m => m.EventId == ev.Id))
            {
                var oddsOk = await TryImportOddsForFixtureAsync(ev, fixtureId, homeName, awayName);
                if (oddsOk) result.OddsImported = 1;
                else        result.OddsFailed = 1;
            }

            return result;
        }

        /// Sincronizează scorul live + statusul pentru un fixture specific de la API-FOOTBALL.
        /// Returnează true dacă a fost actualizat ceva.
    
        public async Task<LiveSyncResult> SyncLiveFixturesAsync()
        {
            var result = new LiveSyncResult();

            // Optimizare: sincronizăm DOAR meciurile relevante acum
            //  • IsLive=true (sunt în desfășurare)
            //  • Status=Open și încep în următoarele 30 minute (vor deveni live curând)
            //  • Status=Open și au început în ultimele 3 ore dar nu au IsLive setat (poate să fi început și nu am detectat încă)
            var now = DateTime.UtcNow;
            var windowStart = now.AddHours(-3);
            var windowEnd = now.AddMinutes(30);

            var events = await _context.Events
                .Include(e => e.Markets).ThenInclude(m => m.Outcomes)
                .Include(e => e.HomeTeam)
                .Include(e => e.AwayTeam)
                .Where(e => e.ExternalMatchId != null
                            && e.Status != "Finished"
                            && (e.IsLive
                                || (e.StartTimeUtc >= windowStart && e.StartTimeUtc <= windowEnd)))
                .ToListAsync();

            if (!events.Any())
            {
                result.Message = "Nu există evenimente cu ID extern API-FOOTBALL de sincronizat.";
                return result;
            }

            // Pentru fiecare event, cerem statusul individual
            foreach (var ev in events)
            {
                try
                {
                    var resp = await _http.GetAsync($"fixtures?id={ev.ExternalMatchId!.Value}");
                    if (!resp.IsSuccessStatusCode) continue;

                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("response", out var responseArr) ||
                        responseArr.ValueKind != JsonValueKind.Array ||
                        responseArr.GetArrayLength() == 0)
                        continue;

                    var fix = responseArr[0];
                    var fixture = fix.GetProperty("fixture");
                    var statusEl = fixture.GetProperty("status");
                    var status = statusEl.GetProperty("short").GetString() ?? "";

                    // API-FOOTBALL status codes:
                    // TBD, NS (not started), 1H, HT, 2H, ET, BT, P (penalty), SUSP, INT, LIVE, FT, AET, PEN, PST, CANC, ABD, AWD, WO
                    var isLive = status is "1H" or "HT" or "2H" or "ET" or "BT" or "P" or "LIVE";
                    var isFinished = status is "FT" or "AET" or "PEN" or "AWD" or "WO";

                    var newStatus = isLive ? "Live" : (isFinished ? "Finished" : ev.Status);
                    var wasFinished = ev.Status == "Finished";

                    ev.IsLive = isLive;
                    ev.Status = newStatus;

                    // Minutul curent — din status.elapsed (API-FOOTBALL îl include în toate statusurile live)
                    // La HT punem 45; la FT/AET punem 90; altfel luăm valoarea exactă din API
                    if (statusEl.TryGetProperty("elapsed", out var elapsedEl) && elapsedEl.ValueKind == JsonValueKind.Number)
                    {
                        ev.CurrentMinute = elapsedEl.GetInt32();
                    }
                    else if (status == "HT")
                    {
                        ev.CurrentMinute = 45;
                    }
                    else if (isFinished)
                    {
                        ev.CurrentMinute = 90;
                    }
                    else if (!isLive)
                    {
                        ev.CurrentMinute = null;
                    }

                    // Salvăm scorul
                    var goals = fix.GetProperty("goals");
                    int? homeScore = goals.GetProperty("home").ValueKind == JsonValueKind.Null
                        ? null : goals.GetProperty("home").GetInt32();
                    int? awayScore = goals.GetProperty("away").ValueKind == JsonValueKind.Null
                        ? null : goals.GetProperty("away").GetInt32();

                    if (homeScore.HasValue) ev.HomeScore = homeScore;
                    if (awayScore.HasValue) ev.AwayScore = awayScore;

                    if (isLive) result.MarkedLive++;

                    // Update cote live pentru meciurile în desfășurare
                    if (isLive)
                    {
                        try { await UpdateLiveOddsAsync(ev); }
                        catch { /* nu blocăm sync-ul de scor pe erori de odds */ }
                    }

                    if (isFinished && !wasFinished)
                    {
                        // Determină câștigătorul din scor
                        var winCode = (ev.HomeScore ?? 0) > (ev.AwayScore ?? 0) ? "1"
                                    : (ev.AwayScore ?? 0) > (ev.HomeScore ?? 0) ? "2"
                                    : "X";

                        var mainMarket = ev.Markets.FirstOrDefault(m => m.MarketType == "1X2");
                        if (mainMarket != null)
                        {
                            foreach (var o in mainMarket.Outcomes)
                                o.IsWinning = o.Code == winCode;
                        }
                        result.MarkedFinished++;
                    }
                }
                catch
                {
                    result.Errors++;
                }
            }

            await _context.SaveChangesAsync();
            result.Message = $"Live sync API-FOOTBALL: {result.MarkedLive} live, {result.MarkedFinished} finalizate, {result.Errors} erori.";
            return result;
        }

       
        /// Aduce cotele LIVE actualizate de la API-FOOTBALL pentru un meci în desfășurare
        /// și actualizează cotele existente în DB (NU creează piețe noi).
        /// Mapează piețele cunoscute: Match Winner, BTTS, Goals Over/Under, Double Chance.
        
        private async Task UpdateLiveOddsAsync(Event ev)
        {
            if (ev.ExternalMatchId == null) return;

            var resp = await _http.GetAsync($"odds/live?fixture={ev.ExternalMatchId.Value}");
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var responseArr) ||
                responseArr.ValueKind != JsonValueKind.Array ||
                responseArr.GetArrayLength() == 0) return;

            var first = responseArr[0];
            if (!first.TryGetProperty("odds", out var oddsArr) ||
                oddsArr.ValueKind != JsonValueKind.Array) return;

            foreach (var bet in oddsArr.EnumerateArray())
            {
                var betName = bet.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                if (!bet.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
                    continue;

                // Skip explicit piețele de halftime (le-am vrea pe full match)
                if (betName.Contains("(1st Half)", StringComparison.OrdinalIgnoreCase) ||
                    betName.Contains("(2nd Half)", StringComparison.OrdinalIgnoreCase) ||
                    betName.Contains(" minutes",  StringComparison.OrdinalIgnoreCase))
                    continue;

                switch (betName)
                {
                    // 1X2 — Full Time Result (API: "Fulltime Result" sau "Match Winner")
                    case "Fulltime Result":
                    case "Match Winner":
                    case "1x2":
                    case "1X2":
                        UpdateOutcomeOdds(ev, "1X2", values, ("Home", "1"), ("Draw", "X"), ("Away", "2"));
                        break;

                    // BTTS — atenție: API folosește "Both Teams to Score" (lowercase "to")
                    case "Both Teams to Score":
                    case "Both Teams To Score":
                    case "Both Teams Score":
                        UpdateOutcomeOdds(ev, "BTTS", values, ("Yes", "GG"), ("No", "NG"));
                        break;

                    case "Double Chance":
                        UpdateOutcomeOdds(ev, "DoubleChance", values,
                            ("Home/Draw", "1X"), ("Home/Away", "12"), ("Draw/Away", "X2"));
                        break;

                    // Over/Under cu handicap (1.5, 2.5, etc.)
                    case "Match Goals":
                    case "Over/Under Line":
                    case "Goals Over/Under":
                        UpdateOverUnderOdds(ev, values);
                        break;

                    // Correct Score — format API: "1:0", "2:1" → DB format: "1 - 0"
                    case "Final Score":
                    case "Correct Score":
                    case "Exact Score":
                        UpdateCorrectScoreOdds(ev, values);
                        break;

                    // Half Time / Full Time — API: "Home/Home", "Draw/Away" → DB: "1 / 1", "X / 2"
                    case "Half Time/Full Time":
                        UpdateHtFtOdds(ev, values);
                        break;

                    

                    // Draw No Bet — egalul = miza înapoi
                    case "Draw No Bet":
                        EnsureDrawNoBetMarket(ev, values);
                        break;

                    // Goals Odd/Even
                    case "Goals Odd/Even":
                        EnsureGoalsOddEvenMarket(ev, values);
                        break;

                    // Result / Both Teams To Score (combo 1X2 + BTTS)
                    case "Result / Both Teams To Score":
                    case "Result/Both Teams To Score":
                        EnsureResultBttsMarket(ev, values);
                        break;

                    // Total Corners (Over/Under cu prag variabil)
                    case "Total Corners":
                        EnsureTotalCornersMarket(ev, values);
                        break;

                    // Home Team Goals (Over/Under goluri gazdă)
                    case "Home Team Goals":
                        EnsureTeamGoalsMarket(ev, values, isHome: true);
                        break;

                    // Away Team Goals (Over/Under goluri oaspete)
                    case "Away Team Goals":
                        EnsureTeamGoalsMarket(ev, values, isHome: false);
                        break;

                    // Home Team to Score in Both Halves (Yes/No)
                    case "Home Team to Score in Both Halves":
                    case "Home Team To Score In Both Halves":
                        EnsureBothHalvesMarket(ev, values);
                        break;

                    // To Win 2nd Half — 1X2 pentru repriza 2
                    case "To Win 2nd Half":
                        EnsureWinSecondHalfMarket(ev, values);
                        break;
                }
            }

            // Strat 2: suspendăm outcomes imposibile bazat pe scor + minut curent
            ApplyDeterministicSuspensions(ev);

            // SaveChanges va fi apelat de SyncLiveFixturesAsync după ce termină toate evenimentele
        }

       
        /// Strat 2 — închide outcomes imposibile deterministic, în plus față de "suspended" din API.
        /// Acoperă cazurile unde API încă nu a actualizat suspended flag dar matematic outcome-ul e mort.
        
        private void ApplyDeterministicSuspensions(Event ev)
        {
            int home  = ev.HomeScore ?? 0;
            int away  = ev.AwayScore ?? 0;
            int total = home + away;
            int min   = EstimateCurrentMinute(ev);

            foreach (var market in ev.Markets)
            {
                switch (market.MarketType)
                {
                    case "BTTS":
                        // GG (Yes) câștigat dacă ambele au marcat → NG (No) mort
                        if (home > 0 && away > 0)
                            SuspendIfOpen(market, "NG");
                        break;

                    case "OU_0_5" when total > 0: SuspendIfOpen(market, "U0.5"); break;
                    case "OU_1_5" when total > 1: SuspendIfOpen(market, "U1.5"); break;
                    case "OU_2_5" when total > 2: SuspendIfOpen(market, "U2.5"); break;
                    case "OU_3_5" when total > 3: SuspendIfOpen(market, "U3.5"); break;

                    case "HOME_GOALS_0_5" when home > 0: SuspendIfOpen(market, "HOME_U0.5"); break;
                    case "HOME_GOALS_1_5" when home > 1: SuspendIfOpen(market, "HOME_U1.5"); break;
                    case "HOME_GOALS_2_5" when home > 2: SuspendIfOpen(market, "HOME_U2.5"); break;

                    case "AWAY_GOALS_0_5" when away > 0: SuspendIfOpen(market, "AWAY_U0.5"); break;
                    case "AWAY_GOALS_1_5" when away > 1: SuspendIfOpen(market, "AWAY_U1.5"); break;
                    case "AWAY_GOALS_2_5" when away > 2: SuspendIfOpen(market, "AWAY_U2.5"); break;

                    case "CorrectScore":
                        // Pentru fiecare scor "a-b", dacă scorul curent l-a depășit → mort
                        foreach (var o in market.Outcomes.Where(o => o.Status == "Open"))
                        {
                            var parts = o.Name.Split(" - ");
                            if (parts.Length != 2) continue;
                            if (!int.TryParse(parts[0].Trim(), out int a)) continue;
                            if (!int.TryParse(parts[1].Trim(), out int b)) continue;
                            if (home > a || away > b) o.Status = "Closed";
                        }
                        break;

                    case "HomeBothHalves":
                        // După repriza 1 (min > 45), dacă gazda n-a marcat în primele 45 minute → "Yes" mort
                        // Aproximăm: dacă suntem în repriza 2 și home == 0, blocăm "Yes"
                        // Notă: presupunem că toate golurile gazdei au fost în 1H dacă min < 50
                        if (min >= 46 && home == 0)
                            SuspendIfOpen(market, "BOTH_YES");
                        break;

                    case "WinSecondHalf":
                        // Repriza 2 = ultimele 45 min. Dacă suntem aproape de final fără goluri în 2H, e greu
                        // Skip — prea complex să știm câte goluri în 2H specific
                        break;
                }
            }
        }

        private void SuspendIfOpen(Market market, string outcomeCode)
        {
            var outcome = market.Outcomes.FirstOrDefault(o => o.Code == outcomeCode);
            if (outcome != null && outcome.Status == "Open")
                outcome.Status = "Closed";
        }

        private int EstimateCurrentMinute(Event ev)
        {
            if (!ev.IsLive) return 0;
            // Preferăm minutul sincronizat din API-FOOTBALL (exact)
            if (ev.CurrentMinute.HasValue && ev.CurrentMinute.Value > 0)
                return ev.CurrentMinute.Value;
            // Fallback: calculăm din ceasul de perete (aproximativ, fără stoppage time)
            var elapsed = DateTime.UtcNow - ev.StartTimeUtc;
            int min = (int)Math.Floor(elapsed.TotalMinutes);
            if (min > 45 && min < 60) min = 45;          // pauza (aproximativ 15 min)
            else if (min >= 60) min -= 15;
            return Math.Clamp(min, 1, 95);
        }

        

        private Market EnsureMarket(Event ev, string marketType, string displayName)
        {
            var market = ev.Markets.FirstOrDefault(m => m.MarketType == marketType);
            if (market == null)
            {
                market = new Market
                {
                    EventId    = ev.Id,
                    Name       = displayName,
                    MarketType = marketType,
                    Status     = "Open",
                    IsMain     = false
                };
                _context.Markets.Add(market);
                ev.Markets.Add(market); // ca să-l găsim în următoarea iterație
            }
            return market;
        }

        private void UpsertOutcome(Market market, string code, string name, decimal odds, bool suspended = false)
        {
            if (odds <= 1m) return;
            var newStatus = suspended ? "Closed" : "Open";
            var existing = market.Outcomes.FirstOrDefault(o => o.Code == code);
            if (existing != null)
            {
                existing.Odds = odds;
                existing.Status = newStatus;
            }
            else
            {
                var outcome = new Outcome
                {
                    MarketId = market.Id,
                    Name = name,
                    Code = code,
                    Odds = odds,
                    Status = newStatus
                };
                _context.Outcomes.Add(outcome);
                market.Outcomes.Add(outcome);
            }
        }

        private bool TryParseOdd(JsonElement v, out decimal odd, out bool suspended)
        {
            odd = 0;
            suspended = false;
            var oddStr = v.TryGetProperty("odd", out var oEl) ? oEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(oddStr)) return false;
            if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out odd) || odd <= 1m) return false;

            // Strat 1: respectăm câmpul "suspended" din API
            if (v.TryGetProperty("suspended", out var sEl) && sEl.ValueKind == JsonValueKind.True)
                suspended = true;
            return true;
        }

        // Overload pentru compatibilitate cu locurile vechi (1X2, BTTS, DoubleChance original)
        private bool TryParseOdd(JsonElement v, out decimal odd)
        {
            return TryParseOdd(v, out odd, out _);
        }

        private void EnsureDrawNoBetMarket(Event ev, JsonElement values)
        {
            var market = EnsureMarket(ev, "DrawNoBet", "Pariu fără egal");
            var home   = ev.HomeTeam?.Name ?? "Acasă";
            var away   = ev.AwayTeam?.Name ?? "Deplasare";

            foreach (var v in values.EnumerateArray())
            {
                if (!TryParseOdd(v, out var odd, out var susp)) continue;
                var apiValue = v.GetProperty("value").GetString() ?? "";
                if (apiValue.Equals("Home", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "DNB_1", home, odd, susp);
                else if (apiValue.Equals("Away", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "DNB_2", away, odd, susp);
            }
        }

        private void EnsureGoalsOddEvenMarket(Event ev, JsonElement values)
        {
            var market = EnsureMarket(ev, "GoalsOddEven", "Total goluri impar / par");
            foreach (var v in values.EnumerateArray())
            {
                if (!TryParseOdd(v, out var odd)) continue;
                var apiValue = v.GetProperty("value").GetString() ?? "";
                if (apiValue.Equals("Odd", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "ODD", "Impar", odd);
                else if (apiValue.Equals("Even", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "EVEN", "Par", odd);
            }
        }

        private void EnsureResultBttsMarket(Event ev, JsonElement values)
        {
            var market = EnsureMarket(ev, "ResultBtts", "Rezultat + Ambele marchează");
            foreach (var v in values.EnumerateArray())
            {
                if (!TryParseOdd(v, out var odd)) continue;
                var apiValue = v.GetProperty("value").GetString() ?? "";
                var parts = apiValue.Split('/');
                if (parts.Length != 2) continue;
                var resultCode = parts[0].Trim() switch
                {
                    "Home" => "1", "Draw" => "X", "Away" => "2",
                    _      => null
                };
                var bttsCode = parts[1].Trim() switch
                {
                    "Yes" => "GG", "No" => "NG",
                    _     => null
                };
                if (resultCode == null || bttsCode == null) continue;
                var code = $"RBTTS_{resultCode}_{bttsCode}";
                var name = $"{resultCode} & {bttsCode}";
                UpsertOutcome(market, code, name, odd);
            }
        }

        private void EnsureTotalCornersMarket(Event ev, JsonElement values)
        {
            // Grupăm pe handicap — fiecare prag = piață separată
            var byThreshold = new Dictionary<string, (decimal? over, decimal? under)>();
            foreach (var v in values.EnumerateArray())
            {
                if (!TryParseOdd(v, out var odd)) continue;
                var apiValue = v.GetProperty("value").GetString() ?? "";
                var handicap = v.TryGetProperty("handicap", out var hEl) && hEl.ValueKind != JsonValueKind.Null
                    ? hEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(handicap)) continue;

                if (!byThreshold.ContainsKey(handicap)) byThreshold[handicap] = (null, null);
                var cur = byThreshold[handicap];
                if (apiValue.Equals("Over",  StringComparison.OrdinalIgnoreCase)) cur.over  = odd;
                else if (apiValue.Equals("Under", StringComparison.OrdinalIgnoreCase)) cur.under = odd;
                byThreshold[handicap] = cur;
            }

            // Salvăm doar pragurile populare 8.5, 9.5, 10.5
            foreach (var threshold in new[] { "8.5", "9.5", "10.5" })
            {
                if (!byThreshold.TryGetValue(threshold, out var pair)) continue;
                if (pair.over == null || pair.under == null) continue;

                var marketType = $"CORNERS_{threshold.Replace(".", "_")}";
                var market = EnsureMarket(ev, marketType, $"Cornere {threshold}");
                UpsertOutcome(market, $"CORN_O{threshold}", $"Peste {threshold} cornere", pair.over.Value);
                UpsertOutcome(market, $"CORN_U{threshold}", $"Sub {threshold} cornere",   pair.under.Value);
            }
        }

        private void EnsureTeamGoalsMarket(Event ev, JsonElement values, bool isHome)
        {
            var teamName = isHome ? (ev.HomeTeam?.Name ?? "gazdă") : (ev.AwayTeam?.Name ?? "oaspete");
            var prefix   = isHome ? "HOME" : "AWAY";

            var byThreshold = new Dictionary<string, (decimal? over, decimal? under)>();
            foreach (var v in values.EnumerateArray())
            {
                if (!TryParseOdd(v, out var odd)) continue;
                var apiValue = v.GetProperty("value").GetString() ?? "";
                var handicap = v.TryGetProperty("handicap", out var hEl) && hEl.ValueKind != JsonValueKind.Null
                    ? hEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(handicap)) continue;

                if (!byThreshold.ContainsKey(handicap)) byThreshold[handicap] = (null, null);
                var cur = byThreshold[handicap];
                if (apiValue.Equals("Over",  StringComparison.OrdinalIgnoreCase)) cur.over  = odd;
                else if (apiValue.Equals("Under", StringComparison.OrdinalIgnoreCase)) cur.under = odd;
                byThreshold[handicap] = cur;
            }

            // Salvăm 0.5 și 1.5 (cele mai comune)
            foreach (var threshold in new[] { "0.5", "1.5", "2.5" })
            {
                if (!byThreshold.TryGetValue(threshold, out var pair)) continue;
                if (pair.over == null || pair.under == null) continue;

                var marketType = $"{prefix}_GOALS_{threshold.Replace(".", "_")}";
                var market = EnsureMarket(ev, marketType, $"Goluri {teamName} {threshold}");
                UpsertOutcome(market, $"{prefix}_O{threshold}", $"{teamName} peste {threshold}", pair.over.Value);
                UpsertOutcome(market, $"{prefix}_U{threshold}", $"{teamName} sub {threshold}",   pair.under.Value);
            }
        }

        private void EnsureBothHalvesMarket(Event ev, JsonElement values)
        {
            var teamName = ev.HomeTeam?.Name ?? "Gazda";
            var market = EnsureMarket(ev, "HomeBothHalves", $"{teamName} marcăm în ambele reprize");
            foreach (var v in values.EnumerateArray())
            {
                if (!TryParseOdd(v, out var odd)) continue;
                var apiValue = v.GetProperty("value").GetString() ?? "";
                if (apiValue.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "BOTH_YES", "Da", odd);
                else if (apiValue.Equals("No", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "BOTH_NO", "Nu", odd);
            }
        }

        private void EnsureWinSecondHalfMarket(Event ev, JsonElement values)
        {
            var market = EnsureMarket(ev, "WinSecondHalf", "Câștigător repriza 2");
            var home   = ev.HomeTeam?.Name ?? "Acasă";
            var away   = ev.AwayTeam?.Name ?? "Deplasare";
            foreach (var v in values.EnumerateArray())
            {
                if (!TryParseOdd(v, out var odd)) continue;
                var apiValue = v.GetProperty("value").GetString() ?? "";
                if (apiValue.Equals("Home", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "WSH_1", home, odd);
                else if (apiValue.Equals("Draw", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "WSH_X", "Egal", odd);
                else if (apiValue.Equals("Away", StringComparison.OrdinalIgnoreCase))
                    UpsertOutcome(market, "WSH_2", away, odd);
            }
        }

        ///Actualizează cotele Correct Score. API format: "1:0", DB format: "1 - 0"
        private void UpdateCorrectScoreOdds(Event ev, JsonElement values)
        {
            var market = ev.Markets.FirstOrDefault(m => m.MarketType == "CorrectScore");
            if (market == null) return;

            foreach (var v in values.EnumerateArray())
            {
                var apiValue = v.TryGetProperty("value", out var vEl) ? vEl.GetString() ?? "" : "";
                var oddStr   = v.TryGetProperty("odd",   out var oEl) ? oEl.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(oddStr)) continue;
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var newOdd)) continue;
                if (newOdd <= 1m) continue;

                // Convertim "1:0" → "1 - 0" pentru a matchui codurile noastre
                var parts = apiValue.Split(':');
                if (parts.Length != 2) continue;
                var dbName = $"{parts[0].Trim()} - {parts[1].Trim()}";
                var dbCode = $"CS_{parts[0].Trim()}_{parts[1].Trim()}";

                var outcome = market.Outcomes.FirstOrDefault(o =>
                    o.Code == dbCode || string.Equals(o.Name, dbName, StringComparison.OrdinalIgnoreCase));
                if (outcome != null) outcome.Odds = newOdd;
            }
        }

       
        private void UpdateHtFtOdds(Event ev, JsonElement values)
        {
            var market = ev.Markets.FirstOrDefault(m => m.MarketType == "HtFt");
            if (market == null) return;

            string MapTeam(string s) => s switch
            {
                "Home" => "1", "Draw" => "X", "Away" => "2",
                _      => s
            };

            foreach (var v in values.EnumerateArray())
            {
                var apiValue = v.TryGetProperty("value", out var vEl) ? vEl.GetString() ?? "" : "";
                var oddStr   = v.TryGetProperty("odd",   out var oEl) ? oEl.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(oddStr)) continue;
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var newOdd)) continue;
                if (newOdd <= 1m) continue;

                // Format API: "Home/Home", "Home/Draw", "Draw/Away" etc.
                var parts = apiValue.Split('/');
                if (parts.Length != 2) continue;
                var ht = MapTeam(parts[0].Trim());
                var ft = MapTeam(parts[1].Trim());

                var dbCode = $"HTFT_{ht}_{ft}";
                var dbName = $"{ht} / {ft}";

                var outcome = market.Outcomes.FirstOrDefault(o =>
                    o.Code == dbCode || string.Equals(o.Name, dbName, StringComparison.OrdinalIgnoreCase));
                if (outcome != null) outcome.Odds = newOdd;
            }
        }

        private void UpdateOutcomeOdds(Event ev, string marketType, JsonElement values,
            params (string apiValue, string code)[] mapping)
        {
            var market = ev.Markets.FirstOrDefault(m => m.MarketType == marketType);
            if (market == null) return;

            foreach (var v in values.EnumerateArray())
            {
                var apiValue = v.TryGetProperty("value", out var vEl) ? vEl.GetString() ?? "" : "";
                if (!TryParseOdd(v, out var newOdd, out var suspended)) continue;

                var match = mapping.FirstOrDefault(m =>
                    string.Equals(m.apiValue, apiValue, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(match.code)) continue;

                var outcome = market.Outcomes.FirstOrDefault(o => o.Code == match.code);
                if (outcome == null) continue;

                outcome.Odds = newOdd;
                // Respectăm flag-ul suspended din API (ex: 1X2 suspendat în minutul gol)
                if (suspended && outcome.Status == "Open")
                    outcome.Status = "Closed";
                else if (!suspended && outcome.Status == "Closed")
                    outcome.Status = "Open"; // redeschide dacă API confirmă că e din nou disponibil
            }
        }

        private void UpdateOverUnderOdds(Event ev, JsonElement values)
        {
            foreach (var v in values.EnumerateArray())
            {
                var apiValue = v.TryGetProperty("value", out var vEl) ? vEl.GetString() ?? "" : "";
                var oddStr   = v.TryGetProperty("odd",   out var oEl) ? oEl.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(oddStr)) continue;
                if (!decimal.TryParse(oddStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var newOdd)) continue;
                if (newOdd <= 1m) continue;

                // API live format: value="Over"/"Under" + handicap separate ("1.5", "2.5")
                // API pre-match format (vechi): value="Over 1.5" / "Under 2.5"
                string? direction = null;
                string? threshold = null;

                if (v.TryGetProperty("handicap", out var hEl) && hEl.ValueKind != JsonValueKind.Null)
                {
                    direction = apiValue;
                    threshold = hEl.GetString();
                }
                else
                {
                    // Fallback la formatul vechi
                    var parts = apiValue.Split(' ');
                    if (parts.Length != 2) continue;
                    direction = parts[0];
                    threshold = parts[1];
                }

                if (string.IsNullOrWhiteSpace(direction) || string.IsNullOrWhiteSpace(threshold)) continue;

                var marketType = threshold switch
                {
                    "0.5" => "OU_0_5",
                    "1.5" => "OU_1_5",
                    "2.5" => "OU_2_5",
                    "3.5" => "OU_3_5",
                    _     => null
                };
                if (marketType == null) continue;

                var market = ev.Markets.FirstOrDefault(m => m.MarketType == marketType);
                if (market == null) continue;

                var code = direction.Equals("Over", StringComparison.OrdinalIgnoreCase)
                    ? $"O{threshold}" : $"U{threshold}";
                var outcome = market.Outcomes.FirstOrDefault(o => o.Code == code);
                if (outcome != null) outcome.Odds = newOdd;
            }
        }

       

        private async Task<Sport> EnsureSportAsync(string name)
        {
            var s = new Sport { Name = name, IsActive = true };
            _context.Sports.Add(s);
            await _context.SaveChangesAsync();
            return s;
        }

        private async Task<Competition> EnsureCompetitionAsync(string name, string country, int sportId)
        {
            var c = new Competition { Name = name, Country = country, SportId = sportId };
            _context.Competitions.Add(c);
            await _context.SaveChangesAsync();
            return c;
        }

        private async Task<Team> EnsureTeamAsync(string name, string country)
        {
            var team = await _context.Teams.FirstOrDefaultAsync(t => t.Name == name);
            if (team != null) return team;

            team = new Team { Name = name, Country = country };
            _context.Teams.Add(team);
            await _context.SaveChangesAsync();
            return team;
        }

   
        public async Task<EventAnalytics?> GetAnalyticsAsync(int eventId)
        {
            var ev = await _context.Events
                .Include(e => e.HomeTeam)
                .Include(e => e.AwayTeam)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (ev == null) return null;

            var analytics = new EventAnalytics
            {
                EventId      = eventId,
                HomeTeamName = ev.HomeTeam?.Name ?? "Acasă",
                AwayTeamName = ev.AwayTeam?.Name ?? "Deplasare"
            };

            if (ev.ExternalMatchId == null)
            {
                analytics.HasRealData = false;
                analytics.Message     = "Eveniment local — statistici reale nedisponibile.";
                return analytics;
            }

            var cacheKey = $"af_analytics:{ev.ExternalMatchId.Value}";
            if (_cache.TryGetValue<EventAnalytics>(cacheKey, out var cached) && cached != null)
                return cached;

            try
            {
                // 1. Obținem ID-urile API-FOOTBALL ale celor 2 echipe din fixture
                var fixResp = await _http.GetAsync($"fixtures?id={ev.ExternalMatchId.Value}");
                if (!fixResp.IsSuccessStatusCode)
                {
                    analytics.HasRealData = false;
                    analytics.Message     = "API-FOOTBALL fixture indisponibil.";
                    return analytics;
                }

                var fixJson = await fixResp.Content.ReadAsStringAsync();
                using var fixDoc = JsonDocument.Parse(fixJson);
                var fixResponse = fixDoc.RootElement.GetProperty("response");
                if (fixResponse.GetArrayLength() == 0)
                {
                    analytics.HasRealData = false;
                    analytics.Message     = "Fixture necunoscut la API-FOOTBALL.";
                    return analytics;
                }

                var teams = fixResponse[0].GetProperty("teams");
                var homeId = teams.GetProperty("home").GetProperty("id").GetInt32();
                var awayId = teams.GetProperty("away").GetProperty("id").GetInt32();

                // 2. H2H — ultimele 10 întâlniri directe
                var h2hResp = await _http.GetAsync($"fixtures/headtohead?h2h={homeId}-{awayId}&last=10");
                if (h2hResp.IsSuccessStatusCode)
                {
                    var h2hJson = await h2hResp.Content.ReadAsStringAsync();
                    using var h2hDoc = JsonDocument.Parse(h2hJson);
                    if (h2hDoc.RootElement.TryGetProperty("response", out var h2hArr))
                    {
                        foreach (var match in h2hArr.EnumerateArray())
                        {
                            try
                            {
                                var f = match.GetProperty("fixture");
                                var dateStr = f.GetProperty("date").GetString() ?? "";
                                if (!DateTime.TryParse(dateStr, out var date)) continue;

                                var league = match.GetProperty("league");
                                var goals  = match.GetProperty("goals");
                                var mTeams = match.GetProperty("teams");
                                var mHome  = mTeams.GetProperty("home").GetProperty("name").GetString() ?? "";
                                var mAway  = mTeams.GetProperty("away").GetProperty("name").GetString() ?? "";
                                int hs = goals.GetProperty("home").ValueKind == JsonValueKind.Null ? 0 : goals.GetProperty("home").GetInt32();
                                int @as = goals.GetProperty("away").ValueKind == JsonValueKind.Null ? 0 : goals.GetProperty("away").GetInt32();

                                analytics.RecentMatches.Add(new H2HMatchInfo
                                {
                                    DateUtc     = date.ToUniversalTime(),
                                    HomeTeam    = mHome,
                                    AwayTeam    = mAway,
                                    Competition = league.GetProperty("name").GetString() ?? "",
                                    HomeScore   = hs,
                                    AwayScore   = @as
                                });

                                // Agregăm statisticile din perspectiva echipei gazdă din event-ul nostru
                                bool homeIsOurHome = mHome.Equals(analytics.HomeTeamName, StringComparison.OrdinalIgnoreCase);
                                int ourHomeScore = homeIsOurHome ? hs : @as;
                                int ourAwayScore = homeIsOurHome ? @as : hs;

                                analytics.H2H.NumMatches++;
                                analytics.H2H.TotalGoals += hs + @as;
                                if (ourHomeScore > ourAwayScore) analytics.H2H.HomeWins++;
                                else if (ourHomeScore < ourAwayScore) analytics.H2H.AwayWins++;
                                else { analytics.H2H.HomeDraws++; analytics.H2H.AwayDraws++; }
                            }
                            catch { /* skip match invalid */ }
                        }
                    }
                }

                // 3. Forma recentă a fiecărei echipe (ultimele 5)
                analytics.HomeForm = await FetchFormAsync(homeId, analytics.HomeTeamName);
                analytics.AwayForm = await FetchFormAsync(awayId, analytics.AwayTeamName);

                analytics.HasRealData = true;
                _cache.Set(cacheKey, analytics, TimeSpan.FromHours(6));
                return analytics;
            }
            catch (Exception ex)
            {
                analytics.HasRealData = false;
                analytics.Message     = $"Eroare la API-FOOTBALL: {ex.Message}";
                return analytics;
            }
        }

        private async Task<List<FormItem>> FetchFormAsync(int teamId, string teamName)
        {
            var form = new List<FormItem>();
            var resp = await _http.GetAsync($"fixtures?team={teamId}&last=5");
            if (!resp.IsSuccessStatusCode) return form;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var arr)) return form;

            foreach (var m in arr.EnumerateArray())
            {
                try
                {
                    var f       = m.GetProperty("fixture");
                    var dateStr = f.GetProperty("date").GetString() ?? "";
                    if (!DateTime.TryParse(dateStr, out var date)) continue;

                    var teams = m.GetProperty("teams");
                    var goals = m.GetProperty("goals");
                    var league = m.GetProperty("league");

                    var hName = teams.GetProperty("home").GetProperty("name").GetString() ?? "";
                    var aName = teams.GetProperty("away").GetProperty("name").GetString() ?? "";
                    int hs = goals.GetProperty("home").ValueKind == JsonValueKind.Null ? 0 : goals.GetProperty("home").GetInt32();
                    int @as = goals.GetProperty("away").ValueKind == JsonValueKind.Null ? 0 : goals.GetProperty("away").GetInt32();

                    bool isHome = hName.Equals(teamName, StringComparison.OrdinalIgnoreCase);
                    int forGoals     = isHome ? hs  : @as;
                    int againstGoals = isHome ? @as : hs;
                    string result = forGoals > againstGoals ? "W" : forGoals == againstGoals ? "D" : "L";

                    form.Add(new FormItem
                    {
                        DateUtc      = date.ToUniversalTime(),
                        Result       = result,
                        Opponent     = isHome ? aName : hName,
                        Competition  = league.GetProperty("name").GetString() ?? "",
                        GoalsFor     = forGoals,
                        GoalsAgainst = againstGoals,
                        WasHome      = isHome
                    });
                }
                catch { /* skip */ }
            }
            return form;
        }
    }

   
    public class EventAnalytics
    {
        public int                EventId       { get; set; }
        public string             HomeTeamName  { get; set; } = "";
        public string             AwayTeamName  { get; set; } = "";
        public bool               HasRealData   { get; set; }
        public string             Message       { get; set; } = "";
        public H2HAggregate       H2H           { get; set; } = new();
        public List<H2HMatchInfo> RecentMatches { get; set; } = new();
        public List<FormItem>     HomeForm      { get; set; } = new();
        public List<FormItem>     AwayForm      { get; set; } = new();
    }

    public class H2HAggregate
    {
        public int NumMatches { get; set; }
        public int TotalGoals { get; set; }
        public int HomeWins   { get; set; }
        public int HomeDraws  { get; set; }
        public int HomeLosses { get; set; }
        public int AwayWins   { get; set; }
        public int AwayDraws  { get; set; }
        public int AwayLosses { get; set; }
    }

    public class H2HMatchInfo
    {
        public DateTime DateUtc     { get; set; }
        public string   HomeTeam    { get; set; } = "";
        public string   AwayTeam    { get; set; } = "";
        public string   Competition { get; set; } = "";
        public int      HomeScore   { get; set; }
        public int      AwayScore   { get; set; }
    }

    public class FormItem
    {
        public DateTime DateUtc      { get; set; }
        public string   Result       { get; set; } = "D"; // W / D / L
        public string   Opponent     { get; set; } = "";
        public string   Competition  { get; set; } = "";
        public int      GoalsFor     { get; set; }
        public int      GoalsAgainst { get; set; }
        public bool     WasHome      { get; set; }
    }

    public class ImportResult
    {
        public int Imported     { get; set; }
        public int Skipped      { get; set; }
        public int OddsImported { get; set; }
        public int OddsFailed   { get; set; }
        public int Errors       { get; set; }

        public override string ToString() =>
            $"{Imported} importate, {Skipped} deja existente, {OddsImported} cu cote reale, {OddsFailed} cu cote auto, {Errors} erori.";
    }

    public class LiveSyncResult
    {
        public int    MarkedLive     { get; set; }
        public int    MarkedFinished { get; set; }
        public int    Errors         { get; set; }
        public string Message        { get; set; } = "";
    }
}
