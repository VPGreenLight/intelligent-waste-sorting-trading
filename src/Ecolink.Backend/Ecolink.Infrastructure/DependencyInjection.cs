using Ecolink.Application.Interfaces;
using Ecolink.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecolink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Đăng ký HttpClient gọi AI Vision Service
        services.AddHttpClient<IAiVisionClient, AiVisionHttpClient>();

        // Đăng ký Rule Engine
        services.AddSingleton<IWasteRuleEngineService, WasteRuleEngineService>();

        // Đăng ký Geo Matching
        services.AddSingleton<IGeoMatchingService, GeoMatchingService>();

        // Đăng ký Gamification & Trade Services
        services.AddSingleton<IGamificationService, GamificationService>();
        services.AddSingleton<ITradeTransactionService, TradeTransactionService>();

        return services;
    }
}
