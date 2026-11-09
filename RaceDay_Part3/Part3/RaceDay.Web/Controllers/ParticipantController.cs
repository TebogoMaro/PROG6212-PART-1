using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleAuthorize(AppRoles.Participant)]
public class ParticipantController : AppController
{
    public ParticipantController(RaceDayApiClient api) : base(api) { }

    /// <summary>Enrol in an event by choosing one of its categories. The API records it immediately.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        var result = await Api.EnrolAsync(categoryId);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success)
        {
            Failure(result.Error ?? "You could not be enrolled. Please try again.");
            return RedirectToAction("Details", "Home", new { id = eventId });
        }

        Success($"You're in! You are enrolled for {result.Data?.EventName} ({result.Data?.CategoryName}).");
        return RedirectToAction(nameof(MyEnrolments));
    }

    public async Task<IActionResult> MyEnrolments()
    {
        var result = await Api.GetMyEnrolmentsAsync();
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success) ViewData["LoadError"] = result.Error;
        return View(result.Data ?? new List<EnrolmentDto>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await Api.CancelEnrolmentAsync(id);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (result.Success) Success("Your enrolment has been cancelled.");
        else Failure(result.Error ?? "The enrolment could not be cancelled.");
        return RedirectToAction(nameof(MyEnrolments));
    }

    public async Task<IActionResult> MyResults()
    {
        var result = await Api.GetMyResultsAsync();
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success) ViewData["LoadError"] = result.Error;
        return View(result.Data ?? new List<ResultDto>());
    }
}
