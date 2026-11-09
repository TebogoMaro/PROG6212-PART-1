namespace RaceDay.Api.Tests;

/// <summary>Event management: what an Organiser can do (success) and cannot do (failure).</summary>
public class EventTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EventTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanCreateEvent_AndItCapturesAllRequiredFields()
    {
        var (organiser, user) = await _factory.NewUserAsync(AppRoles.Organiser);
        var request = TestHelpers.NewEventRequest("Cycle");

        var created = await organiser.CreateEventAsync(request);

        Assert.Equal(request.EventName, created.EventName);
        Assert.Equal(request.Description, created.Description);
        Assert.Equal(request.EventDate, created.EventDate);
        Assert.Equal(request.Location, created.Location);
        Assert.Equal(10m, created.DistanceKm);
        Assert.Equal("Cycle", created.EventType);
        Assert.Equal(user.UserId, created.OrganiserId);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_ReturnsBadRequest()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var request = TestHelpers.NewEventRequest("Swim");

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingName_ReturnsBadRequest()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var request = TestHelpers.NewEventRequest();
        request.EventName = "";

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InThePast_ReturnsBadRequest()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var request = TestHelpers.NewEventRequest(daysAhead: -10);

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var created = await organiser.CreateEventAsync();
        var changes = TestHelpers.NewEventRequest("Walk");
        changes.EventName = "Renamed Event";

        var response = await organiser.PutAsJsonAsync($"/api/events/{created.EventId}", changes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Renamed Event", updated!.EventName);
        Assert.Equal("Walk", updated.EventType);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent()
    {
        var (owner, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (otherOrganiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var created = await owner.CreateEventAsync();

        var response = await otherOrganiser.PutAsJsonAsync($"/api/events/{created.EventId}", TestHelpers.NewEventRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_ThenItIsGone()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var created = await organiser.CreateEventAsync();
        await organiser.CreateCategoryAsync(created.EventId);

        var delete = await organiser.DeleteAsync($"/api/events/{created.EventId}");
        var afterwards = await organiser.GetAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent()
    {
        var (owner, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (otherOrganiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var created = await owner.CreateEventAsync();

        var response = await otherOrganiser.DeleteAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_ReturnsConflict()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(category.CategoryId);

        var response = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrDelete_UnknownEvent_ReturnsNotFound()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);

        var put = await organiser.PutAsJsonAsync("/api/events/999999", TestHelpers.NewEventRequest());
        var delete = await organiser.DeleteAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Anyone_CanListAndViewEvents()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var created = await organiser.CreateEventAsync();
        await organiser.CreateCategoryAsync(created.EventId);
        var anonymous = _factory.CreateClient();

        var list = await anonymous.GetFromJsonAsync<List<EventDto>>("/api/events");
        var detail = await anonymous.GetFromJsonAsync<EventDetailDto>($"/api/events/{created.EventId}");

        Assert.Contains(list!, e => e.EventId == created.EventId);
        Assert.Single(detail!.Categories);
    }

    [Fact]
    public async Task ListEvents_CanBeFilteredByType()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var cycle = await organiser.CreateEventAsync(TestHelpers.NewEventRequest("Cycle"));
        var run = await organiser.CreateEventAsync(TestHelpers.NewEventRequest("Run"));

        var list = await _factory.CreateClient().GetFromJsonAsync<List<EventDto>>("/api/events?eventType=Cycle");

        Assert.Contains(list!, e => e.EventId == cycle.EventId);
        Assert.DoesNotContain(list!, e => e.EventId == run.EventId);
        Assert.All(list!, e => Assert.Equal("Cycle", e.EventType));
    }

    [Fact]
    public async Task GetEvent_Unknown_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanManageCategories_IncludingAgeCategoriesWithoutADistance()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();

        var ageCategory = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { CategoryName = "Under 20", MaxParticipants = 50, EntryFee = 80m });
        Assert.Equal(HttpStatusCode.Created, ageCategory.StatusCode);
        var created = (await ageCategory.Content.ReadFromJsonAsync<CategoryDto>())!;
        Assert.Null(created.DistanceKm);

        var update = await organiser.PutAsJsonAsync($"/api/categories/{created.CategoryId}",
            new CategoryRequest { CategoryName = "Under 21", MaxParticipants = 60, EntryFee = 90m });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await _factory.CreateClient().GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventId}/categories");
        Assert.Contains(list!, c => c.CategoryName == "Under 21");

        var delete = await organiser.DeleteAsync($"/api/categories/{created.CategoryId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanSetARoute_AndAnyoneCanReadIt()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var before = await _factory.CreateClient().GetAsync($"/api/categories/{category.CategoryId}/route");
        var put = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryId}/route",
            new RouteRequest { StartPoint = "Mary Fitzgerald Square", EndPoint = "FNB Stadium", RouteDescription = "Flat city route." });
        var after = await _factory.CreateClient().GetFromJsonAsync<RouteDto>($"/api/categories/{category.CategoryId}/route");

        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("FNB Stadium", after!.EndPoint);
    }

    [Fact]
    public async Task Weather_ForExistingEvent_ReturnsForecast()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();

        var weather = await _factory.CreateClient().GetFromJsonAsync<WeatherDto>($"/api/events/{ev.EventId}/weather");

        Assert.True(weather!.IsForecast);
        Assert.Equal("Clear sky", weather.Summary);
    }

    [Fact]
    public async Task Weather_ForUnknownEvent_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
