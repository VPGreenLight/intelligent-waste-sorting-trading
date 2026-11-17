using Ecolink.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>Múi giờ ICT (UTC+7) cho mọi mốc "ngày" của gamification (check-in reset lúc 00:00 ICT).</summary>
internal static class GamTime
{
    public static readonly TimeSpan Ict = TimeSpan.FromHours(7);
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow + Ict);
    public static DateTime StartOfDayUtc(DateOnly ictDate) =>
        DateTime.SpecifyKind(ictDate.ToDateTime(TimeOnly.MinValue) - Ict, DateTimeKind.Utc);   // Npgsql từ chối Kind=Unspecified với timestamptz
}

internal static class GamErrors
{
    public static BusinessRuleException Rule(string rule, string code, string message) => new(rule, code, message);

    public static bool IsUniqueViolation(DbUpdateException ex, string? constraint = null) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg &&
        (constraint == null || pg.ConstraintName == constraint);

    public static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    public static string Like(string s) => "%" + EscapeLike(s.Trim()) + "%";

    private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>Kiểm tra URL ảnh http(s) và định dạng (MSG-CLS-01); null/rỗng hợp lệ.</summary>
    public static string? CleanImageUrl(string? url, string rule)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var u = url.Trim();
        if (u.Length > 1000) throw Rule(rule, "MSG-NTF-03", "URL ảnh quá dài.");
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw Rule(rule, "MSG-AUTH-02", "URL ảnh không hợp lệ.");
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (ext.Length > 0 && !ImageExt.Contains(ext))
            throw Rule(rule, "MSG-CLS-01", "Định dạng ảnh không được hỗ trợ (chỉ JPG, PNG, WEBP).");
        return u;
    }

    public static string? CleanHttpUrl(string? url, string rule, string label)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var u = url.Trim();
        if (u.Length > 1000) throw Rule(rule, "MSG-NTF-03", $"{label} quá dài.");
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw Rule(rule, "MSG-AUTH-02", $"{label} không hợp lệ.");
        return u;
    }
}