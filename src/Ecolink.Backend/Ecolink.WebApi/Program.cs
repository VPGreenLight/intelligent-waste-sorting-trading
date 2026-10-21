using System.Text;
using Ecolink.Application;
using Ecolink.Application.Common;
using Ecolink.Infrastructure;
using Ecolink.WebApi.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// Thêm dịch vụ tầng Infrastructure (AI Client, Rule Engine, Geo Matching, Gamification, Auth)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Cấu hình JWT Bearer Authentication & Authorization
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
});
builder.Services.AddAuthorization();

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
                      "  - Cơ sở thu gom: `collector` / `collector123`\n" +
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

// Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Cấu hình HTTP request pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AllowAll");

app.UseAuthentication();
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

app.Run();
