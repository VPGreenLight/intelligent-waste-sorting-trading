using Ecolink.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Thêm Controllers
builder.Services.AddControllers();

// Thêm dịch vụ tầng Infrastructure (AI Client, Rule Engine, Geo Matching, Gamification)
builder.Services.AddInfrastructureServices(builder.Configuration);

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

app.MapGet("/", () => Results.Ok(new
{
    service = "EcoLink Core Business API",
    version = "1.0.0",
    status = "Healthy",
    docs = "/api/v1"
}));

app.MapControllers();

app.Run();
