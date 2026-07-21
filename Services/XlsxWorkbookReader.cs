using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace WorkingSprintAgent.Services;

internal sealed record WorkbookSheet(
    string Name,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

/// <summary>
/// Minimal, dependency-free Open XML workbook reader for tabular sprint workbooks.
/// Formula cells use the cached value written by Excel.
/// </summary>
internal static class XlsxWorkbookReader
{
    private const int MaximumSheets = 25;
    private const int MaximumRowsPerSheet = 100_000;
    private const int MaximumCellsPerSheet = 2_000_000;
    private const long MaximumXmlPartBytes = 50L * 1024 * 1024;
    private const long MaximumExpandedWorkbookBytes = 150L * 1024 * 1024;
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<WorkbookSheet> Read(Stream source)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaximumXmlPartBytes || expandedBytes > MaximumExpandedWorkbookBytes - entry.Length)
            {
                throw new InvalidDataException("The Excel workbook expands beyond the supported 150 MB limit.");
            }
            expandedBytes += entry.Length;
        }

        var workbook = LoadXml(archive, "xl/workbook.xml");
        var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels")
            .Root?
            .Elements(PackageRelationships + "Relationship")
            .Where(element => element.Attribute("Id") is not null && element.Attribute("Target") is not null)
            .ToDictionary(
                element => element.Attribute("Id")!.Value,
                element => NormalizeTarget(element.Attribute("Target")!.Value),
                StringComparer.Ordinal) ?? new Dictionary<string, string>();
        var sharedStrings = ReadSharedStrings(archive);
        var result = new List<WorkbookSheet>();
        var workbookSheetElements = workbook.Descendants(Spreadsheet + "sheet").ToList();
        if (workbookSheetElements.Count > MaximumSheets)
        {
            throw new InvalidDataException($"The Excel workbook contains more than the supported maximum of {MaximumSheets} sheets.");
        }

        foreach (var sheet in workbookSheetElements)
        {
            var name = sheet.Attribute("name")?.Value?.Trim();
            var relationshipId = sheet.Attribute(OfficeRelationships + "id")?.Value;
            if (string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(relationshipId)
                || !relationships.TryGetValue(relationshipId, out var target))
            {
                continue;
            }

            var entry = archive.GetEntry(target);
            if (entry is null)
            {
                continue;
            }
            if (entry.Length > MaximumXmlPartBytes)
            {
                throw new InvalidDataException($"Worksheet '{name}' exceeds the supported 50 MB expanded size.");
            }

            using var stream = entry.Open();
            var worksheet = XDocument.Load(stream, LoadOptions.None);
            var rows = ReadRows(worksheet, sharedStrings);
            if (rows.Count > 0)
            {
                result.Add(new WorkbookSheet(name, rows));
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("The Excel workbook does not contain any readable tabular sheets.");
        }

        return result;
    }

    private static List<IReadOnlyDictionary<string, string>> ReadRows(
        XDocument worksheet,
        IReadOnlyList<string> sharedStrings)
    {
        var worksheetRows = worksheet.Descendants(Spreadsheet + "row").ToList();
        if (worksheetRows.Count > MaximumRowsPerSheet)
        {
            throw new InvalidDataException($"A worksheet contains more than the supported maximum of {MaximumRowsPerSheet:N0} rows.");
        }
        var cellCount = worksheetRows.Sum(row => (long)row.Elements(Spreadsheet + "c").Count());
        if (cellCount > MaximumCellsPerSheet)
        {
            throw new InvalidDataException($"A worksheet contains more than the supported maximum of {MaximumCellsPerSheet:N0} cells.");
        }

        var materializedRows = worksheetRows
            .Select(row => ReadCells(row, sharedStrings))
            .Where(row => row.Count > 0 && row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();
        if (materializedRows.Count < 2)
        {
            return [];
        }

        var headerRow = materializedRows[0];
        var maxColumn = materializedRows.Max(row => row.Keys.DefaultIfEmpty(-1).Max());
        var headers = Enumerable.Range(0, maxColumn + 1)
            .Select(index => headerRow.TryGetValue(index, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : $"Column{index + 1}")
            .ToArray();
        var rows = new List<IReadOnlyDictionary<string, string>>(materializedRows.Count - 1);

        foreach (var sourceRow in materializedRows.Skip(1))
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Length; index++)
            {
                row[headers[index]] = sourceRow.TryGetValue(index, out var value) ? value.Trim() : string.Empty;
            }

            if (row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static Dictionary<int, string> ReadCells(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var result = new Dictionary<int, string>();
        var sequentialIndex = 0;
        foreach (var cell in row.Elements(Spreadsheet + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            var index = string.IsNullOrWhiteSpace(reference)
                ? sequentialIndex
                : GetColumnIndex(reference);
            sequentialIndex = index + 1;
            var type = cell.Attribute("t")?.Value;
            string value;

            if (type == "inlineStr")
            {
                value = string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value));
            }
            else
            {
                value = cell.Element(Spreadsheet + "v")?.Value ?? string.Empty;
                if (type == "s"
                    && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex)
                    && sharedIndex >= 0
                    && sharedIndex < sharedStrings.Count)
                {
                    value = sharedStrings[sharedIndex];
                }
                else if (type == "b")
                {
                    value = value == "1" ? "true" : "false";
                }
            }

            result[index] = value;
        }

        return result;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        if (entry.Length > MaximumXmlPartBytes)
        {
            throw new InvalidDataException("The workbook shared-string table exceeds the supported 50 MB expanded size.");
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream, LoadOptions.None);
        return document
            .Descendants(Spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value)))
            .ToList();
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path)
            ?? throw new InvalidDataException($"The Excel workbook is missing required part '{path}'.");
        if (entry.Length > MaximumXmlPartBytes)
        {
            throw new InvalidDataException($"The workbook part '{path}' exceeds the supported 50 MB expanded size.");
        }
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static string NormalizeTarget(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"xl/{normalized.TrimStart('.', '/')}";
    }

    private static int GetColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            index = (index * 26) + char.ToUpperInvariant(character) - 'A' + 1;
        }

        return Math.Max(0, index - 1);
    }
}
