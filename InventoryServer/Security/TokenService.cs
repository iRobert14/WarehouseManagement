using System.Security.Claims;
using System.Text;
using InventoryServer.Data.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InventoryServer.Security;

// Creeaza tokenul JWT dupa un login reusit
public class TokenService(JwtOptions options)
{
    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(options.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new Claim[]
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.Username),
                new("role", user.Role.ToString()),
            }),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}