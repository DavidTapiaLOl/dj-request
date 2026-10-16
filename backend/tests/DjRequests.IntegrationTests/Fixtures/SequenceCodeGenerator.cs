using System.Collections.Generic;
using DjRequests.Api.Modules.Rooms.Services;

namespace DjRequests.IntegrationTests.Fixtures;

public sealed class SequenceCodeGenerator : IRoomCodeGenerator
{
    private readonly Queue<string> _codes;
    private readonly string _fallback;

    public SequenceCodeGenerator(params string[] codes)
    {
        _codes = new Queue<string>(codes);
        _fallback = codes.Length > 0 ? codes[^1] : "FALLBACK01";
    }

    public string GenerateCode()
    {
        return _codes.Count > 0 ? _codes.Dequeue() : _fallback;
    }

    public bool TryGenerateCode(out string code, int maxAttempts = 5)
    {
        code = GenerateCode();
        return true;
    }
}

public sealed class FixedCodeGenerator : IRoomCodeGenerator
{
    private readonly string _code;

    public FixedCodeGenerator(string code)
    {
        _code = code;
    }

    public string GenerateCode() => _code;

    public bool TryGenerateCode(out string code, int maxAttempts = 5)
    {
        code = _code;
        return true;
    }
}
