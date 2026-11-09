using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IWeatherService _weather;
    private readonly IBlobStorageService _blobs;

    public EventsController(RaceDayDbContext db, IWeatherService weather, IBlobStorageService blobs)
    {
        _db = db;
        _weather = weather;
        _blobs = blobs;
    }

    /// <summary>Lists events. By default only upcoming events are returned.</summary>
    /// <remarks>Optional filters: location (contains), eventType (Run/Walk/Cycle), fromDate, toDate, includePast.</remarks>
    /// <response code="200">A list of events, soonest first.</response>
    /// <response code="400">An invalid eventType filter was supplied.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<EventDto>>> GetEvents(
        [FromQuery] string? location,
        [FromQuery] string? eventType,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] bool includePast = false)
    {
        var query = _db.Events.Include(e => e.Organiser).AsQueryable();

        if (fromDate is not null)
        {
            var start = fromDate.Value;
            query = query.Where(e => e.EventDate >= start);
        }
        else if (!includePast)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(e => e.EventDate >= today);
        }

        if (toDate is not null)
        {
            var end = toDate.Value;
            query = query.Where(e => e.EventDate <= end);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim();
            query = query.Where(e => e.Location.Contains(loc));
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = EventTypes.Normalise(eventType);
            if (type is null) return BadRequestProblem("eventType must be Run, Walk or Cycle.");
            query = query.Where(e => e.EventType == type);
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Returns full details for one event, including its categories.</summary>
    /// <response code="200">The event and its categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}", Name = "GetEventById")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> GetEvent(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null) return NotFound();
        return Ok(ev.ToDetailDto());
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed (missing fields, invalid type, date in the past).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Organisers can create events.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> CreateEvent(EventRequest request)
    {
        var type = EventTypes.Normalise(request.EventType);
        if (type is null) return BadRequestProblem("eventType must be Run, Walk or Cycle.");

        var eventDate = request.EventDate!.Value;
        if (eventDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequestProblem("The event date cannot be in the past.");

        var userId = CurrentUserId;
        var ev = new Event
        {
            OrganiserId = userId,
            EventName = request.EventName.Trim(),
            EventDate = eventDate,
            Location = request.Location.Trim(),
            Description = request.Description?.Trim(),
            EventType = type,
            DistanceKm = request.DistanceKm!.Value
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        ev.Organiser = await _db.Users.FindAsync(userId);

        return CreatedAtRoute("GetEventById", new { id = ev.EventId }, ev.ToDto());
    }

    /// <summary>Updates an event. Only the Organiser who owns the event may do this.</summary>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UpdateEvent(int id, EventRequest request)
    {
        var type = EventTypes.Normalise(request.EventType);
        if (type is null) return BadRequestProblem("eventType must be Run, Walk or Cycle.");

        var ev = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only edit your own events.");

        ev.EventName = request.EventName.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventType = type;
        ev.DistanceKm = request.DistanceKm!.Value;

        await _db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Deletes an event together with its categories and routes.</summary>
    /// <remarks>An event that already has enrolments cannot be deleted.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">The event has enrolments and cannot be deleted.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories).ThenInclude(c => c.Route)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete your own events.");
        if (await _db.EventEnrolments.AnyAsync(e => e.EventId == id))
            return ConflictProblem("This event already has enrolments and cannot be deleted.");

        foreach (var category in ev.Categories)
        {
            if (category.Route is not null) _db.Routes.Remove(category.Route);
        }
        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Returns the weather forecast for the event's location and date.</summary>
    /// <remarks>Uses the free Open-Meteo service. Daily forecasts are available up to 16 days ahead.</remarks>
    /// <response code="200">The forecast (or a note explaining why none is available yet).</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="502">The weather provider could not be reached.</response>
    [HttpGet("{id:int}/weather")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WeatherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<WeatherDto>> GetWeather(int id, CancellationToken cancellationToken)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id, cancellationToken);
        if (ev is null) return NotFound();

        try
        {
            var forecast = await _weather.GetForecastAsync(ev.Location, ev.EventDate, cancellationToken);
            return Ok(forecast);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Problem(title: "Bad gateway", detail: "The weather provider is currently unavailable.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>Lists the categories available for an event.</summary>
    /// <response code="200">The event's categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == id)) return NotFound();

        var categories = await _db.Categories
            .Where(c => c.EventId == id)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        return Ok(categories.Select(c => c.ToDto()).ToList());
    }

    /// <summary>Adds a category (for example 10km or Under 20) to an event.</summary>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPost("{id:int}/categories")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(int id, CategoryRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only add categories to your own events.");

        var category = new Category
        {
            EventId = id,
            CategoryName = request.CategoryName.Trim(),
            DistanceKm = request.DistanceKm,
            MaxParticipants = request.MaxParticipants,
            EntryFee = request.EntryFee
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtRoute("GetCategoryById", new { id = category.CategoryId }, category.ToDto());
    }

    /// <summary>Lists everyone enrolled in an event (Organiser who owns the event only).</summary>
    /// <response code="200">The event's enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}/enrolments")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentDto>>> GetEnrolments(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only view enrolments for your own events.");

        var enrolments = await _db.EventEnrolments
            .Where(e => e.EventId == id)
            .Include(e => e.Participant)
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Include(e => e.Result)
            .OrderBy(e => e.EnrolmentDate)
            .ToListAsync();

        return Ok(enrolments.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Lists the events created by the logged-in Organiser, with enrolment counts (the dashboard).</summary>
    /// <response code="200">The organiser's events, soonest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Organisers have a dashboard.</response>
    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(List<MyEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<MyEventDto>>> GetMyEvents()
    {
        var userId = CurrentUserId;
        var events = await _db.Events
            .Where(e => e.OrganiserId == userId)
            .Include(e => e.Categories)
            .Include(e => e.Enrolments)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return Ok(events.Select(e => new MyEventDto(
            e.EventId, e.EventName, e.EventDate, e.Location, e.EventType, e.DistanceKm,
            !string.IsNullOrEmpty(e.BannerImageBlob), e.Categories.Count,
            e.Enrolments.Count(x => x.Status != EnrolmentStatus.Cancelled))).ToList());
    }

    /// <summary>Uploads (or replaces) the banner image of an event. The image is stored in Azure Blob Storage.</summary>
    /// <remarks>Send the file as multipart/form-data in a field called "file". JPG, PNG or WebP, up to 5 MB.</remarks>
    /// <response code="200">The updated event.</response>
    /// <response code="400">No file, wrong file type, or file too large.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="502">Image storage is unavailable.</response>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = AppRoles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<EventDto>> UploadBanner(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var ev = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id, cancellationToken);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only change the banner of your own events.");

        var (extension, error) = await ImageValidator.ValidateAsync(file);
        if (extension is null) return BadRequestProblem(error!);

        var blobName = $"events/{id}/{Guid.NewGuid():N}{extension}";
        try
        {
            await using var stream = file.OpenReadStream();
            await _blobs.UploadAsync(blobName, stream, ImageValidator.ContentTypeFor(extension), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problem(title: "Bad gateway", detail: "Image storage is currently unavailable.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var oldBlob = ev.BannerImageBlob;
        ev.BannerImageBlob = blobName;
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(oldBlob))
        {
            try { await _blobs.DeleteAsync(oldBlob, cancellationToken); }
            catch { /* the old image is just orphaned - not worth failing the request */ }
        }

        return Ok(ev.ToDto());
    }

    /// <summary>Downloads the banner image of an event.</summary>
    /// <response code="200">The image file.</response>
    /// <response code="404">The event does not exist or has no banner.</response>
    /// <response code="502">Image storage is unavailable.</response>
    [HttpGet("{id:int}/banner")]
    [AllowAnonymous]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetBanner(int id, CancellationToken cancellationToken)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id, cancellationToken);
        if (ev is null || string.IsNullOrEmpty(ev.BannerImageBlob)) return NotFound();

        BlobDownload? file;
        try
        {
            file = await _blobs.DownloadAsync(ev.BannerImageBlob, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problem(title: "Bad gateway", detail: "Image storage is currently unavailable.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        if (file is null) return NotFound();
        return File(file.Content, file.ContentType);
    }
}
