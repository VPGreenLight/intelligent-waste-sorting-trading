using Ecolink.Application.Interfaces;
using Ecolink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecolink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Đăng ký Interceptor kiểm toán tự động & xóa mềm
        services.AddScoped<Data.Interceptors.AuditableEntityInterceptor>();

        // Đăng ký CSDL PostgreSQL + PostGIS NetTopologySuite
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? configuration.GetConnectionString("PostgreSQL");

        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("{CORE_POSTGRES_CONNECTION_STRING}"))
        {
            services.AddDbContext<Data.ApplicationDbContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<Data.Interceptors.AuditableEntityInterceptor>();
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.MigrationsAssembly(typeof(Data.ApplicationDbContext).Assembly.FullName);
                })
                .AddInterceptors(interceptor);
            });
        }

        // Đăng ký HttpClient gọi AI Vision Service
        services.AddHttpClient<IAiVisionClient, AiVisionHttpClient>();

        // Đăng ký Rule Engine
        services.AddSingleton<IWasteRuleEngineService, WasteRuleEngineService>();

        // Đăng ký Geo Matching
        services.AddSingleton<IGeoMatchingService, GeoMatchingService>();

        // Đăng ký Gamification & Trade Services
        services.AddSingleton<IGamificationService, GamificationService>();
        services.AddSingleton<ITradeTransactionService, TradeTransactionService>();

        // Đăng ký Authentication & Token & Email Services (Module 1 - AUTH)
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailService, GmailSmtpEmailService>();
        services.AddScoped<IAuthService, AuthService>();

        // Đăng ký Payment Service (VietQR & Webhook)
        services.AddSingleton<IPaymentService, PaymentService>();

        // Đăng ký PayOS Gateway Service (Pay-in & Pay-out)
        services.AddSingleton<IPayOsService, PayOsService>();

        return services;
    }
}
