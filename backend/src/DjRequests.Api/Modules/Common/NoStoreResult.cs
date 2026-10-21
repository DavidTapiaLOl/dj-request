using Microsoft.AspNetCore.Http;

namespace DjRequests.Api.Modules.Common;

public static class NoStoreResults
{
    public static IResult AddNoStore(IResult result) => new NoStoreResult(result);

    private sealed class NoStoreResult : IResult
    {
        private readonly IResult _inner;

        public NoStoreResult(IResult inner)
        {
            _inner = inner;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            return _inner.ExecuteAsync(httpContext);
        }
    }
}
