using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers for every controller.</summary>
public abstract class AppController : Controller
{
    protected RaceDayApiClient Api { get; }

    protected AppController(RaceDayApiClient api) => Api = api;

    protected SessionUser? CurrentUser => HttpContext.Session.GetUser();

    protected void Success(string message) => TempData["Success"] = message;

    protected void Failure(string message) => TempData["Error"] = message;

    protected void AddApiError<T>(ApiResult<T> result) =>
        ModelState.AddModelError(string.Empty, result.Error ?? "Something went wrong. Please try again.");

    /// <summary>If the API no longer recognises the session, end the web session and send the user to log in again.</summary>
    protected IActionResult? ExpiredSession<T>(ApiResult<T> result)
    {
        if (result.StatusCode != 401) return null;

        HttpContext.Session.Clear();
        Failure("Your session has ended. Please log in again.");
        return RedirectToAction("Login", "Account");
    }
}
