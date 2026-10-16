using System.Security.Cryptography;
using System.Text;
using DjRequests.Api.Modules.Rooms.Services;

namespace DjRequests.Api.Modules.Rooms.Services;

public sealed class RoomCodeGenerator : IRoomCodeGenerator
{
    private const int CodeLength = 10;
    private static readonly char[] ValidChars =
    {
        'A','B','C','D','E','F','G','H','J','K','L','M','N','P','Q','R','S','T','U','V','W','X','Y','Z',
        '2','3','4','5','6','7','8','9'
    };

    public string GenerateCode()
    {
        var code = new StringBuilder(CodeLength);
        for (int i = 0; i < CodeLength; i++)
        {
            int index = RandomNumberGenerator.GetInt32(ValidChars.Length);
            code.Append(ValidChars[index]);
        }
        return code.ToString();
    }

    public bool TryGenerateCode(out string code, int maxAttempts = 5)
    {
        code = GenerateCode();
        return true;
    }
}
