using BetLab.DarkWeb.Models;

namespace BetLab.DarkWeb.Helpers
{
    
    /// Verifică conflicte logice între selecțiile unui bilet.
    /// Toate regulile se aplică exclusiv pentru selecții din același eveniment.
    /// Regula 1 — 1X2 ↔ Șansă Dublă (DoubleChance): piețe suprapuse, indiferent de outcome.
    /// Regula 2 — GG (BTTS Yes) ↔ Sub 1.5 / Sub 0.5: GG implică ≥2 goluri, contradicție.
    /// Regula 3 — 1X2 ↔ Scor Corect cu câștigător opus.
    /// Regula 4 — 1X2 ↔ Pauză/Final cu rezultat final opus.
   
    public static class BetSlipConflictChecker
    {
       
        /// Returnează mesajul de eroare dacă <paramref name="newItem"/> intră în conflict
        /// cu oricare selecție din <paramref name="existingItems"/>, sau <c>null</c> dacă e OK.
        public static string? GetConflict(
            BetSlipItemViewModel newItem,
            IEnumerable<BetSlipItemViewModel> existingItems)
        {
            foreach (var item in existingItems.Where(x => x.EventId == newItem.EventId))
            {
                
                if ((Is1X2(newItem) && IsDoubleChance(item)) ||
                    (IsDoubleChance(newItem) && Is1X2(item)))
                    return "Nu poți combina Final (1X2) cu Șansă Dublă — piețe suprapuse.";

                
                if (IsBttsYes(newItem) && IsUnder(item, "OU_1_5", "OU_0_5"))
                    return "Contradicție: GG (ambele marchează = min. 2 goluri) + Sub 1.5 / Sub 0.5 goluri.";
                if (IsUnder(newItem, "OU_1_5", "OU_0_5") && IsBttsYes(item))
                    return "Contradicție: Sub 1.5 / Sub 0.5 goluri + GG (ambele marchează = min. 2 goluri).";

                
                if (Is1X2(newItem) && IsCorrectScore(item))
                {
                    var msg = Check1X2VsCorrectScore(newItem.OutcomeCode, item.OutcomeName);
                    if (msg != null) return msg;
                }
                if (Is1X2(item) && IsCorrectScore(newItem))
                {
                    var msg = Check1X2VsCorrectScore(item.OutcomeCode, newItem.OutcomeName);
                    if (msg != null) return msg;
                }

                
                if (Is1X2(newItem) && IsHtFt(item))
                {
                    var msg = Check1X2VsHtFt(newItem.OutcomeCode, item.OutcomeName);
                    if (msg != null) return msg;
                }
                if (Is1X2(item) && IsHtFt(newItem))
                {
                    var msg = Check1X2VsHtFt(item.OutcomeCode, newItem.OutcomeName);
                    if (msg != null) return msg;
                }
            }

            return null;
        }

        

        private static bool Is1X2(BetSlipItemViewModel x)         => x.MarketType == "1X2";
        private static bool IsDoubleChance(BetSlipItemViewModel x) => x.MarketType == "DoubleChance";
        private static bool IsCorrectScore(BetSlipItemViewModel x) => x.MarketType == "CorrectScore";
        private static bool IsHtFt(BetSlipItemViewModel x)         => x.MarketType == "HtFt";

        
        private static bool IsBttsYes(BetSlipItemViewModel x)
            => x.MarketType == "BTTS" && x.OutcomeCode == "GG";

        
        private static bool IsUnder(BetSlipItemViewModel x, params string[] marketTypes)
            => marketTypes.Contains(x.MarketType) && x.OutcomeCode.StartsWith("U", StringComparison.OrdinalIgnoreCase);

        

        private static string? Check1X2VsCorrectScore(string code1X2, string scoreName)
        {
            // scoreName format din generator: "1 - 0", "0 - 2", "1 - 1" etc.
            var parts = scoreName.Split(" - ");
            if (parts.Length != 2) return null;
            if (!int.TryParse(parts[0].Trim(), out int h) ||
                !int.TryParse(parts[1].Trim(), out int a)) return null;

            return code1X2 switch
            {
                "1" when a > h  => $"Contradicție: pariezi pe 1 (victorie acasă) dar scorul «{scoreName}» înseamnă victorie oaspeți.",
                "X" when h != a => $"Contradicție: pariezi pe X (egal) dar scorul «{scoreName}» nu e egal.",
                "2" when h > a  => $"Contradicție: pariezi pe 2 (victorie oaspeți) dar scorul «{scoreName}» înseamnă victorie acasă.",
                _               => null
            };
        }

        

        private static string? Check1X2VsHtFt(string code1X2, string htftName)
        {
            // htftName format din generator: "1 / X", "X / 2", "2 / 1" etc.
            // A doua parte (după " / ") = rezultat final
            var parts = htftName.Split(" / ");
            if (parts.Length != 2) return null;
            var ft = parts[1].Trim();

            return code1X2 switch
            {
                "1" when ft != "1" => $"Contradicție: pariezi pe 1 (victorie acasă) dar Pauză/Final «{htftName}» are alt rezultat final.",
                "X" when ft != "X" => $"Contradicție: pariezi pe X (egal) dar Pauză/Final «{htftName}» are alt rezultat final.",
                "2" when ft != "2" => $"Contradicție: pariezi pe 2 (victorie oaspeți) dar Pauză/Final «{htftName}» are alt rezultat final.",
                _                  => null
            };
        }
    }
}
