using System.Globalization;

namespace RaceDay.Web.Helpers;

/// <summary>Small display helpers so every page formats money, dates, times and statuses the same way.</summary>
public static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Money(decimal amount) =>
        amount == 0 ? "Free" : "R" + amount.ToString("N2", Culture).Replace(",", " ");

    public static string Distance(decimal km) => km.ToString("0.##", Culture) + " km";

    public static string Date(DateOnly date) => date.ToString("ddd, d MMM yyyy", Culture);

    public static string ShortDate(DateOnly date) => date.ToString("d MMM yyyy", Culture);

    public static string DateTimeShort(DateTime value) => value.ToString("d MMM yyyy, HH:mm", Culture);

    public static string Time(TimeSpan time) =>
        time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";

    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13)
            return number + "th";

        return (number % 10) switch
        {
            1 => number + "st",
            2 => number + "nd",
            3 => number + "rd",
            _ => number + "th"
        };
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
            return parts[0][..1].ToUpperInvariant();
        return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>Colour-coded enrolment status: green Confirmed, amber Pending, red Cancelled, gold Completed.</summary>
    public static string StatusCss(string status, bool hasResult)
    {
        if (hasResult)
            return "rd-status rd-status--completed";

        return status.ToLowerInvariant() switch
        {
            "confirmed" => "rd-status rd-status--confirmed",
            "pending" => "rd-status rd-status--pending",
            "cancelled" => "rd-status rd-status--cancelled",
            _ => "rd-status rd-status--pending"
        };
    }

    public static string StatusText(string status, bool hasResult) => hasResult ? "Completed" : status;

    public static string TypeCss(string eventType) => eventType.ToLowerInvariant() switch
    {
        "run" => "rd-type rd-type--run",
        "walk" => "rd-type rd-type--walk",
        "cycle" => "rd-type rd-type--cycle",
        _ => "rd-type"
    };
}
