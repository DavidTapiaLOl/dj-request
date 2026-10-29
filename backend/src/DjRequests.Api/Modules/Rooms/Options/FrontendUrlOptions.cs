namespace DjRequests.Api.Modules.Rooms.Options;

public sealed class FrontendUrlOptions
{
    public const string SectionName = "Frontend";

    public string BaseUrl { get; set; } = "http://localhost:3000";

    public bool AllowHttpLan { get; set; }
}
