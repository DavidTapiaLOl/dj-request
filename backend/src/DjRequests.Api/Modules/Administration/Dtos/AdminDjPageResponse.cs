namespace DjRequests.Api.Modules.Administration.Dtos;

public sealed record AdminDjPageResponse(
    IReadOnlyList<AdminDjListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore);
