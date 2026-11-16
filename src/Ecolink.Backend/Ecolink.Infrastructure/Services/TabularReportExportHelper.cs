using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Trình xuất bảng dữ liệu sang CSV (UTF-8 BOM) và OpenXML Spreadsheet (.xlsx) dùng chung cho M-15 &amp; M-12.
/// </summary>
internal static class TabularReportExportHelper
{
    public static byte[] BuildUtf8BomCsv(string[] headers, IReadOnlyList<string[]> rows)
    {
        static string EscapeCell(string? value)
        {
            var s = value ?? string.Empty;
            return s.IndexOfAny([',', '"', '\r', '\n']) >= 0
                ? $"\"{s.Replace("\"", "\"\"")}\""
                : s;
        }

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM cho Excel trên Windows
        sb.Append(string.Join(",", headers.Select(EscapeCell))).Append("\r\n");

        foreach (var row in rows)
        {
            sb.Append(string.Join(",", row.Select(EscapeCell))).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] BuildMinimalOpenXmlWorkbook(string sheetTitle, string[] headers, IReadOnlyList<string[]> rows)
    {
        XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace ctNs = "http://schemas.openxmlformats.org/package/2006/content-types";

        static string ColName(int colIdx)
        {
            var name = string.Empty;
            int n = colIdx + 1;
            while (n > 0)
            {
                int rem = (n - 1) % 26;
                name = (char)('A' + rem) + name;
                n = (n - 1) / 26;
            }
            return name;
        }

        XElement BuildInlineRow(int rowNum, string[] values) =>
            new(mainNs + "row",
                new XAttribute("r", rowNum),
                values.Select((val, cIdx) =>
                    new XElement(mainNs + "c",
                        new XAttribute("r", $"{ColName(cIdx)}{rowNum}"),
                        new XAttribute("t", "inlineStr"),
                        new XElement(mainNs + "is",
                            new XElement(mainNs + "t", val ?? string.Empty)))));

        var sheetData = new XElement(mainNs + "sheetData");
        sheetData.Add(BuildInlineRow(1, headers));
        for (int i = 0; i < rows.Count; i++)
        {
            sheetData.Add(BuildInlineRow(i + 2, rows[i]));
        }

        var safeName = new string(sheetTitle.Where(char.IsLetterOrDigit).Take(25).ToArray());
        if (string.IsNullOrEmpty(safeName)) safeName = "Report";

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXmlEntry(archive, "[Content_Types].xml", new XDocument(
                new XElement(ctNs + "Types",
                    new XElement(ctNs + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(ctNs + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(ctNs + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(ctNs + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))
                )));

            WriteXmlEntry(archive, "_rels/.rels", new XDocument(
                new XElement(pkgRelNs + "Relationships",
                    new XElement(pkgRelNs + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml")))));

            WriteXmlEntry(archive, "xl/workbook.xml", new XDocument(
                new XElement(mainNs + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", relNs),
                    new XElement(mainNs + "sheets",
                        new XElement(mainNs + "sheet",
                            new XAttribute("name", safeName),
                            new XAttribute("sheetId", "1"),
                            new XAttribute(relNs + "id", "rId1"))))));

            WriteXmlEntry(archive, "xl/_rels/workbook.xml.rels", new XDocument(
                new XElement(pkgRelNs + "Relationships",
                    new XElement(pkgRelNs + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")))));

            WriteXmlEntry(archive, "xl/worksheets/sheet1.xml", new XDocument(
                new XElement(mainNs + "worksheet", sheetData)));
        }

        return ms.ToArray();
    }

    private static void WriteXmlEntry(ZipArchive archive, string entryPath, XDocument doc)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        doc.Save(writer);
    }
}
