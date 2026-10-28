namespace InventoryCommon.Contracts;

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Category,
    string Size,
    string Color,
    decimal Price,
    int Quantity,
    int LowStockThreshold,
    bool IsLowStock,
    DateTime UpdatedAtUtc);

public record CreateProductRequest(
    string Sku,
    string Name,
    string Category,
    string Size,
    string Color,
    decimal Price,
    int LowStockThreshold,
    int InitialQuantity);

public record UpdateProductRequest(
    string Sku,
    string Name,
    string Category,
    string Size,
    string Color,
    decimal Price,
    int LowStockThreshold);

public record AdjustStockRequest(int Delta, string? Reason);

public record StockMovementDto(
    long Id,
    int Delta,
    int QuantityAfter,
    string? Reason,
    string Username,
    DateTime CreatedAtUtc);