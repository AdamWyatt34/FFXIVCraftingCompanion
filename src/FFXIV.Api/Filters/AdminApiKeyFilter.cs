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
        var expected = configuration["Admin:ApiKey"];
        if (string.IsNullOrEmpty(expected)
            || !context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !FixedTimeEquals(expected, provided.ToString()))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    private static bool FixedTimeEquals(string expected, string provided) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
}
