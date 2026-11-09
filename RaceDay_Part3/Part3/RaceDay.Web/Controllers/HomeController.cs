using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    public HomeController(RaceDayApiClient api) : base(api) { }

    /// <summary>Participants and visitors: upcoming events with filters. Organisers go to their dashboard.</summary>
    public async Task<IActionResult> Index(string? location, string? eventType, DateOnly? fromDate, DateOnly? toDate)
    {
        if (CurrentUser?.Role == AppRoles.Organiser)
            return RedirectToAction("Dashboard", "Organiser");

        var result = await Api.GetEventsAsync(location, eventType, fromDate, toDate);
        if (!result.Success) ViewData["LoadError"] = result.Error;

        return View(new HomeIndexViewModel
        {
            Events = result.Data ?? new List<EventDto>(),
            Location = location,
            EventType = eventType,
            FromDate = fromDate,
            ToDate = toDate
        });
    }

    /// <summary>Full event information, categories, weather and routes.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var eventResult = await Api.GetEventAsync(id);
        if (eventResult.StatusCode == 404) return NotFound();
        if (!eventResult.Success || eventResult.Data is null)
        {
            Failure(eventResult.Error ?? "That event could not be loaded.");
            return RedirectToAction(nameof(Index));
        }

        var model = new EventDetailsViewModel { Event = eventResult.Data };

        // Nice-to-have extras: if any of these fail the page still works.
        var weather = await Api.GetWeatherAsync(id);
        if (weather.Success) model.Weather = weather.Data;

        foreach (var category in model.Event.Categories)
        {
            var route = await Api.GetRouteAsync(category.CategoryId);
            if (route.Success && route.Data is not null) model.Routes[category.CategoryId] = route.Data;
        }

        if (CurrentUser?.Role == AppRoles.Participant)
        {
            var mine = await Api.GetMyEnrolmentsAsync();
            var expired = ExpiredSession(mine);
            if (expired is not null) return expired;
            model.MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id && e.Status != "Cancelled");
        }

        return View(model);
    }

    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    /// <summary>Friendly page for 404 / 403 / 500 etc. (set up with UseStatusCodePagesWithReExecute).</summary>
    public IActionResult StatusPage(int code)
    {
        ViewData["Code"] = code;
        return View();
    }
}
