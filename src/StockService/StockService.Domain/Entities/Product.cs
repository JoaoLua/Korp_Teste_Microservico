namespace StockService.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Balance { get; set; }
    public int MinimumStock { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<StockMovement> StockMovements { get; set; } = [];
}
