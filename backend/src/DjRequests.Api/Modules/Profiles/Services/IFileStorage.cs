namespace DjRequests.Api.Modules.Profiles.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default);

    Task DeleteAsync(string fileName, CancellationToken cancellationToken = default);

    string GetPublicUrl(string fileName);

    bool TryGetPhysicalPath(string fileName, out string physicalPath);
}
