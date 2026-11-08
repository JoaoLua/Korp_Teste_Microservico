namespace BillingService.Domain.Entities;

public sealed class InvoiceSequence
{
    public int Id { get; set; }
    public long CurrentValue { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
