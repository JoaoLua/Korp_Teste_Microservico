using BillingService.Domain.Enums;

namespace BillingService.Domain.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public long Number { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<InvoiceItem> Items { get; set; } = [];
}
