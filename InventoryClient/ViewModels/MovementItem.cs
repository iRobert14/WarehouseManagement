using InventoryCommon.Contracts;

namespace InventoryClient.ViewModels;

public record MovementItem(string DeltaText, bool IsIncoming, string Reason, string Details)
{
    public static MovementItem From(StockMovementDto movement) => new(
        movement.Delta > 0 ? $"+{movement.Delta}" : movement.Delta.ToString(),
        movement.Delta > 0,
        movement.Reason ?? "Fără motiv",
        $"{movement.Username} · {movement.CreatedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm} · rămas {movement.QuantityAfter}");
}