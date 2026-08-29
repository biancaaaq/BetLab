namespace BetLab.DarkWeb.Helpers
{
    
    /// Conversie UTC → ora României (Europe/Bucharest, +2/+3 cu DST automat).
    /// Necesară pentru că Azure App Service rulează pe UTC, iar .ToLocalTime() ar întoarce tot UTC.
    
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo _romanianTz = ResolveRomanianTimeZone();

        private static TimeZoneInfo ResolveRomanianTimeZone()
        {
            // Linux (Azure App Service): "Europe/Bucharest"
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"); } catch { }
            // Windows: "GTB Standard Time" acoperă Romania/Bulgaria/Grecia
            try { return TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time"); } catch { }
            // Fallback: UTC+2 fix (fără DST corect)
            return TimeZoneInfo.CreateCustomTimeZone("Romania", TimeSpan.FromHours(2), "Romania", "Romania");
        }

        /// Convertește un DateTime UTC la ora României
        public static DateTime ToRomanianTime(this DateTime utcTime)
        {
            // Asigurăm că tipul e UTC ca să nu apară conversie dublă
            var asUtc = utcTime.Kind switch
            {
                DateTimeKind.Utc         => utcTime,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(utcTime, DateTimeKind.Utc),
                _                        => utcTime.ToUniversalTime()
            };
            return TimeZoneInfo.ConvertTimeFromUtc(asUtc, _romanianTz);
        }
    }
}
