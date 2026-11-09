using System.Net.Http.Headers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>Event banners and profile pictures: uploads go through the API into blob storage.</summary>
public class ImageTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public ImageTests(RaceDayApiFactory factory) => _factory = factory;

    // A real 1x1 PNG image.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent FileContent(byte[] bytes, string contentType = "image/png", string fileName = "image.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Organiser_CanUploadABanner_AndAnyoneCanDownloadIt()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();

        var upload = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var updated = (await upload.Content.ReadFromJsonAsync<EventDto>())!;
        Assert.True(updated.HasBanner);

        var download = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/banner");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ReplacingABanner_RemovesTheOldImage()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();

        await organiser.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png));
        await organiser.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png));

        var storage = (FakeBlobStorageService)_factory.Services.GetRequiredService<IBlobStorageService>();
        Assert.Equal(1, storage.CountWithPrefix($"events/{ev.EventId}/"));
    }

    [Fact]
    public async Task EventDetails_ShowThatABannerExists()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();
        await organiser.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png));

        var detail = await _factory.CreateClient().GetFromJsonAsync<EventDetailDto>($"/api/events/{ev.EventId}");

        Assert.True(detail!.HasBanner);
    }

    [Fact]
    public async Task Participant_CannotUploadABanner_ReturnsForbidden()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var ev = await organiser.CreateEventAsync();

        var response = await participant.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadABannerForAnotherOrganisersEvent()
    {
        var (owner, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var (otherOrganiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await owner.CreateEventAsync();

        var response = await otherOrganiser.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotUploadABanner_ReturnsUnauthorized()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();

        var response = await _factory.CreateClient().PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithATextFileRenamedToPng_ReturnsBadRequest()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("this is just text, not a picture");

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(notAnImage, "image/png", "fake.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForUnknownEvent_ReturnsNotFound()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);

        var response = await organiser.PostAsync("/api/events/999999/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBanner_WhenNoneWasUploaded_ReturnsNotFound()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();

        var response = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/banner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_CanBeUploadedAndDownloaded()
    {
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var upload = await participant.PostAsync("/api/users/me/picture", FileContent(Png));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var profile = await participant.GetFromJsonAsync<UserDto>("/api/users/me");
        Assert.True(profile!.HasProfilePicture);

        var download = await participant.GetAsync("/api/users/me/picture");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ProfilePicture_BeforeAnyUpload_ReturnsNotFound()
    {
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var response = await participant.GetAsync("/api/users/me/picture");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_WithoutLoggingIn_ReturnsUnauthorized()
    {
        var anonymous = _factory.CreateClient();

        var get = await anonymous.GetAsync("/api/users/me/picture");
        var post = await anonymous.PostAsync("/api/users/me/picture", FileContent(Png));

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_WithAnInvalidFile_ReturnsBadRequest()
    {
        var (participant, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("not a picture");

        var response = await participant.PostAsync("/api/users/me/picture", FileContent(notAnImage, "image/jpeg", "me.jpg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>When Azure is unavailable the API must answer 502 (not crash).</summary>
public class BrokenStorageTests : IClassFixture<BrokenStorageFactory>
{
    private readonly BrokenStorageFactory _factory;

    public BrokenStorageTests(BrokenStorageFactory factory) => _factory = factory;

    [Fact]
    public async Task UploadBanner_WhenStorageIsDown_ReturnsBadGateway()
    {
        var (organiser, _) = await _factory.NewUserAsync(AppRoles.Organiser);
        var ev = await organiser.CreateEventAsync();
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner",
            new MultipartFormDataContent { { file, "file", "banner.png" } });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
