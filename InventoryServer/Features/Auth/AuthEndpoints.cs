using System.Security.Claims;
using InventoryCommon.Contracts;
using InventoryServer.Data;
using InventoryServer.Data.Entities;
using InventoryServer.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace InventoryServer.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", GetCurrentUserAsync); // necesita token
    }

    // POST /api/auth/login
    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        InventoryDbContext db,
        IPasswordHasher<User> passwordHasher,
        TokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem("Completeaza username-ul si parola.", statusCode: StatusCodes.Status400BadRequest);
        }

        var username = request.Username.Trim();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (user is null)
        {
            return InvalidLogin();
        }

        var check = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (check == PasswordVerificationResult.Failed)
        {
            return InvalidLogin();
        }

        var (token, expiresAtUtc) = tokenService.CreateToken(user);
        var userDto = new UserDto(user.Id, user.Username, user.Role);

        return Results.Ok(new LoginResponse(token, expiresAtUtc, userDto));
    }

    // GET /api/auth/me
    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal principal, InventoryDbContext db)
    {
        var userIdText = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userIdText, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Username, u.Role))
            .SingleOrDefaultAsync();

        return user is null ? Results.NotFound() : Results.Ok(user);
    }


    private static IResult InvalidLogin() =>
        Results.Problem("Username sau parola gresita.", statusCode: StatusCodes.Status401Unauthorized, title: "Login esuat");
}