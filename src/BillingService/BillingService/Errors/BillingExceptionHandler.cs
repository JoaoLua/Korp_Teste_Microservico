using System.Diagnostics;
using BillingService.Application.Contracts;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Errors;

public sealed class BillingExceptionHandler(IProblemDetailsService problems, ILogger<BillingExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }
        var sqlException = exception as SqlException ?? exception.InnerException as SqlException;
        var (status, code, detail) = exception switch
        {
            BillingException known => (known.StatusCode, known.Code, known.Message),
            BadHttpRequestException bad => (bad.StatusCode, "VALIDATION_ERROR", "A requisição é inválida ou excede o tamanho permitido."),
            DbUpdateConcurrencyException => (409, "CONCURRENCY_CONFLICT", "Os dados mudaram durante a operação. Consulte o estado atual e tente novamente."),
            _ when sqlException?.Number is 2601 or 2627 => (409, "UNIQUE_CONFLICT", "A chave ou operação já está em uso. Consulte o estado atual antes de tentar novamente."),
            _ when sqlException?.Number is -2 or 53 or 64 or 233 or 4060 or 10054 or 10060
                or 10928 or 10929 or 40197 or 40501 or 40613 or 1205 or 1222 or 18456 or 51001
                => (503, "DATABASE_UNAVAILABLE", "O banco está indisponível ou ocupado. Consulte a nota antes de tentar novamente; se continuar aberta, repita a impressão da mesma nota."),
            _ => (500, "INTERNAL_ERROR", "Não foi possível concluir a operação. Consulte o estado antes de tentar novamente.")
        };
        if (status >= 500) logger.LogError(exception, "Falha na requisição: {ErrorCode}", code);
        else logger.LogWarning("Requisição recusada: {ErrorCode} {Detail}", code, detail);
        context.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status, Title = code, Detail = detail, Instance = context.Request.Path,
            Extensions = { ["code"] = code, ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier }
        };
        if (status == 503) context.Response.Headers.RetryAfter = "2";
        if (!await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = details }))
            await context.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }
}
