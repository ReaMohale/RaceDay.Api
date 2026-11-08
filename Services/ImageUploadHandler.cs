namespace RaceDay.Api.Services;

public record ImageUploadOutcome(string? Url, int Status, string Title, string Detail)
{
    public bool Succeeded => this.Url is not null;
}

/// <summary>Validates an uploaded image and sends it to Azure Blob Storage.</summary>
public static class ImageUploadHandler
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    public static async Task<ImageUploadOutcome> HandleAsync(IFormFile? file, IBlobStorageService blobs, string folder,
        CancellationToken cancellationToken)
    {
        if (!blobs.IsConfigured)
            return Failure(StatusCodes.Status503ServiceUnavailable, "Image storage unavailable",
                "Azure Blob Storage is not configured on the server.");

        if (file is null || file.Length == 0)
            return Failure(StatusCodes.Status400BadRequest, "No file", "Choose an image file to upload.");

        if (file.Length > MaxBytes)
            return Failure(StatusCodes.Status400BadRequest, "File too large", "Images must be 5 MB or smaller.");

        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        if (!AllowedExtensions.Contains(extension))
            return Failure(StatusCodes.Status400BadRequest, "Unsupported file type", "Only JPG, PNG, GIF or WEBP images are allowed.");

        // The file's real content decides its type, not its name: a renamed text file is rejected,
        // and the stored content type and extension always match the actual image format.
        var detected = await DetectFormatAsync(file, cancellationToken);
        if (detected is null)
            return Failure(StatusCodes.Status400BadRequest, "Invalid image", "The file is not a valid image.");

        await using var stream = file.OpenReadStream();
        var url = await blobs.UploadAsync(stream, detected.Value.ContentType, folder, detected.Value.Extension, cancellationToken);
        return new ImageUploadOutcome(url, StatusCodes.Status200OK, "Uploaded", "The image was uploaded.");
    }

    private static ImageUploadOutcome Failure(int status, string title, string detail) =>
        new(null, status, title, detail);

    /// <summary>Reads the first bytes of the file and returns its format, or null when it is not a supported image.</summary>
    private static async Task<(string ContentType, string Extension)?> DetectFormatAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = 0;
        while (read < header.Length)
        {
            var count = await stream.ReadAsync(header.AsMemory(read, header.Length - read), cancellationToken);
            if (count == 0)
                break;
            read += count;
        }

        if (read < 4)
            return null;

        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ("image/jpeg", ".jpg");

        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return ("image/png", ".png");

        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
            return ("image/gif", ".gif");

        if (read >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return ("image/webp", ".webp");

        return null;
    }
}
