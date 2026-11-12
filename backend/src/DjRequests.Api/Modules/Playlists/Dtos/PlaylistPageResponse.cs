namespace DjRequests.Api.Modules.Playlists.Dtos;

public sealed record PlaylistPageResponse(
    IReadOnlyList<PlaylistResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore
);
