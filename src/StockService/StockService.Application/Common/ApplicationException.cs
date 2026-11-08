namespace StockService.Application.Common;

public sealed class BusinessException(int statusCode, string code, string message,
    IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static BusinessException Validation(string field, string message) =>
        new(400, "VALIDATION_ERROR", "Revise os dados informados.", new Dictionary<string, string[]> { [field] = [message] });
}
