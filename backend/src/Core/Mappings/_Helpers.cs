using System.Globalization;

namespace Core.Mappings;

public static class _Helpers
{
    public static DateTime? ParseDate(string? value, string? fallback = null)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt;
        if (!string.IsNullOrWhiteSpace(fallback) &&
            DateTime.TryParse(fallback, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fb))
            return fb;
        return null;
    }

    public static string FormatOrNow(DateTime? value, DateTime? fallback = null)
    {
        var v = value ?? fallback ?? DateTime.UtcNow;
        return v.ToUniversalTime().ToString("o");
    }
}
