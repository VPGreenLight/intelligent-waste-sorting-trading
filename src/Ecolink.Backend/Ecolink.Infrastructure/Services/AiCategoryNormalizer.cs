using System.Security.Cryptography;
using System.Text;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Chuẩn hóa mã danh mục phế liệu, chuyển đổi chỉ mục lớp YOLO và định dạng trạng thái M-13.
/// </summary>
public static class AiCategoryNormalizer
{
    public static readonly string[] YoloClassOrder =
    {
        "battery",
        "biological",
        "cardboard",
        "clothes",
        "glass",
        "metal",
        "paper",
        "plastic",
        "shoes",
        "trash"
    };

    private static readonly Dictionary<string, string> LabelAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["plastic"] = "plastic",
        ["plastic_bottle"] = "plastic",
        ["pet"] = "plastic",
        ["hdpe"] = "plastic",
        ["pp"] = "plastic",
        ["nhua"] = "plastic",

        ["paper"] = "paper",
        ["office-paper"] = "paper",
        ["newspaper"] = "paper",
        ["giay"] = "paper",

        ["cardboard"] = "cardboard",
        ["cardboard_box"] = "cardboard",
        ["carton"] = "cardboard",
        ["corrugated"] = "cardboard",

        ["metal"] = "metal",
        ["metal_can"] = "metal",
        ["aluminum"] = "metal",
        ["steel"] = "metal",
        ["kim_loai"] = "metal",

        ["glass"] = "glass",
        ["glass_bottle"] = "glass",
        ["brown-glass"] = "glass",
        ["green-glass"] = "glass",
        ["white-glass"] = "glass",
        ["thuy_tinh"] = "glass",

        ["battery"] = "battery",
        ["pin"] = "battery",
        ["ewaste"] = "battery",
        ["ewaste_disposal"] = "battery",

        ["biological"] = "biological",
        ["organic"] = "biological",
        ["huu_co"] = "biological",

        ["clothes"] = "clothes",
        ["textile"] = "clothes",
        ["quan_ao"] = "clothes",

        ["shoes"] = "shoes",
        ["giay_dep"] = "shoes",

        ["trash"] = "trash"
    };

    private static readonly (string Code, string Name, string BinColor, bool IsRecyclable)[] DefaultCategories =
    {
        ("plastic", "Nhựa tái chế (Plastic)", "Cam", true),
        ("paper", "Giấy báo & Tài liệu (Paper)", "Xanh Dương", true),
        ("glass", "Thủy tinh (Glass)", "Trắng", true),
        ("metal", "Kim loại & Vỏ lon (Metal)", "Xám Kim Loại", true),
        ("cardboard", "Bìa Carton (Cardboard)", "Nâu Vàng", true),
        ("battery", "Pin & Rác nguy hại (Battery)", "Đỏ Nguy Hại", false),
        ("biological", "Rác hữu cơ & Thực phẩm (Biological)", "Xanh Lá", false),
        ("clothes", "Vải vóc & Quần áo cũ (Clothes)", "Tím Đồ Cũ", false),
        ("shoes", "Giày dép cũ (Shoes)", "Tím Đồ Cũ", false),
        ("trash", "Rác thải thông thường / Không thể tái chế", "Đen", false)
    };

    public static string NormalizeCode(string? rawLabel)
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
        {
            return string.Empty;
        }

        var cleaned = rawLabel.Trim().ToLowerInvariant();
        return LabelAliases.TryGetValue(cleaned, out var canonical) ? canonical : cleaned;
    }

    public static int GetYoloClassIndex(string? categoryCode)
    {
        var norm = NormalizeCode(categoryCode);
        var idx = Array.IndexOf(YoloClassOrder, norm);
        return idx >= 0 ? idx : Array.IndexOf(YoloClassOrder, "trash");
    }

    public static async Task EnsureDefaultCategoriesIfEmptyAsync(ApplicationDbContext db, CancellationToken ct)
    {
        if (await db.WasteCategories.AsNoTracking().AnyAsync(ct))
        {
            return;
        }

        foreach (var (code, name, binColor, isRecyclable) in DefaultCategories)
        {
            db.WasteCategories.Add(new WasteCategory
            {
                Code = code,
                Name = name,
                StandardBinColor = binColor,
                IsRecyclable = isRecyclable,
                Status = "Active"
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public static string FormatFeedbackStatus(FeedbackStatus status) => status switch
    {
        FeedbackStatus.Verified => "Reviewed",
        _ => status.ToString()
    };

    public static bool TryParseFeedbackStatus(string? rawStatus, out FeedbackStatus status)
    {
        status = FeedbackStatus.Pending;
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return false;
        }

        var trimmed = rawStatus.Trim();
        if (string.Equals(trimmed, "Reviewed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Verified", StringComparison.OrdinalIgnoreCase))
        {
            status = FeedbackStatus.Verified;
            return true;
        }

        return Enum.TryParse(trimmed, true, out status);
    }

    public static string ComputeSha256Hex(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string ComputeSha256Hex(string input)
        => ComputeSha256Hex(Encoding.UTF8.GetBytes(input));

    public static string SanitizeFileExtension(string? fileNameOrUrl)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrUrl))
        {
            return ".jpg";
        }

        var ext = Path.GetExtension(fileNameOrUrl.Split('?')[0])?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" => ext,
            _ => ".jpg"
        };
    }
}
