using StockService.Domain.Enums;

namespace StockService.Domain.Entities;

public sealed class StockDeductionOperation
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public StockOperationStatus Status { get; set; }
    public string RequestPayload { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string? ResponsePayload { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
