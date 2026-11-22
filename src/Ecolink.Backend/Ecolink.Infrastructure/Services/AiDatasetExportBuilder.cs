using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Tạo luồng nén .zip cho tập dữ liệu chuẩn hóa theo định dạng YOLO hoặc ImageFolder (S-03).
/// </summary>
public static class AiDatasetExportBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<DatasetArchiveExportResult> BuildZipAsync(
        AiDatasetVersion version,
        IReadOnlyList<AiDatasetVersionItem> versionItems,
        string format,
        CancellationToken ct = default)
    {
        var normalizedFormat = string.Equals(format?.Trim(), "imagefolder", StringComparison.OrdinalIgnoreCase)
            ? "imagefolder"
            : "yolo";

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (normalizedFormat == "yolo")
            {
                await WriteTextEntryAsync(archive, "data.yaml", BuildDataYaml(version), ct);
            }

            var manifestEntries = new List<object>(versionItems.Count);
            foreach (var vi in versionItems)
            {
                var splitDir = vi.AssignedSplit switch
                {
                    DatasetSplitType.Train => "train",
                    DatasetSplitType.Validation => "val",
                    DatasetSplitType.Test => "test",
                    _ => "train"
                };

                var item = vi.DatasetItem;
                var categoryCode = vi.CategoryCodeSnapshot;
                var ext = AiCategoryNormalizer.SanitizeFileExtension(item?.ImageUrl);
                var boxes = ParseBoxes(vi.YoloAnnotationsSnapshot);

                if (normalizedFormat == "yolo")
                {
                    var labelLines = BuildYoloLabelLines(boxes, categoryCode);
                    var labelPath = $"labels/{splitDir}/{vi.DatasetItemId}.txt";
                    await WriteTextEntryAsync(archive, labelPath, labelLines, ct);

                    var imageRefPath = $"images/{splitDir}/{vi.DatasetItemId}.url.txt";
                    await WriteTextEntryAsync(archive, imageRefPath, item?.ImageUrl ?? string.Empty, ct);
                }
                else
                {
                    var folderEntry = $"{splitDir}/{categoryCode}/{vi.DatasetItemId}{ext}.url.txt";
                    await WriteTextEntryAsync(archive, folderEntry, item?.ImageUrl ?? string.Empty, ct);
                }

                manifestEntries.Add(new
                {
                    itemId = vi.DatasetItemId,
                    imageUrl = item?.ImageUrl ?? string.Empty,
                    imageSha256 = item?.ImageSha256 ?? string.Empty,
                    split = splitDir,
                    category = categoryCode,
                    annotations = boxes
                });
            }

            var manifestPayload = new
            {
                versionCode = version.VersionCode,
                versionName = version.VersionName,
                format = normalizedFormat,
                ratios = new
                {
                    train = version.TrainRatio,
                    validation = version.ValRatio,
                    test = version.TestRatio
                },
                totalSamples = version.TotalSamples,
                trainCount = version.TrainCount,
                valCount = version.ValCount,
                testCount = version.TestCount,
                imbalanceRatio = version.ImbalanceRatio,
                isBalanced = version.IsBalanced,
                exportedAtUtc = DateTime.UtcNow,
                items = manifestEntries
            };

            await WriteTextEntryAsync(
                archive,
                "manifest.json",
                JsonSerializer.Serialize(manifestPayload, JsonOptions),
                ct);
        }

        return new DatasetArchiveExportResult
        {
            Content = ms.ToArray(),
            FileName = $"ecolink-dataset-{version.VersionCode}-{normalizedFormat}.zip",
            ContentType = "application/zip",
            SampleCount = versionItems.Count
        };
    }

    private static string BuildDataYaml(AiDatasetVersion version)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# EcoLink Curated Dataset {version.VersionCode} ({version.VersionName})");
        sb.AppendLine("path: .");
        sb.AppendLine("train: images/train");
        sb.AppendLine("val: images/val");
        sb.AppendLine("test: images/test");
        sb.AppendLine($"nc: {AiCategoryNormalizer.YoloClassOrder.Length}");
        sb.AppendLine("names:");
        for (var i = 0; i < AiCategoryNormalizer.YoloClassOrder.Length; i++)
        {
            sb.AppendLine($"  {i}: {AiCategoryNormalizer.YoloClassOrder[i]}");
        }

        return sb.ToString();
    }

    private static string BuildYoloLabelLines(IReadOnlyList<YoloBoundingBoxDto> boxes, string defaultCategory)
    {
        if (boxes.Count == 0)
        {
            var classId = AiCategoryNormalizer.GetYoloClassIndex(defaultCategory);
            return $"{classId} 0.500000 0.500000 1.000000 1.000000\n";
        }

        var sb = new StringBuilder();
        foreach (var box in boxes)
        {
            var classId = AiCategoryNormalizer.GetYoloClassIndex(
                string.IsNullOrWhiteSpace(box.CategoryCode) ? defaultCategory : box.CategoryCode);
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1:F6} {2:F6} {3:F6} {4:F6}\n",
                classId,
                box.XCenter,
                box.YCenter,
                box.Width,
                box.Height);
        }

        return sb.ToString();
    }

    private static List<YoloBoundingBoxDto> ParseBoxes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<YoloBoundingBoxDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<YoloBoundingBoxDto>>(json, JsonOptions) ?? new List<YoloBoundingBoxDto>();
        }
        catch
        {
            return new List<YoloBoundingBoxDto>();
        }
    }

    private static async Task WriteTextEntryAsync(ZipArchive archive, string entryPath, string content, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), ct);
    }
}
