using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Returns one category.</summary>
    /// <response code="200">The category.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpGet("{id:int}", Name = "GetCategoryById")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return NotFound();
        return Ok(category.ToDto());
    }

    /// <summary>Updates a category (Organiser who owns the event only).</summary>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound();
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only manage categories for your own events.");

        category.CategoryName = request.CategoryName.Trim();
        category.DistanceKm = request.DistanceKm;
        category.MaxParticipants = request.MaxParticipants;
        category.EntryFee = request.EntryFee;

        await _db.SaveChangesAsync();
        return Ok(category.ToDto());
    }

    /// <summary>Deletes a category (and its route). Not allowed once people have enrolled.</summary>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">The category already has enrolments.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Route)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null) return NotFound();
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only manage categories for your own events.");
        if (await _db.EventEnrolments.AnyAsync(e => e.CategoryId == id))
            return ConflictProblem("This category already has enrolments and cannot be deleted.");

        if (category.Route is not null) _db.Routes.Remove(category.Route);
        _db.Categories.Remove(category);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Returns the route (start, end, description, map link) for a category.</summary>
    /// <response code="200">The route.</response>
    /// <response code="404">The category does not exist, or it has no route yet.</response>
    [HttpGet("{id:int}/route")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> GetRoute(int id)
    {
        var category = await _db.Categories.Include(c => c.Route).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category?.Route is null) return NotFound();
        return Ok(category.Route.ToDto());
    }

    /// <summary>Creates or replaces the route of a category (Organiser who owns the event only).</summary>
    /// <response code="200">The saved route.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpPut("{id:int}/route")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> PutRoute(int id, RouteRequest request)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Route)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null) return NotFound();
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only manage routes for your own events.");

        var route = category.Route ?? new CourseRoute();
        route.StartPoint = request.StartPoint.Trim();
        route.EndPoint = request.EndPoint.Trim();
        route.RouteDescription = request.RouteDescription?.Trim();
        route.MapUrl = request.MapUrl?.Trim();
        category.Route = route;

        await _db.SaveChangesAsync();
        return Ok(route.ToDto());
    }

    /// <summary>Enrols the logged-in Participant in this category (and therefore in its event).</summary>
    /// <remarks>
    /// The enrolment records the participant, the event and the chosen category. A participant can enter
    /// an event once; a cancelled enrolment can be re-activated by enrolling again.
    /// </remarks>
    /// <response code="201">Enrolment created.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants can enrol.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">Already enrolled, the category is full, or the event has already taken place.</response>
    [HttpPost("{id:int}/enrolments")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound();

        var userId = CurrentUserId;
        var ev = category.Event!;

        if (ev.EventDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return ConflictProblem("This event has already taken place.");

        var existing = await _db.EventEnrolments
            .FirstOrDefaultAsync(e => e.ParticipantId == userId && e.EventId == ev.EventId);

        if (existing is not null && existing.Status != EnrolmentStatus.Cancelled)
            return ConflictProblem("You are already enrolled in this event.");

        var taken = await _db.EventEnrolments
            .CountAsync(e => e.CategoryId == id && e.Status != EnrolmentStatus.Cancelled);
        if (taken >= category.MaxParticipants)
            return ConflictProblem("This category is full.");

        EventEnrolment enrolment;
        if (existing is not null)
        {
            // Re-activate a previously cancelled enrolment (one enrolment per participant per event).
            enrolment = existing;
            enrolment.CategoryId = id;
            enrolment.Status = EnrolmentStatus.Confirmed;
            enrolment.EnrolmentDate = DateTime.UtcNow;
        }
        else
        {
            enrolment = new EventEnrolment
            {
                ParticipantId = userId,
                EventId = ev.EventId,
                CategoryId = id,
                Status = EnrolmentStatus.Confirmed
            };
            _db.EventEnrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync();

        enrolment.Participant = await _db.Users.FindAsync(userId);
        enrolment.Event = ev;
        enrolment.Category = category;

        return StatusCode(StatusCodes.Status201Created, enrolment.ToDto());
    }
}
