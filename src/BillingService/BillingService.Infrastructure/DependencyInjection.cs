using BillingService.Application.Abstractions;
using BillingService.Application.Services;
using BillingService.Infrastructure.Http;
using BillingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BillingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BillingDb")
            ?? throw new InvalidOperationException("A connection string 'BillingDb' não foi configurada.");
        services.AddDbContext<BillingDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IBillingStore, BillingStore>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddOptions<StockClientOptions>().Bind(configuration.GetSection("Services:Stock"))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https", "Services:Stock:BaseUrl deve ser uma URL HTTP/HTTPS absoluta.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 120, "Timeout do estoque deve estar entre 0 e 120 segundos.")
            .ValidateOnStart();
        services.AddHttpClient<IStockClient, StockClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<StockClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.MaxResponseContentBufferSize = 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });
        return services;
    }
}
