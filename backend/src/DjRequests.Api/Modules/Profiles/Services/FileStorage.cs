using System.Text.RegularExpressions;
using DjRequests.Api.Modules.Profiles.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Profiles.Services;

public sealed partial class FileStorage : IFileStorage
{
    private readonly string _root;
    private readonly string? _publicUrlBase;
    private readonly ILogger<FileStorage> _logger;

    public FileStorage(
        IHostEnvironment environment,
        IOptions<FileStorageOptions> options,
        ILogger<FileStorage> logger)
    {
        _logger = logger;
        var configured = options.Value.Path;
        _root = System.IO.Path.GetFullPath(
            System.IO.Path.IsPathRooted(configured)
                ? configured
                : System.IO.Path.Combine(environment.ContentRootPath, configured));

        var publicUrlBase = options.Value.PublicUrlBase?.Trim();
        _publicUrlBase = string.IsNullOrEmpty(publicUrlBase) ? null : publicUrlBase.TrimEnd('/');
    }

    public async Task<string> SaveAsync(
        byte[] content,
        string extension,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);

        var fileName = $"{Guid.NewGuid():N}{NormalizeExtension(extension)}";
        var physicalPath = System.IO.Path.Combine(_root, fileName);

        await File.WriteAllBytesAsync(physicalPath, content, cancellationToken);
        return fileName;
    }

    public Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!TryGetPhysicalPath(fileName, out var physicalPath))
        {
            return Task.CompletedTask;
        }

        try
        {
            File.Delete(physicalPath);
        }
        catch (IOException)
        {
            _logger.LogWarning("No se pudo eliminar el archivo de perfil {FileName}.", fileName);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("No se pudo eliminar el archivo de perfil {FileName} por falta de acceso.", fileName);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string fileName)
    {
        if (_publicUrlBase is not null)
        {
            return $"{_publicUrlBase}/{fileName}";
        }

        return $"/api/profile-photos/{fileName}";
    }

    public bool TryGetPhysicalPath(string fileName, out string physicalPath)
    {
        physicalPath = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName) || !GeneratedFileNamePattern().IsMatch(fileName))
        {
            return false;
        }

        var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, fileName));
        var rootWithSeparator = _root.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? _root
            : _root + System.IO.Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        physicalPath = candidate;
        return true;
    }

    private static string NormalizeExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
    }

    [GeneratedRegex("^[a-f0-9]{32}\\.(jpg|png)$", RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedFileNamePattern();
}
