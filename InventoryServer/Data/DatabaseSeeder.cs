using InventoryCommon;
using InventoryServer.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryServer.Data;

public class DatabaseSeeder(
    InventoryDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync()
    {
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var password = configuration["Seed:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DefaultPassword lipseste din User Secrets, nu am creat useri.");
            return;
        }

        db.Users.AddRange(
            CreateUser("admin", UserRole.Admin, password),
            CreateUser("manager", UserRole.Manager, password),
            CreateUser("employee", UserRole.Employee, password));

        await db.SaveChangesAsync();
        logger.LogInformation("Am creat userii de test: admin, manager, employee.");
    }

    private User CreateUser(string username, UserRole role, string password)
    {
        var user = new User
        {
            Username = username,
            Role = role,
            CreatedAtUtc = DateTime.UtcNow,
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);
        return user;
    }
}