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

    public static string GetCustomerId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("customerId");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Cia.Api.Exceptions.UnauthorizedAppException("Conta de cliente não associada a este usuário.");
        }

        return value;
    }
}
