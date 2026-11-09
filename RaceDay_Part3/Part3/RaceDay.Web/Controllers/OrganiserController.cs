using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleAuthorize(AppRoles.Organiser)]
public class OrganiserController : AppController
{
    public OrganiserController(RaceDayApiClient api) : base(api) { }

    // ======================= Dashboard and event list =======================

    public async Task<IActionResult> Dashboard()
    {
        var result = await Api.GetMyEventsAsync();
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success) ViewData["LoadError"] = result.Error;
        return View(result.Data ?? new List<MyEventDto>());
    }

    public async Task<IActionResult> Events()
    {
        var result = await Api.GetMyEventsAsync();
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success) ViewData["LoadError"] = result.Error;
        return View(result.Data ?? new List<MyEventDto>());
    }

    // ======================= Create / edit / delete event =======================

    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new EventFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormModel model)
    {
        if (model.EventDate is not null && DateOnly.FromDateTime(model.EventDate.Value) < DateOnly.FromDateTime(DateTime.UtcNow))
            ModelState.AddModelError(nameof(model.EventDate), "The event date can't be in the past.");
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await Api.CreateEventAsync(model);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success || result.Data is null)
        {
            AddApiError(result);
            return View("EventForm", model);
        }

        var created = result.Data;
        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadBannerAsync(created.EventId, model.Banner);
            if (!upload.Success)
            {
                Failure($"The event was created, but the banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(Categories), new { eventId = created.EventId });
            }
        }

        Success("Event created. Now add the categories people can enter.");
        return RedirectToAction(nameof(Categories), new { eventId = created.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var (ev, problem) = await LoadOwnedEventAsync(id);
        if (problem is not null) return problem;

        return View("EventForm", new EventFormModel
        {
            EventId = ev!.EventId,
            EventName = ev.EventName,
            EventDate = ev.EventDate.ToDateTime(TimeOnly.MinValue),
            Location = ev.Location,
            Description = ev.Description,
            EventType = ev.EventType,
            DistanceKm = ev.DistanceKm,
            HasBanner = ev.HasBanner
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EventFormModel model)
    {
        model.EventId = id;
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await Api.UpdateEventAsync(id, model);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success)
        {
            AddApiError(result);
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadBannerAsync(id, model.Banner);
            if (!upload.Success)
            {
                Failure($"The event was saved, but the banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(Events));
            }
        }

        Success("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(int id)
    {
        var (ev, problem) = await LoadOwnedEventAsync(id);
        if (problem is not null) return problem;
        return View("DeleteEvent", ev);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await Api.DeleteEventAsync(id);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (result.Success) Success("The event has been deleted.");
        else Failure(result.Error ?? "The event could not be deleted.");
        return RedirectToAction(nameof(Events));
    }

    // ======================= Categories and routes =======================

    [HttpGet]
    public async Task<IActionResult> Categories(int eventId)
    {
        var (ev, problem) = await LoadOwnedEventAsync(eventId);
        if (problem is not null) return problem;

        return View(new CategoriesViewModel { Event = ev!, NewCategory = new CategoryFormModel { EventId = eventId } });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(CategoryFormModel model)
    {
        if (!ModelState.IsValid)
        {
            var (ev, problem) = await LoadOwnedEventAsync(model.EventId);
            if (problem is not null) return problem;
            return View("Categories", new CategoriesViewModel { Event = ev!, NewCategory = model });
        }

        var result = await Api.CreateCategoryAsync(model.EventId, model);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (result.Success) Success($"Category \"{model.CategoryName}\" added.");
        else Failure(result.Error ?? "The category could not be added.");
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id)
    {
        var result = await Api.GetCategoryAsync(id);
        if (result.StatusCode == 404) return NotFound();
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;
        if (!result.Success || result.Data is null)
        {
            Failure(result.Error ?? "The category could not be loaded.");
            return RedirectToAction(nameof(Events));
        }

        var (ev, problem) = await LoadOwnedEventAsync(result.Data.EventId);
        if (problem is not null) return problem;

        ViewData["EventName"] = ev!.EventName;
        var c = result.Data;
        return View("CategoryForm", new CategoryFormModel
        {
            CategoryId = c.CategoryId,
            EventId = c.EventId,
            CategoryName = c.CategoryName,
            DistanceKm = c.DistanceKm,
            MaxParticipants = c.MaxParticipants,
            EntryFee = c.EntryFee
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, CategoryFormModel model)
    {
        model.CategoryId = id;
        if (!ModelState.IsValid) return View("CategoryForm", model);

        var result = await Api.UpdateCategoryAsync(id, model);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success)
        {
            AddApiError(result);
            return View("CategoryForm", model);
        }

        Success("Category updated.");
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        var result = await Api.DeleteCategoryAsync(id);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (result.Success) Success("The category has been deleted.");
        else Failure(result.Error ?? "The category could not be deleted.");
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    [HttpGet]
    public async Task<IActionResult> CategoryRoute(int categoryId, int eventId)
    {
        var (ev, problem) = await LoadOwnedEventAsync(eventId);
        if (problem is not null) return problem;

        var category = ev!.Categories.FirstOrDefault(c => c.CategoryId == categoryId);
        if (category is null) return NotFound();

        ViewData["Heading"] = $"{ev.EventName} - {category.CategoryName}";
        var existing = await Api.GetRouteAsync(categoryId);
        var model = new RouteFormModel { CategoryId = categoryId, EventId = eventId };
        if (existing.Success && existing.Data is not null)
        {
            model.StartPoint = existing.Data.StartPoint;
            model.EndPoint = existing.Data.EndPoint;
            model.RouteDescription = existing.Data.RouteDescription;
            model.MapUrl = existing.Data.MapUrl;
        }
        return View("Route", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryRoute(RouteFormModel model)
    {
        if (!ModelState.IsValid) return View("Route", model);

        var result = await Api.PutRouteAsync(model.CategoryId, model);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success)
        {
            AddApiError(result);
            return View("Route", model);
        }

        Success("Route saved. Participants can now see it on the event page.");
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    // ======================= Enrolments and results =======================

    [HttpGet]
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var (ev, problem) = await LoadOwnedEventAsync(eventId);
        if (problem is not null) return problem;

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        if (!enrolments.Success) ViewData["LoadError"] = enrolments.Error;

        return View(new EnrolmentsViewModel { Event = ev!, Enrolments = enrolments.Data ?? new List<EnrolmentDto>() });
    }

    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var (ev, problem) = await LoadOwnedEventAsync(eventId);
        if (problem is not null) return problem;

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        if (!enrolments.Success) ViewData["LoadError"] = enrolments.Error;

        return View(new EnrolmentsViewModel { Event = ev!, Enrolments = enrolments.Data ?? new List<EnrolmentDto>() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResult(int eventId, int enrolmentId, int? resultId, string? finishTime, int? position)
    {
        // The finish time must look like 00:48:32 (hours:minutes:seconds).
        if (string.IsNullOrWhiteSpace(finishTime) || !Regex.IsMatch(finishTime.Trim(), @"^\d{1,2}:[0-5]\d:[0-5]\d$")
            || !TimeSpan.TryParse(finishTime.Trim(), CultureInfo.InvariantCulture, out var time) || time <= TimeSpan.Zero)
        {
            Failure("Please enter the finish time as hours:minutes:seconds, for example 00:48:32.");
            return RedirectToAction(nameof(Results), new { eventId });
        }

        if (position is < 1)
        {
            Failure("The finishing position must be 1 or higher.");
            return RedirectToAction(nameof(Results), new { eventId });
        }

        var result = resultId is null
            ? await Api.CaptureResultAsync(enrolmentId, time, position)
            : await Api.UpdateResultAsync(resultId.Value, time, position);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (result.Success) Success("Result saved - the participant can now see it.");
        else Failure(result.Error ?? "The result could not be saved.");
        return RedirectToAction(nameof(Results), new { eventId });
    }

    // ======================= Helpers =======================

    /// <summary>Loads an event and makes sure it belongs to the logged-in Organiser (the API enforces this too).</summary>
    private async Task<(EventDetailDto? Event, IActionResult? Problem)> LoadOwnedEventAsync(int eventId)
    {
        var result = await Api.GetEventAsync(eventId);
        if (result.StatusCode == 404) return (null, NotFound());

        var expired = ExpiredSession(result);
        if (expired is not null) return (null, expired);

        if (!result.Success || result.Data is null)
        {
            Failure(result.Error ?? "The event could not be loaded.");
            return (null, RedirectToAction(nameof(Events)));
        }

        if (result.Data.OrganiserId != CurrentUser!.UserId)
            return (null, RedirectToAction("AccessDenied", "Home"));

        return (result.Data, null);
    }
}
