using Ecolink.Infrastructure;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Thêm Controllers
builder.Services.AddControllers();

// Thêm dịch vụ tầng Infrastructure (AI Client, Rule Engine, Geo Matching, Gamification, Auth)
builder.Services.AddInfrastructureServices(builder.Configuration);

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
app.UseCors("AllowAll");

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
