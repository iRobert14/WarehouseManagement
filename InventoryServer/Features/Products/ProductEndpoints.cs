using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.RegularExpressions;
using InventoryCommon.Contracts;
using InventoryServer.Data;
using InventoryServer.Data.Entities;
using InventoryServer.Security;
using Microsoft.EntityFrameworkCore;

namespace InventoryServer.Features.Products;

public static partial class ProductEndpoints
{
    private static readonly Expression<Func<Product, ProductDto>> ToDto = p => new ProductDto(
        p.Id, p.Sku, p.Name, p.Category, p.Size, p.Color, p.Price, p.Quantity,
        p.LowStockThreshold, p.Quantity <= p.LowStockThreshold, p.UpdatedAtUtc);

    private static readonly Func<Product, ProductDto> ToDtoInMemory = ToDto.Compile();

    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("", GetProductsAsync);
        group.MapGet("/categories", GetCategoriesAsync);
        group.MapGet("/{id:guid}", GetProductAsync);
        group.MapPost("", CreateProductAsync).RequireAuthorization(Policies.ManageProducts);
        group.MapPut("/{id:guid}", UpdateProductAsync).RequireAuthorization(Policies.ManageProducts);
        group.MapDelete("/{id:guid}", DeleteProductAsync).RequireAuthorization(Policies.DeleteProducts);
        group.MapPost("/{id:guid}/stock", AdjustStockAsync);
        group.MapGet("/{id:guid}/movements", GetMovementsAsync);
    }

    private static async Task<IResult> GetProductsAsync(
        InventoryDbContext db, string? search, string? category, bool? lowStockOnly)
    {
        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Sku.Contains(term) || p.Color.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (lowStockOnly == true)
        {
            query = query.Where(p => p.Quantity <= p.LowStockThreshold);
        }

        var products = await query
            .OrderBy(p => p.Category).ThenBy(p => p.Name).ThenBy(p => p.Size)
            .Select(ToDto)
            .ToListAsync();

        return Results.Ok(products);
    }

    private static async Task<IResult> GetCategoriesAsync(InventoryDbContext db)
    {
        var categories = await db.Products
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Results.Ok(categories);
    }

    private static async Task<IResult> GetProductAsync(Guid id, InventoryDbContext db)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ToDto)
            .SingleOrDefaultAsync();

        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> CreateProductAsync(
        CreateProductRequest request, ClaimsPrincipal user, InventoryDbContext db)
    {
        var errors = Validate(request.Sku, request.Name, request.Category, request.Size,
            request.Color, request.Price, request.LowStockThreshold);

        if (request.InitialQuantity < 0)
        {
            errors["InitialQuantity"] = ["Cantitatea initiala nu poate fi negativa."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == sku))
        {
            return Results.Problem($"Exista deja un produs cu SKU-ul '{sku}'.", statusCode: StatusCodes.Status409Conflict);
        }

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            Sku = sku,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Size = request.Size.Trim().ToUpperInvariant(),
            Color = request.Color.Trim(),
            Price = request.Price,
            Quantity = request.InitialQuantity,
            LowStockThreshold = request.LowStockThreshold,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        if (request.InitialQuantity > 0)
        {
            product.Movements.Add(new StockMovement
            {
                Delta = request.InitialQuantity,
                QuantityAfter = request.InitialQuantity,
                Reason = "Stoc initial",
                UserId = user.GetUserId(),
                CreatedAtUtc = now,
            });
        }

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return Results.Created($"/api/products/{product.Id}", ToDtoInMemory(product));
    }

    private static async Task<IResult> UpdateProductAsync(
        Guid id, UpdateProductRequest request, InventoryDbContext db)
    {
        var errors = Validate(request.Sku, request.Name, request.Category, request.Size,
            request.Color, request.Price, request.LowStockThreshold);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return Results.NotFound();
        }

        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == sku && p.Id != id))
        {
            return Results.Problem($"Alt produs foloseste deja SKU-ul '{sku}'.", statusCode: StatusCodes.Status409Conflict);
        }

        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Category = request.Category.Trim();
        product.Size = request.Size.Trim().ToUpperInvariant();
        product.Color = request.Color.Trim();
        product.Price = request.Price;
        product.LowStockThreshold = request.LowStockThreshold;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ToDtoInMemory(product));
    }

    private static async Task<IResult> DeleteProductAsync(Guid id, InventoryDbContext db)
    {
        var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync();
        return deleted > 0 ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AdjustStockAsync(
        Guid id, AdjustStockRequest request, ClaimsPrincipal user, InventoryDbContext db)
    {
        if (request.Delta == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Delta"] = ["Cantitatea nu poate fi 0."],
            });
        }

        if (request.Reason?.Length > 200)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Reason"] = ["Motivul poate avea maxim 200 de caractere."],
            });
        }

        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Un singur UPDATE atomic: doi angajati care scot ultima bucata in acelasi timp
        // nu pot duce stocul sub zero.
        var updated = await db.Products
            .Where(p => p.Id == id && p.Quantity + request.Delta >= 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Quantity, p => p.Quantity + request.Delta)
                .SetProperty(p => p.UpdatedAtUtc, now));

        if (updated == 0)
        {
            var currentQuantity = await db.Products
                .Where(p => p.Id == id)
                .Select(p => (int?)p.Quantity)
                .SingleOrDefaultAsync();

            return currentQuantity is null
                ? Results.NotFound()
                : Results.Problem(
                    $"Stoc insuficient: sunt {currentQuantity} bucati, ai incercat sa scoti {-request.Delta}.",
                    statusCode: StatusCodes.Status409Conflict);
        }

        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);

        db.StockMovements.Add(new StockMovement
        {
            ProductId = id,
            Delta = request.Delta,
            QuantityAfter = product.Quantity,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            UserId = user.GetUserId(),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Ok(ToDtoInMemory(product));
    }

    private static async Task<IResult> GetMovementsAsync(Guid id, InventoryDbContext db)
    {
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == id)
            .OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)
            .Take(50)
            .Select(m => new StockMovementDto(m.Id, m.Delta, m.QuantityAfter, m.Reason, m.User.Username, m.CreatedAtUtc))
            .ToListAsync();

        return Results.Ok(movements);
    }

    private static Dictionary<string, string[]> Validate(
        string sku, string name, string category, string size, string color, decimal price, int lowStockThreshold)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(sku) || !SkuRegex().IsMatch(sku.Trim()))
            errors["Sku"] = ["SKU-ul e obligatoriu: 3-40 caractere, doar litere, cifre si '-'."];

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            errors["Name"] = ["Numele e obligatoriu (maxim 120 de caractere)."];

        if (string.IsNullOrWhiteSpace(category) || category.Trim().Length > 60)
            errors["Category"] = ["Categoria e obligatorie (maxim 60 de caractere)."];

        if (string.IsNullOrWhiteSpace(size) || size.Trim().Length > 10)
            errors["Size"] = ["Marimea e obligatorie (maxim 10 caractere)."];

        if (string.IsNullOrWhiteSpace(color) || color.Trim().Length > 30)
            errors["Color"] = ["Culoarea e obligatorie (maxim 30 de caractere)."];

        if (price < 0)
            errors["Price"] = ["Pretul nu poate fi negativ."];

        if (lowStockThreshold < 0)
            errors["LowStockThreshold"] = ["Pragul de stoc redus nu poate fi negativ."];

        return errors;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{3,40}$")]
    private static partial Regex SkuRegex();
}