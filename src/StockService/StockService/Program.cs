using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using StockService.Errors;
using StockService.Health;
using StockService.Application.Abstractions;
using StockService.Application.Services;
using StockService.Infrastructure;
using StockService.Infrastructure.AI;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 256 * 1024);
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Revise os dados informados.",
            Instance = context.HttpContext.Request.Path
        };
        details.Extensions["code"] = "VALIDATION_ERROR";
        details.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(details);
    };
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
    context.ProblemDetails.Extensions.TryAdd("code", "HTTP_ERROR");
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("stock-database", tags: ["ready"]);

var connectionString = builder.Configuration.GetConnectionString("StockDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("A connection string 'StockDb' não foi configurada.");
builder.Services.AddStockInfrastructure(connectionString);
builder.Services.Configure<OpenRouterOptions>(builder.Configuration.GetSection("OpenRouter"));
builder.Services.AddScoped<DescriptionImprovementService>();
builder.Services.AddHttpClient<IDescriptionImprover, OpenRouterDescriptionImprover>((services, client) =>
{
    var settings = services.GetRequiredService<IOptions<OpenRouterOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 60));
    client.MaxResponseContentBufferSize = 64 * 1024;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
if (origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || origin.Contains('*')))
    throw new InvalidOperationException("Configure Cors:AllowedOrigins com origens HTTP/HTTPS explícitas.");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins)
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Location")));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else app.UseHttpsRedirection();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();
app.Run();

public partial class Program;
