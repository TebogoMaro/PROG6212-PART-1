using System.Globalization;

namespace RaceDay.Web.Services;

/// <summary>Small formatting helpers used by the views so every page shows things the same way.</summary>
public static class Display
{
    /// <summary>1 -> 1st, 2 -> 2nd, 47 -> 47th, 112 -> 112th.</summary>
    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13) return number + "th";
        return (number % 10) switch
        {
            1 => number + "st",
            2 => number + "nd",
            3 => number + "rd",
            _ => number + "th"
        };
    }

    public static string Time(TimeSpan time) => time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    public static string Money(decimal amount) => amount == 0 ? "Free" : "R" + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Km(decimal km) => km.ToString("0.##", CultureInfo.InvariantCulture) + " km";

    public static string Date(DateOnly date) => date.ToString("ddd, d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Colour-coded enrolment status: green Confirmed, amber Pending, grey Cancelled.</summary>
    public static string StatusClass(string status) => status switch
    {
        "Confirmed" => "status-confirmed",
        "Pending" => "status-pending",
        "Cancelled" => "status-cancelled",
        _ => "status-cancelled"
    };

    public static string EventIcon(string eventType) => eventType switch
    {
        "Cycle" => "bi-bicycle",
        "Walk" => "bi-person-walking",
        _ => "bi-lightning-charge-fill"
    };

    public static bool IsUpcoming(DateOnly date) => date >= DateOnly.FromDateTime(DateTime.UtcNow);
}
