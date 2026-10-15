using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Registers a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// The role must be "Organiser" or "Participant". Organisers must also supply the organiser
    /// registration code, so nobody can simply promote themselves. The password is hashed before it is stored.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user (never the password).</response>
    /// <response code="400">Validation failed (missing fields, weak password, invalid role).</response>
    /// <response code="403">Organiser registration code missing or incorrect.</response>
    /// <response code="409">That email address is already registered.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request)
    {
        var role = AppRoles.Normalise(request.Role);
        if (role is null)
        {
            ModelState.AddModelError(nameof(request.Role), "Role must be 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }

        if (role == AppRoles.Organiser)
        {
            var expectedCode = _config["Registration:OrganiserCode"];
            if (string.IsNullOrWhiteSpace(expectedCode) || request.OrganiserCode != expectedCode)
                return ForbiddenProblem("A valid organiser registration code is required to register as an Organiser.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return ConflictProblem("That email address is already registered.");

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHelper.Hash(request.Password),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, user.ToDto());
    }

    /// <summary>Logs in and starts a session.</summary>
    /// <remarks>
    /// On success the API sets a secure, HTTP-only session cookie that keeps the user's authenticated
    /// state and role for all subsequent requests (Swagger UI sends it automatically).
    /// </remarks>
    /// <response code="200">Logged in. Returns the user's details.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !PasswordHelper.Verify(request.Password, user.PasswordHash))
            return Problem(title: "Unauthorized", detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

        await SessionHelper.SignInAsync(HttpContext, user);
        return Ok(user.ToDto());
    }

    /// <summary>Logs out and ends the current session.</summary>
    /// <response code="204">Session ended.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
