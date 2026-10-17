using System.Security.Cryptography;

namespace DjRequests.Api.Modules.Guests.Services;

public interface IGuestTokenGenerator
{
    (string Token, string TokenHash) GenerateToken();
}

public sealed class GuestTokenGenerator : IGuestTokenGenerator
{
    private const int TokenBytes = 32;
    private const int HashHexLength = 64;

    public (string Token, string TokenHash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        
        var hashBytes = SHA256.HashData(bytes);
        var tokenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return (token, tokenHash);
    }
}
