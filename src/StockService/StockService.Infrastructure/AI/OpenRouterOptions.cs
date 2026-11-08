namespace StockService.Infrastructure.AI;

public sealed class OpenRouterOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "nvidia/nemotron-3.5-lightning:free";
    public int TimeoutSeconds { get; set; } = 30;
}
