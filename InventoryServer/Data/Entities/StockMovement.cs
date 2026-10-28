namespace InventoryServer.Data.Entities;

public class StockMovement
{
    public long Id { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Delta { get; set; }
    public int QuantityAfter { get; set; }
    public string? Reason { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}