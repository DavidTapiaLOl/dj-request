using System.Security.Cryptography;
using DjRequests.Api.Modules.Guests.Services;

namespace DjRequests.IntegrationTests.Fixtures;

public sealed class DeterministicGuestTokenGenerator : IGuestTokenGenerator
{
    private readonly Queue<(string Token, string TokenHash)> _tokens;
    private readonly (string Token, string TokenHash) _fallback;

    public DeterministicGuestTokenGenerator(params (string Token, string TokenHash)[] tokens)
    {
        _tokens = new Queue<(string Token, string TokenHash)>(tokens);
        _fallback = tokens.Length > 0 ? tokens[^1] : GenerateRandom();
    }

    public (string Token, string TokenHash) GenerateToken()
    {
        if (_tokens.Count > 0)
        {
            return _tokens.Dequeue();
        }
        return _fallback;
    }

    private static (string Token, string TokenHash) GenerateRandom()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).TrimEnd("="[0]).Replace("+", "-").Replace("/", "_");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (Token: token, TokenHash: hash);
    }
}

public sealed class FixedGuestTokenGenerator : IGuestTokenGenerator
{
    private readonly string _token;
    private readonly string _hash;

    public FixedGuestTokenGenerator(string token)
    {
        _token = token;
        var base64 = token.Replace("-", "+").Replace("_", "/");
        var padding = 4 - (base64.Length % 4);
        if (padding != 4) base64 += new string("="[0], padding);
        var bytes = Convert.FromBase64String(base64);
        _hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public (string Token, string TokenHash) GenerateToken()
    {
        return (Token: _token, TokenHash: _hash);
    }
}