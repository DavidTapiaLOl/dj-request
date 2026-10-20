namespace DjRequests.Api.Modules.Requests.Dtos;

public sealed record SolicitudPageResponse(
    IReadOnlyList<SolicitudResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore
);
