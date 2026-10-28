using System.Security.Claims;
using System.Text.RegularExpressions;
using InventoryCommon.Contracts;
using InventoryServer.Data;
using InventoryServer.Data.Entities;
using InventoryServer.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryServer.Features.Users;

public static partial class UserEndpoints
{
    private const string PasswordRule = "Parola trebuie sa aiba minim 8 caractere, cel putin o litera si o cifra.";

    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.ManageUsers);

        group.MapGet("", GetUsersAsync);
        group.MapPost("", CreateUserAsync);
        group.MapPut("/{id:int}", UpdateUserAsync);
        group.MapPost("/{id:int}/reset-password", ResetPasswordAsync);
    }

    private static async Task<IResult> GetUsersAsync(InventoryDbContext db)
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new UserListItemDto(u.Id, u.Username, u.Role, u.IsActive, u.CreatedAtUtc))
            .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request, InventoryDbContext db, IPasswordHasher<User> passwordHasher)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Username) || !UsernameRegex().IsMatch(request.Username.Trim()))
            errors["Username"] = ["Username-ul trebuie sa aiba 3-50 caractere: litere, cifre, '.', '_' sau '-'."];

        if (!IsStrongEnough(request.Password))
            errors["Password"] = [PasswordRule];

        if (!Enum.IsDefined(request.Role))
            errors["Role"] = ["Rol invalid."];

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            return Results.Problem($"Exista deja un utilizator '{username}'.", statusCode: StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Username = username,
            Role = request.Role,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Results.Created($"/api/users/{user.Id}", ToDto(user));
    }

    private static async Task<IResult> UpdateUserAsync(
        int id, UpdateUserRequest request, ClaimsPrincipal principal, InventoryDbContext db)
    {
        if (!Enum.IsDefined(request.Role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Role"] = ["Rol invalid."] });
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return Results.NotFound();
        }

        var isSelf = id == principal.GetUserId();
        if (isSelf && (request.Role != user.Role || !request.IsActive))
        {
            return Results.Problem(
                "Nu iti poti schimba propriul rol si nici nu iti poti dezactiva propriul cont.",
                statusCode: StatusCodes.Status409Conflict);
        }

        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync();

        return Results.Ok(ToDto(user));
    }

    private static async Task<IResult> ResetPasswordAsync(
        int id, ResetPasswordRequest request, InventoryDbContext db, IPasswordHasher<User> passwordHasher)
    {
        if (!IsStrongEnough(request.NewPassword))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["NewPassword"] = [PasswordRule] });
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return Results.NotFound();
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static bool IsStrongEnough(string? password) =>
        password is { Length: >= 8 } && password.Any(char.IsLetter) && password.Any(char.IsDigit);

    private static UserListItemDto ToDto(User user) =>
        new(user.Id, user.Username, user.Role, user.IsActive, user.CreatedAtUtc);

    [GeneratedRegex("^[A-Za-z0-9._-]{3,50}$")]
    private static partial Regex UsernameRegex();
}