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
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .AddInterceptors(interceptor);
            });
        }

        // Đăng ký bộ nhớ đệm IMemoryCache phục vụ lưu kết quả Geocoding Proxy & tối ưu truy vấn
        services.AddMemoryCache();

        // Đăng ký HttpClient gọi AI Vision Service
        services.AddHttpClient<IAiVisionClient, AiVisionHttpClient>();

        // Đăng ký HttpClient cho Backend Geocoding Proxy (Nominatim / LocationIQ)
        services.AddHttpClient<IGeocodingProxyService, GeocodingProxyService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });

        // Đăng ký Rule Engine
        services.AddSingleton<IWasteRuleEngineService, WasteRuleEngineService>();

        // Đăng ký Geo Matching
        services.AddScoped<IGeoMatchingService, GeoMatchingService>();

        // Đăng ký Gamification & Trade Services
        services.AddSingleton<IGamificationService, GamificationService>();
        services.AddScoped<ITradeTransactionService, TradeTransactionService>();

        // Đăng ký In-App Real-Time Negotiation Chatbox Service (Module NEG: U-08 & R-05)
        services.AddScoped<INegotiationChatService, NegotiationChatService>();

        // Đăng ký Authentication & Token & Email Services (Module 1 - AUTH)
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailService, GmailSmtpEmailService>();
        services.AddScoped<IAuthService, AuthService>();

        // Đăng ký HttpClient cho Cloudflare R2 Object Storage (KYB Permits, Facility Photos, Chat Attachments)
        services.AddHttpClient<IFileStorageService, CloudflareR2StorageService>();

        // Đăng ký Recycler Onboarding & Verification Service (Module 3: U-17 & S-04)
        services.AddScoped<IRecyclerOnboardingService, RecyclerOnboardingService>();

        // Đăng ký Recycler Profile, Pricing & Availability Service (R-01, R-02)
        services.AddScoped<IRecyclerManagementService, RecyclerManagementService>();

        // Handover Request Management (U-06, U-07, R-03, R-04)
        services.AddScoped<IHandoverRequestService, HandoverRequestService>();

        services.AddScoped<IHandoverReceiptService, HandoverReceiptService>();

        // Đăng ký Payment Service (VietQR & Webhook)
        services.AddSingleton<IPaymentService, PaymentService>();

        // Đăng ký PayOS Gateway Service (Pay-in & Pay-out)
        services.AddSingleton<IPayOsService, PayOsService>();

        return services;
    }
}
