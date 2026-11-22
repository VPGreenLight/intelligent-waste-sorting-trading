using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ecolink.Infrastructure.Data;

/// <summary>
/// Design-time factory phục vụ `dotnet ef migrations` trên Ecolink.Infrastructure
/// ngay cả khi tiến trình Ecolink.WebApi đang chạy và khoá thư mục bin của WebApi.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            })
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        var envConn = Environment.GetEnvironmentVariable("CORE_POSTGRES_CONNECTION_STRING")
                      ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            return envConn;
        }

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Ecolink.WebApi", "appsettings.Development.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Ecolink.Backend", "Ecolink.WebApi", "appsettings.Development.json"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs))
                {
                    if (cs.TryGetProperty("DefaultConnection", out var def) &&
                        def.GetString() is { Length: > 0 } defStr &&
                        !defStr.Contains("{CORE_POSTGRES_CONNECTION_STRING}"))
                    {
                        return defStr;
                    }
                }
            }
            catch
            {
                // Bỏ qua file lỗi cú pháp
            }
        }

        return "Host=localhost;Port=5432;Database=ecolink_db;Username=postgres;Password=postgrespassword;Include Error Detail=true";
    }
}
