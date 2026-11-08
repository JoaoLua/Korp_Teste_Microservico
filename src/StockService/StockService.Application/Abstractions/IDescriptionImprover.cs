namespace StockService.Application.Abstractions;

public interface IDescriptionImprover
{
    Task<string> ImproveAsync(string description, CancellationToken cancellationToken);
}
