using System.Diagnostics;
using System.Text.Json.Serialization;
using BillingService.Errors;
using BillingService.Health;
using BillingService.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
}).ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
{
    var problem = new ValidationProblemDetails(context.ModelState)
    {
        Status = 400, Title = "Dados inválidos.", Instance = context.HttpContext.Request.Path
    };
    problem.Extensions["code"] = "VALIDATION_ERROR";
    problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    var result = new BadRequestObjectResult(problem);
    result.ContentTypes.Add("application/problem+json");
    return result;
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions.TryAdd("code", $"HTTP_{context.ProblemDetails.Status}");
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<BillingExceptionHandler>();
builder.Services.AddBillingInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<BillingDatabaseHealthCheck>("billing-database", tags: ["ready"]);
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
    || uri.Scheme is not ("http" or "https") || origin.Contains('*')))
    throw new InvalidOperationException("Configure Cors:AllowedOrigins com origens HTTP/HTTPS explícitas.");
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST", "OPTIONS")
        .WithExposedHeaders("Location", "X-Correlation-ID");
}));

var app = builder.Build();
app.Use(async (context, next) =>
{
    var incoming = context.Request.Headers["X-Correlation-ID"].ToString();
    var correlationId = incoming.Length is > 0 and <= 100
        && incoming.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
        ? incoming : Guid.NewGuid().ToString("D");
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    using var scope = app.Logger.BeginScope(new Dictionary<string, object>
    {
        ["Service"] = "BillingService", ["CorrelationId"] = correlationId
    });
    var started = Stopwatch.GetTimestamp();
    await next(context);
    app.Logger.LogInformation("{Method} {Path} retornou {StatusCode} em {ElapsedMs} ms",
        context.Request.Method, context.Request.Path, context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program;
