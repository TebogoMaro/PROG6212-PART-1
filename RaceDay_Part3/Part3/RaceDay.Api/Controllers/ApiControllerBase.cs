using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers for all RaceDay controllers.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The id of the logged-in user (read from the session cookie).</summary>
    protected int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? throw new InvalidOperationException("No authenticated user."));

    protected ObjectResult BadRequestProblem(string detail) =>
        Problem(title: "Bad request", detail: detail, statusCode: StatusCodes.Status400BadRequest);

    protected ObjectResult ForbiddenProblem(string detail) =>
        Problem(title: "Forbidden", detail: detail, statusCode: StatusCodes.Status403Forbidden);

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(title: "Conflict", detail: detail, statusCode: StatusCodes.Status409Conflict);
}
