namespace StockService.Domain.Enums;

public enum StockOperationStatus : byte
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
