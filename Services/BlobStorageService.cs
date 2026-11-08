using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

public class BlobStorageOptions
{
    /// <summary>Azure Storage connection string. Use "UseDevelopmentStorage=true" for the Azurite emulator.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "raceday-images";
}

public interface IBlobStorageService
{
    /// <summary>False when no connection string has been configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads a file to Azure Blob Storage and returns the blob's URL (this is what the database stores).</summary>
    Task<string> UploadAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a stored blob URL into a URL the browser can open. The container is private, so a
    /// short-lived read-only SAS token is added. Returns null for null input.
    /// </summary>
    string? GetReadUrl(string? storedUrl);
}

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobStorageOptions _options;
    private readonly BlobContainerClient? _container;
    private volatile bool _containerReady;

    public BlobStorageService(IOptions<BlobStorageOptions> options)
    {
        this._options = options.Value;

        if (!string.IsNullOrWhiteSpace(this._options.ConnectionString))
        {
            this._container = new BlobContainerClient(this._options.ConnectionString, this._options.ContainerName);
        }
    }

    public bool IsConfigured => this._container is not null;

    public async Task<string> UploadAsync(Stream content, string contentType, string folder, string extension,
        CancellationToken cancellationToken = default)
    {
        if (this._container is null)
            throw new InvalidOperationException("Azure Blob Storage is not configured.");

        // Private container (no public access): images are shown through short-lived SAS links (see GetReadUrl).
        // The existence check only needs to happen once per application run.
        if (!this._containerReady)
        {
            await this._container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            this._containerReady = true;
        }

        var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
        var blob = this._container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);

        return blob.Uri.ToString();
    }

    public string? GetReadUrl(string? storedUrl)
    {
        if (string.IsNullOrWhiteSpace(storedUrl))
            return null;

        if (this._container is null || !this._container.CanGenerateSasUri)
            return storedUrl;

        try
        {
            var blobName = new BlobUriBuilder(new Uri(storedUrl)).BlobName;
            var blob = this._container.GetBlobClient(blobName);
            return blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1)).ToString();
        }
        catch (Exception)
        {
            // If anything goes wrong, fall back to the stored URL rather than failing the whole request.
            return storedUrl;
        }
    }
}
