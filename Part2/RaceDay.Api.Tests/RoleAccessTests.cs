namespace RaceDay.Api.Tests;

/// <summary>Role-based access: the wrong role (or no login at all) must be rejected.</summary>
public class RoleAccessTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public RoleAccessTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_CannotCreateEvent_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/events", TestHelpers.NewEventRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_ReturnsForbidden()
    {
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var response = await participant.PostAsJsonAsync("/api/events", TestHelpers.NewEventRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotEditOrDeleteEvents_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();

        var put = await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.NewEventRequest());
        var delete = await participant.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAddCategories_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { CategoryName = "Sneaky", MaxParticipants = 10 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var response = await organiser.PostAsync($"/api/categories/{category.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotEnrol_ReturnsUnauthorized()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var response = await _factory.CreateClient().PostAsync($"/api/categories/{category.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();

        var response = await participant.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewAnotherOrganisersEnrolments_ReturnsForbidden()
    {
        var (owner, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (otherOrganiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await owner.CreateEventAsync();

        var response = await otherOrganiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);

        var response = await participant.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 40, 0), Position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantOnlyEndpoints_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);

        var enrolments = await organiser.GetAsync("/api/enrolments/me");
        var results = await organiser.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Forbidden, enrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, results.StatusCode);
    }
}
