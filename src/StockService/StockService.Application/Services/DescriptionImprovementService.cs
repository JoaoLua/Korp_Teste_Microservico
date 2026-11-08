using StockService.Application.Abstractions;
using StockService.Application.Common;
using StockService.Application.Contracts;

namespace StockService.Application.Services;

public sealed class DescriptionImprovementService(IDescriptionImprover improver)
{
    public async Task<ImprovedDescriptionResponse> ImproveAsync(ImproveDescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var description = request.Description?.Trim();
        if (string.IsNullOrEmpty(description) || description.Length > 200)
            throw BusinessException.Validation("description", "Informe uma descrição com 1 a 200 caracteres.");

        var suggestion = (await improver.ImproveAsync(description, cancellationToken)).Trim();
        if (suggestion.Length is 0 or > 200)
            throw new BusinessException(502, "AI_INVALID_RESPONSE",
                "A IA não retornou uma descrição válida de até 200 caracteres. Edite o texto ou tente novamente.");

        return new ImprovedDescriptionResponse(suggestion);
    }
}
