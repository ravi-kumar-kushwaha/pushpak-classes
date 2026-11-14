namespace PushpakClasses.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveProfileImageAsync(Guid userId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    void DeleteFile(string? relativeUrl);
}
