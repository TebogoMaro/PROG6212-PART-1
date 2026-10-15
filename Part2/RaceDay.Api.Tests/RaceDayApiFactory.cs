using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>
/// Starts the real API in memory for the tests. SQL Server is swapped for an in-memory database
/// and the live weather service is swapped for a fake, so tests are fast and need no internet.
/// </summary>
public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    public string OrganiserCode =>
        Services.GetRequiredService<IConfiguration>()["Registration:OrganiserCode"]!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server registration (including the EF Core 9+ options-configuration entries).
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal) &&
                 d.ServiceType.GenericTypeArguments.Contains(typeof(RaceDayDbContext)))).ToList();
            foreach (var descriptor in toRemove) services.Remove(descriptor);

            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IWeatherService>();
            services.AddSingleton<IWeatherService, FakeWeatherService>();
        });
    }
}

public class FakeWeatherService : IWeatherService
{
    public Task<WeatherDto> GetForecastAsync(string location, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WeatherDto(location, date, true, 24.5, 12.0, 10, 18.0, "Clear sky", null));
}
