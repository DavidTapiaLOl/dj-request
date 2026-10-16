using System;

namespace DjRequests.Api.Modules.Rooms.Services;

public sealed class TimeZoneValidator
{
    public sealed class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    private const string DefaultTimezone = "America/Mexico_City";

    public static TimeZoneInfo ValidateAndResolve(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimezone);
        }

        if (timezone.Length > 64)
        {
            throw new ValidationException(
                "La zona horaria no puede superar los 64 caracteres.");
        }

        string? windowsId;
        bool isIana = TimeZoneInfo.TryConvertIanaIdToWindowsId(timezone, out windowsId);
        if (!isIana)
        {
            throw new ValidationException(
                $"La zona horaria '{timezone}' no es un identificador IANA valido. " +
                "Use un identificador IANA como 'America/Mexico_City'.");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ValidationException(
                $"La zona horaria '{timezone}' no fue encontrada en el sistema. " +
                "Zonas IANA validas incluyen 'America/Mexico_City'.");
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new ValidationException(
                $"Los datos de zona horaria del sistema estan corruptos o incompletos: {ex.Message}");
        }
    }

    public static bool TryValidateAndResolve(string? timezone, out TimeZoneInfo? info, out string? error)
    {
        try
        {
            info = ValidateAndResolve(timezone);
            error = null;
            return true;
        }
        catch (ValidationException ex)
        {
            info = null;
            error = ex.Message;
            return false;
        }
    }
}
