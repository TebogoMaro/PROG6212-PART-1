namespace RaceDay.Api.Models;

/// <summary>The two roles RaceDay supports.</summary>
public static class AppRoles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    /// <summary>Returns the canonical role name, or null if the value is not a valid role.</summary>
    public static string? Normalise(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "organiser" => Organiser,
        "participant" => Participant,
        _ => null
    };
}

/// <summary>The kinds of event RaceDay supports.</summary>
public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";

    public static string? Normalise(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "run" => Run,
        "walk" => Walk,
        "cycle" => Cycle,
        _ => null
    };
}

public static class EnrolmentStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

/// <summary>Table: Users. Holds both Organisers and Participants, separated by Role.</summary>
public class AppUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Participant;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}

/// <summary>Table: Events. Owned by one Organiser.</summary>
public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public AppUser? Organiser { get; set; }

    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = EventTypes.Run;
    public decimal DistanceKm { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}

/// <summary>Table: Categories. Distance categories (10km) or age categories (Under 20).</summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }

    public string CategoryName { get; set; } = string.Empty;
    /// <summary>Null for age-group categories such as "Senior".</summary>
    public decimal? DistanceKm { get; set; }
    public int MaxParticipants { get; set; } = 100;
    public decimal EntryFee { get; set; }

    public CourseRoute? Route { get; set; }
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}

/// <summary>Table: Routes. At most one route per category.</summary>
public class CourseRoute
{
    public int RouteId { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string StartPoint { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public string? RouteDescription { get; set; }
    public string? MapUrl { get; set; }
}

/// <summary>Table: EventEnrolments. Links a Participant, an Event and the chosen Category.</summary>
public class EventEnrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public AppUser? Participant { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = EnrolmentStatus.Confirmed;

    public RaceResult? Result { get; set; }
}

/// <summary>Table: Results. At most one result per enrolment.</summary>
public class RaceResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public EventEnrolment? Enrolment { get; set; }

    public TimeSpan FinishTime { get; set; }
    public int? Position { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
