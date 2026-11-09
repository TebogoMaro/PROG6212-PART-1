using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>A file read back from blob storage.</summary>
public record BlobDownload(Stream Content, string ContentType);

/// <summary>
/// All image storage goes through this interface. Only the API ever talks to Azure -
/// the MVC web app uses the API's upload/download endpoints.
/// </summary>
public interface IBlobStorageService
{
    Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<BlobDownload?> DownloadAsync(string blobName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Blob Storage implementation. Settings: AzureBlob:ConnectionString and AzureBlob:ContainerName.
/// The container is private and is created on first use. The connection is opened lazily so the API
/// still starts (and every non-image endpoint still works) when storage is not configured.
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly string? _connectionString;
    private readonly string _containerName;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private BlobContainerClient? _container;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        _connectionString = configuration["AzureBlob:ConnectionString"];
        _containerName = configuration["AzureBlob:ContainerName"] ?? "raceday-images";
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null) return _container;

        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("Azure Blob Storage is not configured (AzureBlob:ConnectionString is empty).");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_container is null)
            {
                var client = new BlobContainerClient(_connectionString, _containerName);
                await client.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                _container = client;
            }
            return _container;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);
    }

    public async Task<BlobDownload?> DownloadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        var blob = container.GetBlobClient(blobName);

        var exists = await blob.ExistsAsync(cancellationToken);
        if (!exists.Value) return null;

        var result = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new BlobDownload(result.Value.Content, result.Value.Details.ContentType ?? "application/octet-stream");
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
