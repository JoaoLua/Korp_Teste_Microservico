namespace StockService.Application.Contracts;

public sealed record ImproveDescriptionRequest(string? Description);
public sealed record ImprovedDescriptionResponse(string Description);
