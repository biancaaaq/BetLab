using BetLab.Domain.Entities;

namespace BetLab.Api.Services
{
    
    /// Calculează valoarea Cash Out pentru un bilet, urmând principiile folosite de bookmakerii reali.
    /// Formula generală:
    ///   CashOut = (Stake × OriginalTotalOdds / CurrentTotalOdds) × (1 - margin)
    ///           = PotentialPayout / CurrentTotalOdds × (1 - margin)
    /// Unde CurrentTotalOdds = produsul cotelor curente estimate:
    ///   - 1.0 pentru selecții deja câștigate (locked-in)
    ///   - estimated current odd pentru selecții încă active
    ///   - Dacă oricare e pierdută → CashOut = 0
    
    public static class CashOutCalculator
    {
        /// Marja pentru EthicalWeb — standardul industriei (5%).
        public const decimal EthicalMargin = 0.05m;

        /// Marja pentru DarkWeb — predatorie (30%).
        public const decimal DarkMargin = 0.30m;

        /// Marja default dacă nu se specifică variant.
        public const decimal DefaultMargin = 0.10m;

        public static decimal GetMarginForVariant(string? variant) => variant?.ToLowerInvariant() switch
        {
            "ethical" => EthicalMargin,
            "dark"    => DarkMargin,
            _         => DefaultMargin
        };

        
        /// Calculează valoarea cash-out pentru un bilet.
        /// Returnează 0 dacă vreo selecție e pierdută sau biletul nu poate fi încasat.
        
        public static CashOutResult Calculate(BetSlip betSlip, string? variant)
        {
            if (betSlip.Status != "Open")
                return CashOutResult.Unavailable("Biletul nu mai este activ.");

            var margin = GetMarginForVariant(variant);
            decimal currentTotalOdds = 1m;
            int lockedSelections = 0;
            int liveSelections   = 0;
            int prematchSelections = 0;

            foreach (var sel in betSlip.Selections)
            {
                var ev      = sel.Event;
                var outcome = sel.Outcome;
                if (ev == null || outcome == null)
                    return CashOutResult.Unavailable("Date incomplete pentru calcul.");

                // Selecție deja decisă (Won/Lost)
                if (sel.ResultStatus == "Lost" || outcome.IsWinning == false && ev.Status == "Finished")
                {
                    // Dacă vreo selecție a pierdut → bilet mort
                    if (sel.ResultStatus == "Lost") return CashOutResult.Unavailable("Cel puțin o selecție e pierdută — biletul nu mai poate fi încasat.");
                }

                if (sel.ResultStatus == "Won" || (ev.Status == "Finished" && outcome.IsWinning))
                {
                    // Locked in — factor 1.0
                    currentTotalOdds *= 1m;
                    lockedSelections++;
                    continue;
                }

                // Selecție încă activă — estimăm cota curentă
                decimal currentOdd;
                if (ev.IsLive && (ev.HomeScore.HasValue || ev.AwayScore.HasValue))
                {
                    // LIVE: ajustăm cota în funcție de scor + timp scurs
                    var minute = EstimateMinute(ev);
                    currentOdd = EstimateLiveOdd(
                        outcome.Code,
                        outcome.Market?.MarketType ?? "",
                        outcome.Odds,
                        ev.HomeScore ?? 0,
                        ev.AwayScore ?? 0,
                        minute);
                    liveSelections++;
                }
                else
                {
                    // Pre-meci: cota e cea inițială (nu s-a schimbat semnificativ)
                    currentOdd = outcome.Odds;
                    prematchSelections++;
                }

                currentTotalOdds *= currentOdd;
            }

            if (currentTotalOdds <= 0)
                return CashOutResult.Unavailable("Cota curentă invalidă.");

            // Formula realistă
            var rawValue = betSlip.PotentialPayout / currentTotalOdds;
            var afterMargin = rawValue * (1m - margin);
            var cashoutValue = decimal.Round(Math.Max(0m, afterMargin), 2);

            return new CashOutResult
            {
                Available           = true,
                CashOutValue        = cashoutValue,
                PotentialPayout     = betSlip.PotentialPayout,
                Stake               = betSlip.Stake,
                MarginPct           = margin * 100m,
                IsPreMatch          = liveSelections == 0,
                LiveSelections      = liveSelections,
                LockedSelections    = lockedSelections,
                PreMatchSelections  = prematchSelections
            };
        }

        
        /// Estimează cota curentă în funcție de scor + minut.
        /// Pentru piețele necunoscute folosește un factor de timp simplu.
        
        private static decimal EstimateLiveOdd(
            string outcomeCode,
            string marketType,
            decimal initialOdd,
            int homeScore,
            int awayScore,
            int minute)
        {
            if (initialOdd <= 1m) return initialOdd;

            // Convertim cota inițială în probabilitate "fair" (scoatem marja inițială)
            double initialProb = 1.0 / (double)initialOdd;
            double fairInitialProb = initialProb / 1.05; // presupunem marjă ~5% la cota originală

            int diff = homeScore - awayScore;
            int remainingMin = Math.Max(0, 95 - minute); // 90 + ~5 prelungiri
            double remainingFactor = remainingMin / 95.0; // 1.0 la start, 0 la final

            double newProb;

            switch (marketType)
            {
                case "1X2":
                    newProb = CalcProb1X2(outcomeCode, diff, fairInitialProb, remainingFactor);
                    break;

                case "DoubleChance":
                    newProb = CalcProbDoubleChance(outcomeCode, diff, fairInitialProb, remainingFactor);
                    break;

                case "BTTS":
                    newProb = CalcProbBtts(outcomeCode, homeScore, awayScore, fairInitialProb, remainingFactor);
                    break;

                case "OU_0_5":
                case "OU_1_5":
                case "OU_2_5":
                case "OU_3_5":
                    newProb = CalcProbOverUnder(outcomeCode, marketType, homeScore + awayScore, fairInitialProb, remainingFactor);
                    break;

                default:
                    // Piață necunoscută: degradăm cota proporțional cu timpul rămas
                    // Mai mult timp = mai aproape de cota inițială; final = mai certitudine
                    newProb = fairInitialProb;
                    break;
            }

            // Clamp pentru a evita cote absurde
            newProb = Math.Clamp(newProb, 0.01, 0.985);
            return decimal.Round((decimal)(1.0 / newProb), 2);
        }

        private static double CalcProb1X2(string code, int diff, double fairProb, double remainingFactor)
        {
            return code switch
            {
                "1" when diff > 0 => Math.Min(0.985, 0.55 + 0.18 * diff + (1 - remainingFactor) * 0.35),
                "1" when diff == 0 => fairProb * 0.7 * remainingFactor + 0.05 * remainingFactor,
                "1" when diff < 0 => Math.Max(0.02, fairProb * 0.4 * remainingFactor / (1 + Math.Abs(diff) * 0.5)),

                "X" when diff == 0 => Math.Min(0.85, 0.20 + (1 - remainingFactor) * 0.55),
                "X" when diff != 0 => Math.Max(0.02, 0.18 * remainingFactor / (1 + Math.Abs(diff) * 0.4)),

                "2" when diff < 0 => Math.Min(0.985, 0.55 + 0.18 * Math.Abs(diff) + (1 - remainingFactor) * 0.35),
                "2" when diff == 0 => fairProb * 0.7 * remainingFactor + 0.05 * remainingFactor,
                "2" when diff > 0 => Math.Max(0.02, fairProb * 0.4 * remainingFactor / (1 + diff * 0.5)),

                _ => fairProb
            };
        }

        private static double CalcProbDoubleChance(string code, int diff, double fairProb, double remainingFactor)
        {
            // "1X" = home or draw, "12" = home or away (no draw), "X2" = draw or away
            bool isHomeOrDraw = code == "1X";
            bool isHomeOrAway = code == "12";
            bool isDrawOrAway = code == "X2";

            if (isHomeOrDraw)
            {
                if (diff >= 0) return Math.Min(0.985, 0.7 + 0.15 * diff + (1 - remainingFactor) * 0.20);
                return Math.Max(0.05, fairProb * remainingFactor);
            }
            if (isHomeOrAway)
            {
                // Doar egal pierde
                if (diff != 0) return Math.Min(0.985, 0.6 + (1 - remainingFactor) * 0.30);
                return Math.Max(0.10, fairProb * remainingFactor + 0.10);
            }
            if (isDrawOrAway)
            {
                if (diff <= 0) return Math.Min(0.985, 0.7 + 0.15 * Math.Abs(diff) + (1 - remainingFactor) * 0.20);
                return Math.Max(0.05, fairProb * remainingFactor);
            }
            return fairProb;
        }

        private static double CalcProbBtts(string code, int homeScore, int awayScore, double fairProb, double remainingFactor)
        {
            bool isYes = code == "GG" || code.Equals("Yes", StringComparison.OrdinalIgnoreCase);

            if (isYes)
            {
                if (homeScore > 0 && awayScore > 0) return 0.99; // deja realizat
                // Dacă o echipă încă n-a marcat, probabilitatea scade cu cât rămâne mai puțin
                int teamsToScore = (homeScore == 0 ? 1 : 0) + (awayScore == 0 ? 1 : 0);
                return Math.Max(0.02, fairProb * Math.Pow(remainingFactor, teamsToScore));
            }
            else // NG
            {
                if (homeScore > 0 && awayScore > 0) return 0.01; // deja pierdut
                return Math.Min(0.98, 1.0 - fairProb * remainingFactor);
            }
        }

        private static double CalcProbOverUnder(string code, string marketType, int totalGoals, double fairProb, double remainingFactor)
        {
            double threshold = marketType switch
            {
                "OU_0_5" => 0.5,
                "OU_1_5" => 1.5,
                "OU_2_5" => 2.5,
                "OU_3_5" => 3.5,
                _        => 2.5
            };

            bool isOver = code.StartsWith("O", StringComparison.OrdinalIgnoreCase);

            if (isOver)
            {
                if (totalGoals > threshold) return 0.99; // deja realizat
                int goalsNeeded = (int)Math.Ceiling(threshold - totalGoals);
                return Math.Max(0.02, fairProb * Math.Pow(remainingFactor, goalsNeeded));
            }
            else // Under
            {
                if (totalGoals > threshold) return 0.01; // deja pierdut
                return Math.Min(0.98, 1.0 - fairProb * remainingFactor);
            }
        }

        
        /// Estimează minutul curent al meciului LIVE. Folosim StartTimeUtc + acum.
        /// Limit între 0 și 95.
        
        private static int EstimateMinute(Event ev)
        {
            var elapsed = DateTime.UtcNow - ev.StartTimeUtc;
            int min = (int)Math.Floor(elapsed.TotalMinutes);
            // Adăugăm ~15 minute pauză la jumătate (între min 45 și 60 echivalente)
            if (min > 45 && min < 60) min = 45;
            else if (min >= 60) min -= 15;
            return Math.Clamp(min, 1, 95);
        }
    }

    public class CashOutResult
    {
        public bool    Available          { get; set; }
        public decimal CashOutValue       { get; set; }
        public decimal PotentialPayout    { get; set; }
        public decimal Stake              { get; set; }
        public decimal MarginPct          { get; set; }
        public bool    IsPreMatch         { get; set; }
        public int     LiveSelections     { get; set; }
        public int     LockedSelections   { get; set; }
        public int     PreMatchSelections { get; set; }
        public string? UnavailableReason  { get; set; }

        public static CashOutResult Unavailable(string reason) => new()
        {
            Available = false,
            UnavailableReason = reason
        };
    }
}
