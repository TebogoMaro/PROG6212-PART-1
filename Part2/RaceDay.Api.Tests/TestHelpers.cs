namespace RaceDay.Api.Tests;

public static class TestHelpers
{
    public const string Password = "Test#Passw0rd1";

    public static string NewEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@test.raceday.co.za";

    /// <summary>Registers a new user, logs them in and returns a client that carries their session cookie.</summary>
    public static async Task<(HttpClient Client, UserDto User)> NewUserAsync(this RaceDayApiFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = NewEmail(role.ToLowerInvariant());

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = $"Test {role}",
            Email = email,
            Password = Password,
            Role = role,
            OrganiserCode = role == AppRoles.Organiser ? factory.OrganiserCode : null
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var user = (await login.Content.ReadFromJsonAsync<UserDto>())!;
        return (client, user);
    }

    public static EventRequest NewEventRequest(string eventType = "Run", int daysAhead = 30) => new()
    {
        EventName = $"Test Event {Guid.NewGuid():N}",
        EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead)),
        Location = "Johannesburg, Gauteng",
        Description = "An event created by an automated test.",
        EventType = eventType,
        DistanceKm = 10m
    };

    public static async Task<EventDto> CreateEventAsync(this HttpClient organiser, EventRequest? request = null)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", request ?? NewEventRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EventDto>())!;
    }

    public static async Task<CategoryDto> CreateCategoryAsync(this HttpClient organiser, int eventId, int maxParticipants = 100)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new CategoryRequest
        {
            CategoryName = "10km",
            DistanceKm = 10m,
            MaxParticipants = maxParticipants,
            EntryFee = 150m
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    public static async Task<EnrolmentDto> EnrolAsync(this HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;
    }
}
