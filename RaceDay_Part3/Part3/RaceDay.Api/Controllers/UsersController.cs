using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public UsersController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        _db = db;
        _blobs = blobs;
    }

    /// <summary>Returns the profile of the logged-in user.</summary>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return Unauthorized();
        return Ok(user.ToDto());
    }

    /// <summary>Updates the name and email of the logged-in user.</summary>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="409">That email address belongs to another account.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return Unauthorized();

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.UserId != user.UserId))
            return ConflictProblem("That email address is already registered to another account.");

        user.FullName = request.FullName.Trim();
        user.Email = email;
        await _db.SaveChangesAsync();

        await SessionHelper.SignInAsync(HttpContext, user); // refresh the name/email stored in the session
        return Ok(user.ToDto());
    }

    /// <summary>Returns the logged-in Participant's personal results history (newest first).</summary>
    /// <response code="200">The participant's results.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants have a results history.</response>
    [HttpGet("me/results")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ResultDto>>> GetMyResults()
    {
        var userId = CurrentUserId;
        var results = await _db.Results
            .Where(r => r.Enrolment!.ParticipantId == userId)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category)
            .OrderByDescending(r => r.CapturedAt)
            .ToListAsync();

        // How many people finished each of these events (for "47th out of 312 finishers").
        var eventIds = results.Select(r => r.Enrolment!.EventId).Distinct().ToList();
        var counts = await _db.Results
            .Where(r => eventIds.Contains(r.Enrolment!.EventId))
            .GroupBy(r => r.Enrolment!.EventId)
            .Select(g => new { EventId = g.Key, Total = g.Count() })
            .ToListAsync();
        var totals = counts.ToDictionary(c => c.EventId, c => c.Total);

        return Ok(results.Select(r => r.ToDto(totals.GetValueOrDefault(r.Enrolment!.EventId))).ToList());
    }

    /// <summary>Uploads (or replaces) the logged-in user's profile picture. Stored in Azure Blob Storage.</summary>
    /// <remarks>Send the file as multipart/form-data in a field called "file". JPG, PNG or WebP, up to 5 MB.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">No file, wrong file type, or file too large.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="502">Image storage is unavailable.</response>
    [HttpPost("me/picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UserDto>> UploadPicture(IFormFile file, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return Unauthorized();

        var (extension, error) = await ImageValidator.ValidateAsync(file);
        if (extension is null) return BadRequestProblem(error!);

        var blobName = $"profiles/{user.UserId}/{Guid.NewGuid():N}{extension}";
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

        var oldBlob = user.ProfilePictureBlob;
        user.ProfilePictureBlob = blobName;
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(oldBlob))
        {
            try { await _blobs.DeleteAsync(oldBlob, cancellationToken); }
            catch { /* the old image is just orphaned - not worth failing the request */ }
        }

        return Ok(user.ToDto());
    }

    /// <summary>Downloads the logged-in user's profile picture.</summary>
    /// <response code="200">The image file.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The user has not uploaded a picture.</response>
    /// <response code="502">Image storage is unavailable.</response>
    [HttpGet("me/picture")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetPicture(CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return Unauthorized();
        if (string.IsNullOrEmpty(user.ProfilePictureBlob)) return NotFound();

        BlobDownload? file;
        try
        {
            file = await _blobs.DownloadAsync(user.ProfilePictureBlob, cancellationToken);
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
