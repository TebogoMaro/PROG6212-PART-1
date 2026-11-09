namespace RaceDay.Web.Models;

// Shapes of the JSON the RaceDay API returns. The web app holds NO business logic and NO database access -
// it only displays what the API sends.

public static class AppRoles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public record UserDto(int UserId, string FullName, string Email, string Role, DateTime CreatedAt, bool HasProfilePicture);

public record CategoryDto(int CategoryId, int EventId, string CategoryName, decimal? DistanceKm, int MaxParticipants, decimal EntryFee);

public record EventDto(int EventId, int OrganiserId, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string? Description, string EventType, decimal DistanceKm, DateTime CreatedAt, bool HasBanner);

public record EventDetailDto(int EventId, int OrganiserId, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string? Description, string EventType, decimal DistanceKm, DateTime CreatedAt, bool HasBanner,
    List<CategoryDto> Categories);

public record MyEventDto(int EventId, string EventName, DateOnly EventDate, string Location, string EventType,
    decimal DistanceKm, bool HasBanner, int CategoryCount, int EnrolmentCount);

public record RouteDto(int RouteId, int CategoryId, string StartPoint, string EndPoint, string? RouteDescription, string? MapUrl);

public record EnrolmentDto(int EnrolmentId, int ParticipantId, string ParticipantName, int EventId, string EventName,
    DateOnly EventDate, int CategoryId, string CategoryName, DateTime EnrolmentDate, string Status,
    int? ResultId, TimeSpan? FinishTime, int? Position);

public record ResultDto(int ResultId, int EnrolmentId, string EventName, DateOnly EventDate, string CategoryName,
    TimeSpan FinishTime, int? Position, int TotalFinishers, DateTime CapturedAt);

public record WeatherDto(string Location, DateOnly EventDate, bool IsForecast, double? TemperatureMaxC,
    double? TemperatureMinC, int? PrecipitationChancePercent, double? WindSpeedMaxKmh, string? Summary, string? Note);

/// <summary>The signed-in user, kept in the web session so every page knows who they are and their role.</summary>
public record SessionUser(int UserId, string FullName, string Email, string Role, bool HasProfilePicture);
