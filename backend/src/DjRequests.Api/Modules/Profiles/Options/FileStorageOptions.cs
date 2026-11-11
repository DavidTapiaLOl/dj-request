namespace DjRequests.Api.Modules.Profiles.Options;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Path { get; set; } = "storage/profiles";

    public string? PublicUrlBase { get; set; }

    public int MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxDimensionPx { get; set; } = 2048;

    public int MaxSourcePixels { get; set; } = 50_000_000;
}
