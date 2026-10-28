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
        await SeedUsersAsync();
        await SeedProductsAsync();
    }

    private async Task SeedUsersAsync()
    {
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

    private async Task SeedProductsAsync()
    {
        if (await db.Products.AnyAsync())
        {
            return;
        }

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
        if (admin is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        (string Sku, string Name, string Category, string Size, string Color, decimal Price, int Qty, int Min)[] items =
        [
            ("TSH-BAS-WHT-M", "Tricou bumbac basic", "Tricouri", "M", "Alb", 49.90m, 120, 20),
            ("TSH-BAS-BLK-L", "Tricou bumbac basic", "Tricouri", "L", "Negru", 49.90m, 8, 20),
            ("HOD-ZIP-GRY-M", "Hanorac cu fermoar", "Hanorace", "M", "Gri", 179.00m, 35, 10),
            ("JNS-SLM-BLU-32", "Blugi slim fit", "Blugi", "32", "Albastru", 229.00m, 42, 10),
            ("JNS-REG-BLK-34", "Blugi regular fit", "Blugi", "34", "Negru", 219.00m, 4, 10),
            ("JKT-DEN-BLU-L", "Geaca de blugi", "Geci", "L", "Albastru", 349.00m, 15, 5),
            ("DRS-SUM-RED-S", "Rochie de vara", "Rochii", "S", "Rosu", 199.00m, 22, 8),
            ("SWT-WOL-BGE-M", "Pulover de lana", "Pulovere", "M", "Bej", 259.00m, 0, 5),
        ];

        foreach (var item in items)
        {
            var product = new Product
            {
                Id = Guid.CreateVersion7(),
                Sku = item.Sku,
                Name = item.Name,
                Category = item.Category,
                Size = item.Size,
                Color = item.Color,
                Price = item.Price,
                Quantity = item.Qty,
                LowStockThreshold = item.Min,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            if (item.Qty > 0)
            {
                product.Movements.Add(new StockMovement
                {
                    Delta = item.Qty,
                    QuantityAfter = item.Qty,
                    Reason = "Stoc initial",
                    UserId = admin.Id,
                    CreatedAtUtc = now,
                });
            }

            db.Products.Add(product);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Am adaugat {Count} produse de test.", items.Length);
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