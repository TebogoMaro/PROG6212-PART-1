using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Services;

/// <summary>Maps entities to the DTOs returned by the API (so password hashes are never exposed).</summary>
public static class Mapping
{
    public static UserDto ToDto(this AppUser u) =>
        new(u.UserId, u.FullName, u.Email, u.Role, u.CreatedAt);

    public static CategoryDto ToDto(this Category c) =>
        new(c.CategoryId, c.EventId, c.CategoryName, c.DistanceKm, c.MaxParticipants, c.EntryFee);

    public static EventDto ToDto(this Event e) =>
        new(e.EventId, e.OrganiserId, e.Organiser?.FullName ?? string.Empty, e.EventName, e.EventDate,
            e.Location, e.Description, e.EventType, e.DistanceKm, e.CreatedAt);

    public static EventDetailDto ToDetailDto(this Event e) =>
        new(e.EventId, e.OrganiserId, e.Organiser?.FullName ?? string.Empty, e.EventName, e.EventDate,
            e.Location, e.Description, e.EventType, e.DistanceKm, e.CreatedAt,
            e.Categories.OrderBy(c => c.CategoryName).Select(c => c.ToDto()).ToList());

    public static RouteDto ToDto(this CourseRoute r) =>
        new(r.RouteId, r.CategoryId, r.StartPoint, r.EndPoint, r.RouteDescription, r.MapUrl);

    public static EnrolmentDto ToDto(this EventEnrolment e) =>
        new(e.EnrolmentId, e.ParticipantId, e.Participant?.FullName ?? string.Empty, e.EventId,
            e.Event?.EventName ?? string.Empty, e.CategoryId, e.Category?.CategoryName ?? string.Empty,
            e.EnrolmentDate, e.Status);

    public static ResultDto ToDto(this RaceResult r) =>
        new(r.ResultId, r.EnrolmentId, r.Enrolment?.Event?.EventName ?? string.Empty,
            r.Enrolment?.Category?.CategoryName ?? string.Empty, r.FinishTime, r.Position, r.CapturedAt);
}
