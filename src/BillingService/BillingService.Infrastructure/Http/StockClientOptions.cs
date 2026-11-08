namespace BillingService.Infrastructure.Http;

public sealed class StockClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public double TimeoutSeconds { get; set; } = 5;
}
