namespace DjRequests.Api.Modules.Retention.Options;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    public bool Enabled { get; set; }

    public int IntervalMinutes { get; set; } = 15;

    public int BatchSize { get; set; } = 100;
}
