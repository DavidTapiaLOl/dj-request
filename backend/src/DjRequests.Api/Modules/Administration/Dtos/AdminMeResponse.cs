namespace DjRequests.Api.Modules.Administration.Dtos;

public sealed record AdminMeResponse(
    Guid Id,
    string Name,
    string Email);
