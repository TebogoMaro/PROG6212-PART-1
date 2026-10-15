using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>
/// Swagger acts as live verification that the built API matches the Part 1 endpoint plan (v2).
/// If any planned endpoint is missing from the running API, this test fails.
/// </summary>
public class SwaggerTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public SwaggerTests(RaceDayApiFactory factory) => _factory = factory;

    private static readonly (string Method, string Path)[] PlannedEndpoints =
    {
        ("post", "/api/auth/register"),
        ("post", "/api/auth/login"),
        ("post", "/api/auth/logout"),
        ("get", "/api/users/me"),
        ("put", "/api/users/me"),
        ("get", "/api/users/me/results"),
        ("get", "/api/events"),
        ("get", "/api/events/{id}"),
        ("post", "/api/events"),
        ("put", "/api/events/{id}"),
        ("delete", "/api/events/{id}"),
        ("get", "/api/events/{id}/weather"),
        ("get", "/api/events/{id}/categories"),
        ("post", "/api/events/{id}/categories"),
        ("get", "/api/events/{id}/enrolments"),
        ("get", "/api/categories/{id}"),
        ("put", "/api/categories/{id}"),
        ("delete", "/api/categories/{id}"),
        ("get", "/api/categories/{id}/route"),
        ("put", "/api/categories/{id}/route"),
        ("post", "/api/categories/{id}/enrolments"),
        ("get", "/api/enrolments/me"),
        ("delete", "/api/enrolments/{id}"),
        ("post", "/api/enrolments/{id}/results"),
        ("get", "/api/enrolments/{id}/results"),
        ("put", "/api/results/{id}")
    };

    [Fact]
    public void Plan_Lists26Endpoints()
    {
        Assert.Equal(26, PlannedEndpoints.Length);
    }

    [Fact]
    public async Task SwaggerDocument_ExposesEveryPlannedEndpoint()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        foreach (var (method, path) in PlannedEndpoints)
        {
            Assert.True(paths.TryGetProperty(path, out var item), $"Swagger is missing the path {path}");
            Assert.True(item.TryGetProperty(method, out _), $"Swagger is missing {method.ToUpperInvariant()} {path}");
        }
    }

    [Fact]
    public async Task SwaggerUi_IsAvailableInTheBrowser()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
