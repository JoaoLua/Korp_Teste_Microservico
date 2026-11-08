using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockService.Application.Common;

namespace StockService.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return true;
        var error = Translate(exception);
        if (error.StatusCode >= 500)
            logger.LogError(exception, "Stock request failed with {Code} {TraceId}", error.Code, context.TraceIdentifier);
        else
            logger.LogWarning("Stock request rejected with {Code} {TraceId}", error.Code, context.TraceIdentifier);

        var details = error.Errors is null ? new ProblemDetails() : new ValidationProblemDetails(error.Errors);
        details.Status = error.StatusCode;
        details.Title = error.Message;
        details.Instance = context.Request.Path;
        details.Extensions["code"] = error.Code;
        details.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.Response.StatusCode = error.StatusCode;
        if (error.StatusCode == 503) context.Response.Headers.RetryAfter = "2";
        if (!await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = details, Exception = exception }))
            await context.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }

    private static BusinessException Translate(Exception exception)
    {
        if (exception is BusinessException business) return business;
        if (exception is DbUpdateConcurrencyException)
            return new(409, "ROW_VERSION_CONFLICT", "O produto foi alterado. Atualize os dados antes de tentar novamente.");
        if (exception is BadHttpRequestException badRequest)
            return new(badRequest.StatusCode, "INVALID_REQUEST", "A requisição não atende ao contrato da API.");
        var sql = exception as SqlException ?? exception.InnerException as SqlException;
        if (sql?.Number is 2601 or 2627)
            return sql.Message.Contains("UQ_Products_Code", StringComparison.Ordinal)
                ? new(409, "PRODUCT_CODE_CONFLICT", "Já existe um produto com este código.")
                : new(409, "DATA_CONFLICT", "Já existe um registro para esta operação.");
        if (sql?.Number is -2 or 53 or 64 or 233 or 4060 or 10054 or 10060 or 10928 or 10929 or 40197 or 40501 or 40613 or 1205 or 1222 or 18456)
            return new(503, "DATABASE_UNAVAILABLE", "O banco de estoque está temporariamente indisponível. Tente novamente.");
        return new(500, "INTERNAL_ERROR", "Não foi possível concluir a solicitação.");
    }
}
