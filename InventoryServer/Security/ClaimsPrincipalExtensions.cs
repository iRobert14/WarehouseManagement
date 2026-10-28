using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace InventoryServer.Security;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Tokenul nu contine id-ul utilizatorului.");
    }
}