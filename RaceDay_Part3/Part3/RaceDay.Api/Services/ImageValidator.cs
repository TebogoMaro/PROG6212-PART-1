namespace RaceDay.Api.Services;

/// <summary>
/// Checks an uploaded image. The file type is decided by the file's real first bytes (its "signature"),
/// not by the name or content type the browser claims, so a renamed text file is rejected.
/// </summary>
public static class ImageValidator
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public static async Task<(string? Extension, string? Error)> ValidateAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return (null, "Please choose an image file to upload.");
        if (file.Length > MaxBytes)
            return (null, "The image must be 5 MB or smaller.");

        var header = new byte[12];
        int read;
        await using (var stream = file.OpenReadStream())
        {
            read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        }

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return (".jpg", null);
        if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return (".png", null);
        if (read >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
            && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
            return (".webp", null);

        return (null, "Only JPG, PNG or WebP images are allowed.");
    }

    public static string ContentTypeFor(string extension) => extension switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
