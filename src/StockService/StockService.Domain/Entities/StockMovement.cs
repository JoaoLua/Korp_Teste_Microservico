using StockService.Domain.Enums;

namespace StockService.Domain.Entities;

public sealed class StockMovement
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public StockMovementType MovementType { get; set; }
    public string? Reason { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? OriginalMovementId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public StockMovement? OriginalMovement { get; set; }
    public StockMovement? Reversal { get; set; }
}
