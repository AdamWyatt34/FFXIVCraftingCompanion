using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FFXIV.Api.Filters;

// Admin endpoints are reached only by the data-export workflow; a shared key in a header is
// enough. No configured key means the endpoints are closed, never open.
public sealed class AdminApiKeyFilter(IConfiguration configuration) : IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!Matches(context.HttpContext.Request, configuration))
            context.Result = new UnauthorizedResult();
    }

    /// <summary>
    /// True when the request carries the configured admin key. Shared with the readiness endpoint,
    /// which must not run its checks (and wake the database) for anonymous callers.
    /// </summary>
    public static bool Matches(HttpRequest request, IConfiguration configuration)
    {
        var expected = configuration["Admin:ApiKey"];
        return !string.IsNullOrEmpty(expected)
            && request.Headers.TryGetValue(HeaderName, out var provided)
            && FixedTimeEquals(expected, provided.ToString());
    }

    private static bool FixedTimeEquals(string expected, string provided) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
}
