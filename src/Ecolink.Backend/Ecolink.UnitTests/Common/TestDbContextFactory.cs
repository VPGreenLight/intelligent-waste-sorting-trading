using System.Net;
using System.Text;
using Ecolink.Domain.Entities;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Ecolink.UnitTests.Common;

public sealed class TestApplicationDbContext : ApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var wktWriter = new WKTWriter();
        var wktReader = new WKTReader();

        modelBuilder.Entity<RecyclerProfile>()
            .Property(r => r.Location)
            .HasConversion(
                v => v == null ? null : wktWriter.Write(v),
                v => string.IsNullOrEmpty(v) ? null : CreatePointFromWkt(wktReader, v));
    }

    private static Point CreatePointFromWkt(WKTReader reader, string wkt)
    {
        var geom = (Point)reader.Read(wkt);
        geom.SRID = 4326;
        return geom;
    }
}

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? $"ecolink_unit_test_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;

        var db = new TestApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static IConfiguration CreateConfiguration(Dictionary<string, string?>? settings = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["AiService:BaseUrl"] = "http://localhost:8000",
            ["GoogleAuth:ClientId"] = "mock-google-client-id",
            ["CloudflareR2:AccountId"] = "test-r2-account-id",
            ["CloudflareR2:AccessKeyId"] = "test-r2-access-key",
            ["CloudflareR2:SecretAccessKey"] = "test-r2-secret-key",
            ["CloudflareR2:BucketName"] = "ecolink-test-bucket",
            ["CloudflareR2:PublicUrl"] = "https://cdn.ecolink.test"
        };

        if (settings != null)
        {
            foreach (var kv in settings)
            {
                defaults[kv.Key] = kv.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }
}

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public List<HttpRequestMessage> CapturedRequests { get; } = new();
    public List<string?> CapturedBodies { get; } = new();

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public static MockHttpMessageHandler FromJson(HttpStatusCode statusCode, string jsonBody)
    {
        return new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });
    }

    public static MockHttpMessageHandler FromException(Exception exception)
    {
        return new MockHttpMessageHandler((_, _) => throw exception);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);
        if (request.Content != null)
        {
            CapturedBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }
        else
        {
            CapturedBodies.Add(null);
        }

        return await _handler(request, cancellationToken);
    }
}

