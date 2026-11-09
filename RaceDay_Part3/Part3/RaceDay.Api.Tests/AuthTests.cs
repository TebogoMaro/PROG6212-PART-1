using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

public class AuthTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public AuthTests(RaceDayApiFactory factory) => _factory = factory;

    private static RegisterRequest NewParticipant(string? email = null) => new()
    {
        FullName = "Thabo Nkosi",
        Email = email ?? TestHelpers.NewEmail(),
        Password = TestHelpers.Password,
        Role = AppRoles.Participant
    };

    [Fact]
    public async Task Register_Participant_ReturnsCreated_AndNeverReturnsPassword()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewParticipant());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(AppRoles.Participant, user!.Role);
    }

    [Fact]
    public async Task Register_StoresAHashedPassword_NotThePlainText()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant();

        await client.PostAsJsonAsync("/api/auth/register", request);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = db.Users.Single(u => u.Email == request.Email.ToLowerInvariant());
        Assert.NotEqual(request.Password, stored.PasswordHash);
        Assert.True(PasswordHelper.Verify(request.Password, stored.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant();

        await client.PostAsJsonAsync("/api/auth/register", request);
        var second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_ReturnsBadRequest()
    {
        var request = NewParticipant();
        request.Role = "Admin";

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        var request = NewParticipant();
        request.Password = "short";

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutValidCode_ReturnsForbidden()
    {
        var request = NewParticipant();
        request.Role = AppRoles.Organiser;
        request.OrganiserCode = "WRONG-CODE";

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_ReturnsCreated()
    {
        var request = NewParticipant();
        request.Role = AppRoles.Organiser;
        request.OrganiserCode = _factory.OrganiserCode;

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_StartsASession()
    {
        var (client, user) = await _factory.NewUserAsync(AppRoles.Participant);

        var me = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(user.UserId, profile!.UserId);
        Assert.Equal(AppRoles.Participant, profile.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant();
        await client.PostAsJsonAsync("/api/auth/register", request);

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = "Wrong#Passw0rd9" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = TestHelpers.NewEmail("nobody"), Password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutLoggingIn_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var (client, _) = await _factory.NewUserAsync(AppRoles.Participant);

        var logout = await client.PostAsync("/api/auth/logout", null);
        var afterLogout = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesTheName()
    {
        var (client, _) = await _factory.NewUserAsync(AppRoles.Participant);
        var current = (await client.GetFromJsonAsync<UserDto>("/api/users/me"))!;

        var update = await client.PutAsJsonAsync("/api/users/me",
            new UpdateProfileRequest { FullName = "Renamed Person", Email = current.Email });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var after = (await client.GetFromJsonAsync<UserDto>("/api/users/me"))!;
        Assert.Equal("Renamed Person", after.FullName);
    }

    [Fact]
    public void PasswordHelper_HashesAndVerifies()
    {
        var hash = PasswordHelper.Hash("MyS3cret!Pass");

        Assert.NotEqual("MyS3cret!Pass", hash);
        Assert.True(PasswordHelper.Verify("MyS3cret!Pass", hash));
        Assert.False(PasswordHelper.Verify("not-the-password", hash));
    }

    [Fact]
    public void PasswordHelper_UsesARandomSalt()
    {
        Assert.NotEqual(PasswordHelper.Hash("SamePassword1!"), PasswordHelper.Hash("SamePassword1!"));
    }
}
