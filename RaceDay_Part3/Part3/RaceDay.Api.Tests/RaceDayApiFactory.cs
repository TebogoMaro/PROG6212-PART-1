using System.Collections.Concurrent;
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

            // Images: an in-memory fake instead of Azure Blob Storage.
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, FakeBlobStorageService>();
        });
    }
}

public class FakeWeatherService : IWeatherService
{
    public Task<WeatherDto> GetForecastAsync(string location, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WeatherDto(location, date, true, 24.5, 12.0, 10, 18.0, "Clear sky", null));
}

/// <summary>Keeps uploaded images in memory so the tests need no Azure account.</summary>
public class FakeBlobStorageService : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _blobs = new();

    public int CountWithPrefix(string prefix) => _blobs.Keys.Count(k => k.StartsWith(prefix, StringComparison.Ordinal));

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        _blobs[blobName] = (memory.ToArray(), contentType);
    }

    public Task<BlobDownload?> DownloadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        BlobDownload? result = _blobs.TryGetValue(blobName, out var blob)
            ? new BlobDownload(new MemoryStream(blob.Data), blob.ContentType)
            : null;
        return Task.FromResult(result);
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
}

/// <summary>Simulates Azure being down, to prove the API answers 502 instead of crashing.</summary>
public class ThrowingBlobStorageService : IBlobStorageService
{
    public Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Storage is down.");
    public Task<BlobDownload?> DownloadAsync(string blobName, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Storage is down.");
    public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Storage is down.");
}

public class BrokenStorageFactory : RaceDayApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, ThrowingBlobStorageService>();
        });
    }
}
