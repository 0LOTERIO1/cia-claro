using System.Security.Claims;

namespace Cia.Api.Controllers;

public static class HttpUserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
