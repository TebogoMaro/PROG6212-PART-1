namespace RaceDay.Api.Tests;

/// <summary>The Organiser dashboard and the richer enrolment/result data the web app needs.</summary>
public class DashboardTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public DashboardTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_ListsOnlyMyEvents_WithEnrolmentCounts()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (otherOrganiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var busy = await organiser.CreateEventAsync();
        var quiet = await organiser.CreateEventAsync();
        var someoneElses = await otherOrganiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(busy.EventId);
        await participant.EnrolAsync(category.CategoryId);

        var mine = await organiser.GetFromJsonAsync<List<MyEventDto>>("/api/events/mine");

        Assert.Equal(2, mine!.Count);
        Assert.DoesNotContain(mine, e => e.EventId == someoneElses.EventId);
        Assert.Equal(1, mine.Single(e => e.EventId == busy.EventId).EnrolmentCount);
        Assert.Equal(1, mine.Single(e => e.EventId == busy.EventId).CategoryCount);
        Assert.Equal(0, mine.Single(e => e.EventId == quiet.EventId).EnrolmentCount);
    }

    [Fact]
    public async Task Dashboard_DoesNotCountCancelledEnrolments()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);
        await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        var mine = await organiser.GetFromJsonAsync<List<MyEventDto>>("/api/events/mine");

        Assert.Equal(0, mine!.Single(e => e.EventId == ev.EventId).EnrolmentCount);
    }

    [Fact]
    public async Task Dashboard_IsForOrganisersOnly()
    {
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var asParticipant = await participant.GetAsync("/api/events/mine");
        var anonymous = await _factory.CreateClient().GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Enrolments_IncludeTheEventDate()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(category.CategoryId);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/me");

        Assert.Equal(ev.EventDate, mine!.Single().EventDate);
    }

    [Fact]
    public async Task OrganiserEnrolmentList_ShowsCapturedResults()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 44, 10), Position = 3 });

        var list = await organiser.GetFromJsonAsync<List<EnrolmentDto>>($"/api/events/{ev.EventId}/enrolments");

        var row = list!.Single();
        Assert.NotNull(row.ResultId);
        Assert.Equal(new TimeSpan(0, 44, 10), row.FinishTime);
        Assert.Equal(3, row.Position);
    }

    [Fact]
    public async Task ResultsHistory_ShowsEventDateAndTotalFinishers()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (first, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var (second, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var firstEnrolment = await first.EnrolAsync(category.CategoryId);
        var secondEnrolment = await second.EnrolAsync(category.CategoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{firstEnrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 40, 0), Position = 1 });
        await organiser.PostAsJsonAsync($"/api/enrolments/{secondEnrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 50, 0), Position = 2 });

        var history = await second.GetFromJsonAsync<List<ResultDto>>("/api/users/me/results");

        var result = history!.Single();
        Assert.Equal(2, result.TotalFinishers);
        Assert.Equal(2, result.Position);
        Assert.Equal(ev.EventDate, result.EventDate);
        Assert.Equal(ev.EventName, result.EventName);
    }

    [Fact]
    public async Task ResultsHistory_IsNewestFirst()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var firstEvent = await organiser.CreateEventAsync();
        var secondEvent = await organiser.CreateEventAsync();
        var firstCategory = await organiser.CreateCategoryAsync(firstEvent.EventId);
        var secondCategory = await organiser.CreateCategoryAsync(secondEvent.EventId);
        var firstEnrolment = await participant.EnrolAsync(firstCategory.CategoryId);
        var secondEnrolment = await participant.EnrolAsync(secondCategory.CategoryId);

        await organiser.PostAsJsonAsync($"/api/enrolments/{firstEnrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 45, 0) });
        await Task.Delay(50);
        await organiser.PostAsJsonAsync($"/api/enrolments/{secondEnrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 46, 0) });

        var history = await participant.GetFromJsonAsync<List<ResultDto>>("/api/users/me/results");

        Assert.Equal(secondEnrolment.EnrolmentId, history![0].EnrolmentId);
        Assert.Equal(firstEnrolment.EnrolmentId, history[1].EnrolmentId);
    }
}
