using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockService.Application.Abstractions;
using StockService.Application.Services;
using StockService.Infrastructure.Persistence;

namespace StockService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStockInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<StockDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IStockStore, StockStore>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockDeductionService>();
        return services;
    }
}
