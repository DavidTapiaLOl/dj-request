namespace DjRequests.Api.Modules.Rooms.Services;

public interface IRoomCodeGenerator
{
    string GenerateCode();
    bool TryGenerateCode(out string code, int maxAttempts = 5);
}