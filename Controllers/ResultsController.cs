using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/results")]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Corrects a previously captured result (Organiser who owns the event only).</summary>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The result does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> UpdateResult(int id, ResultRequest request)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e!.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category)
            .FirstOrDefaultAsync(r => r.ResultId == id);

        if (result is null) return NotFound();
        if (result.Enrolment!.Event!.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only correct results for your own events.");

        var finishTime = request.FinishTime!.Value;
        if (finishTime <= TimeSpan.Zero || finishTime >= TimeSpan.FromHours(24))
            return BadRequestProblem("finishTime must be greater than 00:00:00 and less than 24 hours.");

        result.FinishTime = finishTime;
        result.Position = request.Position;

        await _db.SaveChangesAsync();
        return Ok(result.ToDto());
    }
}
