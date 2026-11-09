namespace RaceDay.Api.Tests;

/// <summary>Results: Organisers capture finish times; Participants view their own.</summary>
public class ResultTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public ResultTests(RaceDayApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Organiser, HttpClient Participant, EnrolmentDto Enrolment)> SetUpAsync()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);
        return (organiser, participant, enrolment);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanSeeIt()
    {
        var (organiser, participant, enrolment) = await SetUpAsync();

        var capture = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 48, 32), Position = 152 });
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var viewed = await participant.GetFromJsonAsync<ResultDto>($"/api/enrolments/{enrolment.EnrolmentId}/results");
        Assert.Equal(new TimeSpan(0, 48, 32), viewed!.FinishTime);
        Assert.Equal(152, viewed.Position);

        var history = await participant.GetFromJsonAsync<List<ResultDto>>("/api/users/me/results");
        Assert.Contains(history!, r => r.EnrolmentId == enrolment.EnrolmentId);
    }

    [Fact]
    public async Task CaptureResult_Twice_ReturnsConflict()
    {
        var (organiser, _, enrolment) = await SetUpAsync();
        var body = new ResultRequest { FinishTime = new TimeSpan(0, 50, 0), Position = 10 };

        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", body);
        var second = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_WithZeroFinishTime_ReturnsBadRequest()
    {
        var (organiser, _, enrolment) = await SetUpAsync();

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = TimeSpan.Zero });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForUnknownEnrolment_ReturnsNotFound()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);

        var response = await organiser.PostAsJsonAsync("/api/enrolments/999999/results",
            new ResultRequest { FinishTime = new TimeSpan(1, 0, 0) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultsForAnotherOrganisersEvent()
    {
        var (_, _, enrolment) = await SetUpAsync();
        var (otherOrganiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);

        var response = await otherOrganiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(1, 0, 0) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetResult_BeforeOneIsCaptured_ReturnsNotFound()
    {
        var (_, participant, enrolment) = await SetUpAsync();

        var response = await participant.GetAsync($"/api/enrolments/{enrolment.EnrolmentId}/results");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotSeeAnotherParticipantsResult()
    {
        var (organiser, _, enrolment) = await SetUpAsync();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 45, 0), Position = 5 });
        var (stranger, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var response = await stranger.GetAsync($"/api/enrolments/{enrolment.EnrolmentId}/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectAResult()
    {
        var (organiser, _, enrolment) = await SetUpAsync();
        var capture = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 55, 0), Position = 20 });
        var original = (await capture.Content.ReadFromJsonAsync<ResultDto>())!;

        var update = await organiser.PutAsJsonAsync($"/api/results/{original.ResultId}",
            new ResultRequest { FinishTime = new TimeSpan(0, 54, 10), Position = 19 });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<ResultDto>())!;
        Assert.Equal(new TimeSpan(0, 54, 10), updated.FinishTime);
        Assert.Equal(19, updated.Position);
    }

    [Fact]
    public async Task Participant_CannotCorrectAResult()
    {
        var (organiser, participant, enrolment) = await SetUpAsync();
        var capture = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 55, 0), Position = 20 });
        var original = (await capture.Content.ReadFromJsonAsync<ResultDto>())!;

        var response = await participant.PutAsJsonAsync($"/api/results/{original.ResultId}",
            new ResultRequest { FinishTime = new TimeSpan(0, 30, 0), Position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownResult_ReturnsNotFound()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);

        var response = await organiser.PutAsJsonAsync("/api/results/999999",
            new ResultRequest { FinishTime = new TimeSpan(1, 0, 0) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
