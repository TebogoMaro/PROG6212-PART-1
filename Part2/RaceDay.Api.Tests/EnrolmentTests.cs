using RaceDay.Api.Data;

namespace RaceDay.Api.Tests;

/// <summary>Event enrolments: Participants enter an event by choosing a category.</summary>
public class EnrolmentTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EnrolmentTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_AndTheLinkToParticipantEventAndCategoryIsRecorded()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, user) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var enrolment = await participant.EnrolAsync(category.CategoryId);

        Assert.Equal(user.UserId, enrolment.ParticipantId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(EnrolmentStatus.Confirmed, enrolment.Status);

        // ...and it really is stored in the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = db.EventEnrolments.Single(e => e.EnrolmentId == enrolment.EnrolmentId);
        Assert.Equal(user.UserId, stored.ParticipantId);
        Assert.Equal(ev.EventId, stored.EventId);
        Assert.Equal(category.CategoryId, stored.CategoryId);
    }

    [Fact]
    public async Task Enrol_InTheSameEventTwice_ReturnsConflict()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var otherCategory = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(category.CategoryId);

        var again = await participant.PostAsync($"/api/categories/{category.CategoryId}/enrolments", null);
        var otherCategoryAttempt = await participant.PostAsync($"/api/categories/{otherCategory.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otherCategoryAttempt.StatusCode);
    }

    [Fact]
    public async Task Enrol_FullCategory_ReturnsConflict()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (first, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var (second, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId, maxParticipants: 1);
        await first.EnrolAsync(category.CategoryId);

        var response = await second.PostAsync($"/api/categories/{category.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_ReturnsNotFound()
    {
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var response = await participant.PostAsync("/api/categories/999999/enrolments", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_SeesOnlyTheirOwnEnrolments()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (mine, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var (someoneElse, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var myEnrolment = await mine.EnrolAsync(category.CategoryId);
        var theirEnrolment = await someoneElse.EnrolAsync(category.CategoryId);

        var list = await mine.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/me");

        Assert.Contains(list!, e => e.EnrolmentId == myEnrolment.EnrolmentId);
        Assert.DoesNotContain(list!, e => e.EnrolmentId == theirEnrolment.EnrolmentId);
    }

    [Fact]
    public async Task Organiser_CanSeeAllEnrolmentsForTheirEvent()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (one, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var (two, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await one.EnrolAsync(category.CategoryId);
        await two.EnrolAsync(category.CategoryId);

        var list = await organiser.GetFromJsonAsync<List<EnrolmentDto>>($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(2, list!.Count);
        Assert.All(list, e => Assert.Equal(ev.EventId, e.EventId));
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_AndThenReEnrol()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);

        var cancel = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        var afterCancel = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/me");
        var reEnrol = await participant.PostAsync($"/api/categories/{category.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(EnrolmentStatus.Cancelled, afterCancel!.Single(e => e.EnrolmentId == enrolment.EnrolmentId).Status);
        Assert.Equal(HttpStatusCode.Created, reEnrol.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (owner, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var (intruder, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await owner.EnrolAsync(category.CategoryId);

        var response = await intruder.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_UnknownEnrolment_ReturnsNotFound()
    {
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var response = await participant.DeleteAsync("/api/enrolments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
