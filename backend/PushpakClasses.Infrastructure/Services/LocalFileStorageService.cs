using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;

namespace PushpakClasses.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/pjpeg", "application/octet-stream"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IWebHostEnvironment environment, ILogger<LocalFileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveProfileImageAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new AuthException("Profile photo file cannot be empty.");
        }

        if (fileStream.Length > MaxFileSizeBytes)
        {
            throw new AuthException("Profile photo size exceeds maximum allowed limit of 2 MB.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new AuthException("Invalid file format. Only JPG, JPEG, and PNG images are allowed.");
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new AuthException("Invalid MIME type. Only image/jpeg and image/png files are allowed.");
        }

        var uploadsFolder = Path.Combine(_environment.ContentRootPath, "uploads", "profiles");

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{userId}_{DateTime.UtcNow.Ticks}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var destinationStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        _logger.LogInformation("Saved profile photo for User {UserId} at {Path}", userId, fullPath);

        return $"/uploads/profiles/{uniqueFileName}";
    }

    public void DeleteFile(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return;
        }

        try
        {
            var fileName = Path.GetFileName(relativeUrl);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var fullPath = Path.Combine(_environment.ContentRootPath, "uploads", "profiles", fileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file at {Path}", fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file at {RelativeUrl}", relativeUrl);
        }
    }
}
