using LDFRecruitment.Models;

namespace LDFRecruitment.Services;

public record StoredFile(string RelativePath, string ContentType, string OriginalName);

/// <summary>Validates and stores uploads outside wwwroot so they are only reachable through an authorised controller.</summary>
public class FileStorageService
{
    private const long PhotoMaxBytes = 2 * 1024 * 1024;
    private const long CertMaxBytes = 5 * 1024 * 1024;

    private readonly string _root;

    public FileStorageService(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(_root);
    }

    public string GetFullPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(_root, relativePath));

    public async Task<(StoredFile? File, string? Error)> SaveAsync(IFormFile file, int applicationId, DocumentType type)
    {
        if (file.Length == 0) return (null, "The file is empty.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = type == DocumentType.Photo
            ? new[] { ".jpg", ".jpeg", ".png" }
            : new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        if (!allowed.Contains(ext)) return (null, $"Only {string.Join(", ", allowed)} files are accepted.");

        var max = type == DocumentType.Photo ? PhotoMaxBytes : CertMaxBytes;
        if (file.Length > max) return (null, $"File is too large (max {max / (1024 * 1024)} MB).");

        // Check magic bytes so a renamed file cannot sneak through.
        var header = new byte[5];
        await using (var peek = file.OpenReadStream())
        {
            var read = await peek.ReadAsync(header, 0, header.Length);
            if (read < 4 || !MatchesExtension(header, ext)) return (null, "The file content does not match its extension.");
        }

        var dir = Path.Combine(_root, applicationId.ToString());
        Directory.CreateDirectory(dir);
        var storedName = $"{type}-{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(dir, storedName);
        await using (var fs = File.Create(full))
            await file.CopyToAsync(fs);

        var contentType = ext switch
        {
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            _ => "image/jpeg"
        };
        return (new StoredFile(Path.Combine(applicationId.ToString(), storedName), contentType, Path.GetFileName(file.FileName)), null);
    }

    public void Delete(string relativePath)
    {
        var full = GetFullPath(relativePath);
        if (full.StartsWith(_root, StringComparison.Ordinal) && File.Exists(full))
            File.Delete(full);
    }

    private static bool MatchesExtension(byte[] h, string ext) => ext switch
    {
        ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8,
        ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
        ".pdf" => h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46,
        _ => false
    };
}
