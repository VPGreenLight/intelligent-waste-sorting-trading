using System.Text;
using Ecolink.Application;
using Ecolink.Application.Common;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Constants;
using Ecolink.Infrastructure;
using Ecolink.WebApi.Hubs;
using Ecolink.WebApi.Middlewares;
using Ecolink.WebApi.Security;
using Ecolink.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Thêm Controllers kèm chuẩn hoá phản hồi lỗi Model Binding theo api_design_and_response_contract_guidelines.md
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x => $"{e.Key}: {x.ErrorMessage}"))
                .ToList();

            var response = ApiResponse<object>.Fail(
                message: "Dữ liệu đầu vào không hợp lệ.",
                messageCode: "MSG-GEN-01",
                errors: errors
            );

            return new BadRequestObjectResult(response);
        };
    });

// Thêm dịch vụ tầng Application (FluentValidation Validators)
builder.Services.AddApplicationServices();

// Thêm dịch vụ tầng Infrastructure (AI Client, Rule Engine, Geo Matching, Gamification, Auth, Negotiation Chat)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Kiểm tra chế độ CLI seed dữ liệu Demo Hội đồng (không chạy background jobs khi seed một lần)
var isSeedCli = args.Any(a =>
    string.Equals(a, "--seed-demo", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(a, "--reset-and-seed-demo", StringComparison.OrdinalIgnoreCase));

// Đăng ký SignalR Real-Time Hub & Notifier cho Module Chatbox + System Notifications (STT 172)
builder.Services.AddSignalR();
builder.Services.AddScoped<INegotiationChatNotifier, SignalRNegotiationChatNotifier>();
builder.Services.AddScoped<INotificationRealtimeNotifier, SignalRNotificationNotifier>();
if (!isSeedCli)
{
    builder.Services.AddHostedService<Ecolink.WebApi.Services.EventBackgroundWorker>();
}

// Lấy thông tin người dùng hiện tại (UserId/Role) từ JWT cho Controller và Audit Interceptor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
if (!isSeedCli)
{
    builder.Services.AddHostedService<Ecolink.WebApi.Services.HandoverExpirationJob>(); // JOB-03
    builder.Services.AddHostedService<Ecolink.WebApi.Services.ReceiptAutoCompleteJob>(); // JOB-01

    // Xử lý Voucher hết hạn.
    builder.Services.AddHostedService<Ecolink.WebApi.Services.VoucherExpirationJob>();
}

// Cấu hình JWT Bearer Authentication & Authorization (Hỗ trợ cả REST Header và SignalR WebSocket access_token)
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"]
                ?? "EcoLink_Super_Secure_Secret_Key_For_JWT_Signing_2026_XYZ987!";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "EcoLinkBackend";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "EcoLinkApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Cho phép SignalR WebSocket truyền JWT qua query parameter ?access_token=...
    // và tự động trích xuất JWT từ HttpOnly Cookie "accessToken" cho mọi request trình duyệt
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs/negotiation-chat") || path.StartsWithSegments("/hubs/notifications")))
            {
                context.Token = accessToken;
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(context.Token) &&
                string.IsNullOrEmpty(context.Request.Headers.Authorization) &&
                context.Request.Cookies.TryGetValue("accessToken", out var cookieToken) &&
                !string.IsNullOrWhiteSpace(cookieToken))
            {
                context.Token = cookieToken.Trim();
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization(options =>
{
    foreach (var code in StaffPermissions.AllCodes)
    {
        options.AddPolicy(
            $"{HasPermissionAttribute.PolicyPrefix}{code}",
            policy => policy.Requirements.Add(new PermissionRequirement(code)));
    }
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Cấu hình Swagger / OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EcoLink Core Business & Trading API",
        Version = "v1",
        Description = "Hệ thống Phân loại rác thông minh kết hợp Giao dịch thu gom & Đổi thưởng phế liệu EcoLink.\n\n" +
                      "- **Tài khoản Demo có sẵn:**\n" +
                      "  - Người dân: `citizen` / `citizen123`\n" +
                      "  - Cơ sở thu gom (Recycler): `recycler` / `recycler123`\n" +
                      "  - Nhân viên vận hành: `staff` / `staff123`\n" +
                      "  - Quản trị viên: `admin` / `admin123`\n" +
                      "- **Hướng dẫn xác thực:** Đăng nhập tại `POST /api/v1/auth/login`, copy trường `token`, bấm nút **Authorize** ở góc trên bên phải, nhập `Bearer <token>` rồi bấm Authorize."
    });

    // Thêm nút Authorize (JWT Bearer Token) trên giao diện Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập Token vào ô bên dưới (Chỉ cần paste JWT Token hoặc 'Bearer <token>'):"
    });

    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});

// Cấu hình CORS (hỗ trợ cả Bearer Header lẫn HttpOnly RefreshToken Cookie qua Proxy/Tunnel)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition", "X-EcoLink-Message-Code", "X-EcoLink-Row-Count", "X-EcoLink-Sample-Count")
              .AllowCredentials();
    });
});

var app = builder.Build();

if (isSeedCli)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<Ecolink.Infrastructure.Persistence.Seed.DemoDataSeeder>();
    var resetExisting = args.Any(a => string.Equals(a, "--reset-and-seed-demo", StringComparison.OrdinalIgnoreCase));
    var report = await seeder.SeedAsync(resetExisting: resetExisting);
    Console.WriteLine(report.ToSummaryString());
    return;
}

// Cấu hình HTTP request pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseMiddleware<AccountStatusGuardMiddleware>();
app.UseAuthorization();

// Kích hoạt Swagger UI cho cả Local và Tunnel (ngrok)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EcoLink API v1");
    // Đặt RoutePrefix = string.Empty để vào root URL (http://localhost:5017/ hoặc ngrok) là mở ngay giao diện Swagger
    c.RoutePrefix = string.Empty;
});

// Endpoint kiểm tra sức khỏe hệ thống
app.MapGet("/health", () => Results.Ok(new
{
    service = "EcoLink Core Business API",
    version = "1.0.0",
    status = "Healthy",
    time = DateTime.UtcNow
}));

app.MapControllers();
app.MapHub<NegotiationChatHub>("/hubs/negotiation-chat");
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
