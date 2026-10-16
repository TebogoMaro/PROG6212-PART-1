using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// ===================== REQUESTS (data the API expects) =====================

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored hashed, never in plain text.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>Required only when registering as an Organiser (prevents anyone self-promoting).</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public class EventRequest
{
    [Required, StringLength(150)]
    public string EventName { get; set; } = string.Empty;

    /// <summary>Event date (yyyy-MM-dd).</summary>
    [Required]
    public DateOnly? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Main distance of the event in kilometres.</summary>
    [Required, Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }
}

public class CategoryRequest
{
    /// <summary>For example "10km", "21km", "Under 20" or "Senior".</summary>
    [Required, StringLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Optional - leave empty for age-group categories.</summary>
    [Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }

    [Range(1, 100000)]
    public int MaxParticipants { get; set; } = 100;

    [Range(0.0, 100000.0)]
    public decimal EntryFee { get; set; }
}

public class RouteRequest
{
    [Required, StringLength(150)] public string StartPoint { get; set; } = string.Empty;
    [Required, StringLength(150)] public string EndPoint { get; set; } = string.Empty;
    [StringLength(1000)] public string? RouteDescription { get; set; }
    [StringLength(255), Url] public string? MapUrl { get; set; }
}

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, for example 00:48:32.</summary>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position (optional).</summary>
    [Range(1, 1000000)]
    public int? Position { get; set; }
}

// ===================== RESPONSES (data the API returns) =====================

public record UserDto(int UserId, string FullName, string Email, string Role, DateTime CreatedAt);

public record CategoryDto(int CategoryId, int EventId, string CategoryName, decimal? DistanceKm, int MaxParticipants, decimal EntryFee);

public record EventDto(int EventId, int OrganiserId, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string? Description, string EventType, decimal DistanceKm, DateTime CreatedAt);

public record EventDetailDto(int EventId, int OrganiserId, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string? Description, string EventType, decimal DistanceKm, DateTime CreatedAt,
    IReadOnlyList<CategoryDto> Categories);

public record RouteDto(int RouteId, int CategoryId, string StartPoint, string EndPoint, string? RouteDescription, string? MapUrl);

public record EnrolmentDto(int EnrolmentId, int ParticipantId, string ParticipantName, int EventId, string EventName,
    int CategoryId, string CategoryName, DateTime EnrolmentDate, string Status);

public record ResultDto(int ResultId, int EnrolmentId, string EventName, string CategoryName, TimeSpan FinishTime,
    int? Position, DateTime CapturedAt);

public record WeatherDto(string Location, DateOnly EventDate, bool IsForecast, double? TemperatureMaxC,
    double? TemperatureMinC, int? PrecipitationChancePercent, double? WindSpeedMaxKmh, string? Summary, string? Note);
