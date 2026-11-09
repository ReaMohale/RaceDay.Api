using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>Stands in for Azure Blob Storage in tests: nothing is uploaded, URLs are made up.</summary>
public class FakeBlobStorageService : IBlobStorageService
{
    public bool IsConfigured => true;

    public Task<string> UploadAsync(Stream content, string contentType, string folder, string extension,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://fake.blob.test/raceday-images/{folder}/{Guid.NewGuid():N}{extension}";
        return Task.FromResult(url);
    }

    public string? GetReadUrl(string? storedUrl) =>
        string.IsNullOrWhiteSpace(storedUrl) ? null : storedUrl + "?sv=fake-sas";
}
