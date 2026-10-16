using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/enrolments")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Lists all enrolments (past and upcoming) of the logged-in Participant.</summary>
    /// <response code="200">The participant's enrolments, newest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants have enrolments.</response>
    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EnrolmentDto>>> GetMine()
    {
        var userId = CurrentUserId;
        var enrolments = await _db.EventEnrolments
            .Where(e => e.ParticipantId == userId)
            .Include(e => e.Participant)
            .Include(e => e.Event)
            .Include(e => e.Category)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToListAsync();

        return Ok(enrolments.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Cancels the logged-in Participant's own enrolment.</summary>
    /// <remarks>The enrolment is kept with the status "Cancelled". An enrolment that already has a result cannot be cancelled.</remarks>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or not your enrolment.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.EventEnrolments.Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.ParticipantId != CurrentUserId) return ForbiddenProblem("You can only cancel your own enrolments.");
        if (enrolment.Result is not null) return ConflictProblem("A result has already been captured for this enrolment.");

        enrolment.Status = EnrolmentStatus.Cancelled;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Captures the finish time and position for an enrolment (Organiser who owns the event only).</summary>
    /// <response code="201">Result captured.</response>
    /// <response code="400">Validation failed (for example a finish time of zero).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">A result already exists, or the enrolment was cancelled.</response>
    [HttpPost("{id:int}/results")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> CaptureResult(int id, ResultRequest request)
    {
        var enrolment = await _db.EventEnrolments
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return NotFound();
        if (enrolment.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only capture results for your own events.");
        if (enrolment.Status == EnrolmentStatus.Cancelled) return ConflictProblem("This enrolment was cancelled.");
        if (enrolment.Result is not null) return ConflictProblem("A result has already been captured for this enrolment.");

        var finishTime = request.FinishTime!.Value;
        if (finishTime <= TimeSpan.Zero || finishTime >= TimeSpan.FromHours(24))
            return BadRequestProblem("finishTime must be greater than 00:00:00 and less than 24 hours.");

        var result = new RaceResult
        {
            Enrolment = enrolment,
            FinishTime = finishTime,
            Position = request.Position
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetResult), new { id = enrolment.EnrolmentId }, result.ToDto());
    }

    /// <summary>Returns the result for an enrolment.</summary>
    /// <remarks>A Participant can only see their own result; an Organiser only results for their own events.</remarks>
    /// <response code="200">The result.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">This result belongs to someone else.</response>
    /// <response code="404">The enrolment does not exist, or no result has been captured yet.</response>
    [HttpGet("{id:int}/results")]
    [Authorize]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> GetResult(int id)
    {
        var enrolment = await _db.EventEnrolments
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return NotFound();

        var userId = CurrentUserId;
        var allowed = User.IsInRole(AppRoles.Organiser)
            ? enrolment.Event!.OrganiserId == userId
            : enrolment.ParticipantId == userId;
        if (!allowed) return ForbiddenProblem("You can only view results for your own enrolments or events.");

        if (enrolment.Result is null) return NotFound();

        enrolment.Result.Enrolment = enrolment;
        return Ok(enrolment.Result.ToDto());
    }
}
