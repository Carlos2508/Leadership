using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace SchoolPlatform.Host.Academic;

/// <summary>
/// Minimal reader for the first sheet of an .xlsx file (Office Open XML) or a CSV saved from Excel (comma or semicolon,
/// UTF-8 or Windows-1252). Only text/number values are read: no formulas are evaluated, no macros or external links are
/// opened. Returns rows of trimmed strings; dates stored as Excel serial numbers are returned as yyyy-MM-dd when the
/// header of that column looks like a date.
/// </summary>
public static class SpreadsheetReader
{
    public const int MaxRows = 1000;
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static List<string[]> Read(Stream content, string fileName)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        buffer.Position = 0;
        var isZip = buffer.Length > 4 && buffer.GetBuffer()[0] == 'P' && buffer.GetBuffer()[1] == 'K';
        if (isZip) return ReadXlsx(buffer);
        if (fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El formato .xls antiguo no es compatible. Guarda el archivo como .xlsx o .csv.");
        return ReadCsv(buffer);
    }

    private static List<string[]> ReadXlsx(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var shared = new List<string>();
        if (zip.GetEntry("xl/sharedStrings.xml") is { } sharedEntry)
        {
            var doc = Load(sharedEntry);
            shared.AddRange(doc.Root!.Elements(Main + "si").Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value))));
        }
        var sheetPath = FirstSheetPath(zip) ?? throw new InvalidDataException("El libro no tiene hojas.");
        var sheet = Load(zip.GetEntry(sheetPath) ?? throw new InvalidDataException("No se encontró la primera hoja."));
        var rows = new List<string[]>();
        foreach (var row in sheet.Descendants(Main + "row"))
        {
            if (rows.Count > MaxRows) throw new InvalidDataException($"El archivo supera {MaxRows} filas. Divídelo por grado.");
            var values = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                var column = ColumnIndex((string?)cell.Attribute("r"), values.Count == 0 ? 0 : values.Keys.Max() + 1);
                var type = (string?)cell.Attribute("t");
                var raw = cell.Element(Main + "v")?.Value;
                var text = type switch
                {
                    "s" when int.TryParse(raw, out var index) && index >= 0 && index < shared.Count => shared[index],
                    "inlineStr" => string.Concat(cell.Descendants(Main + "t").Select(t => t.Value)),
                    _ => raw ?? ""
                };
                values[column] = text.Trim();
            }
            if (values.Count == 0) continue;
            var width = values.Keys.Max() + 1;
            rows.Add(Enumerable.Range(0, width).Select(i => values.TryGetValue(i, out var v) ? v : "").ToArray());
        }
        return rows;
    }

    private static string? FirstSheetPath(ZipArchive zip)
    {
        var workbook = zip.GetEntry("xl/workbook.xml");
        var rels = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbook is not null && rels is not null)
        {
            var first = Load(workbook).Descendants(Main + "sheet").FirstOrDefault();
            var id = (string?)first?.Attribute(Rel + "id");
            var target = Load(rels).Root!.Elements(PackageRel + "Relationship").FirstOrDefault(r => (string?)r.Attribute("Id") == id)?.Attribute("Target")?.Value;
            if (target is not null)
                return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        }
        return zip.Entries.Select(e => e.FullName).Where(n => n.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)).Order().FirstOrDefault();
    }

    private static XDocument Load(ZipArchiveEntry entry)
    {
        if (entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("El archivo es demasiado grande.");
        using var s = entry.Open();
        return XDocument.Load(s, LoadOptions.None);
    }

    private static int ColumnIndex(string? reference, int fallback)
    {
        if (string.IsNullOrEmpty(reference)) return fallback;
        var index = 0;
        foreach (var ch in reference)
        {
            if (!char.IsLetter(ch)) break;
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return index == 0 ? fallback : index - 1;
    }

    private static List<string[]> ReadCsv(MemoryStream buffer)
    {
        var bytes = buffer.ToArray();
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { text = Encoding.Latin1.GetString(bytes); }
        text = text.TrimStart('﻿');
        var firstLine = text.Split('\n')[0];
        var separator = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var row = new List<string>();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == separator) { row.Add(field.ToString().Trim()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString().Trim()); field.Clear();
                if (row.Any(x => x.Length > 0)) rows.Add(row.ToArray());
                row = [];
                if (rows.Count > MaxRows + 1) throw new InvalidDataException($"El archivo supera {MaxRows} filas. Divídelo por grado.");
            }
            else field.Append(c);
        }
        row.Add(field.ToString().Trim());
        if (row.Any(x => x.Length > 0)) rows.Add(row.ToArray());
        return rows;
    }

    /// <summary>Accepts dd/MM/yyyy, yyyy-MM-dd or an Excel serial date.</summary>
    public static DateOnly? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string[] formats = ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "dd-MM-yyyy"];
        if (DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 1 and < 80000)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        throw new FormatException();
    }
}
