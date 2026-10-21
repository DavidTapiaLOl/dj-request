namespace DjRequests.Api.Modules.Common;

public static class PaginationValidation
{
    public const int MaxPageSize = 100;

    public static bool TryValidate(int page, int pageSize, out string error)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            error = $"page debe ser >= 1 y pageSize entre 1 y {MaxPageSize}.";
            return false;
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            error = "page fuera de rango.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static long GetOffset(int page, int pageSize) => (long)(page - 1) * pageSize;
}
