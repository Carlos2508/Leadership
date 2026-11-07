using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace SchoolPlatform.Host.Grades;

/// <summary>
/// Writes a simple .xlsx (one sheet, inline strings and numbers, bold header row, frozen header) without external
/// libraries. Values starting with = + - @ are written as text, never as formulas.
/// </summary>
public static class SpreadsheetWriter
{
    public static byte[] Write(string sheetName, IReadOnlyList<string> title, IReadOnlyList<string> header, IEnumerable<object?[]> rows)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            var safeName = new string(sheetName.Where(c => c is not ('[' or ']' or '*' or '?' or '/' or '\\' or ':')).Take(31).ToArray());
            Add(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                <sheets><sheet name="{Escape(safeName == "" ? "Hoja1" : safeName)}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Add(zip, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
                <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
                <borders count="1"><border/></borders>
                <cellStyleXfs count="1"><xf/></cellStyleXfs>
                <cellXfs count="2"><xf fontId="0"/><xf fontId="1" applyFont="1"/></cellXfs>
                </styleSheet>
                """);
            var sheet = new StringBuilder();
            sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
            var headerRow = title.Count + 1;
            sheet.Append($"""<sheetViews><sheetView workbookViewId="0"><pane ySplit="{headerRow}" topLeftCell="A{headerRow + 1}" state="frozen" activePane="bottomLeft"/></sheetView></sheetViews><sheetData>""");
            var r = 0;
            foreach (var line in title) AppendRow(sheet, ++r, [line], bold: true);
            AppendRow(sheet, ++r, header.Cast<object?>().ToArray(), bold: true);
            foreach (var row in rows) AppendRow(sheet, ++r, row, bold: false);
            sheet.Append("</sheetData></worksheet>");
            Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return output.ToArray();
    }

    private static void AppendRow(StringBuilder sheet, int index, object?[] values, bool bold)
    {
        sheet.Append($"<row r=\"{index}\">");
        for (var i = 0; i < values.Length; i++)
        {
            var reference = Column(i) + index.ToString(CultureInfo.InvariantCulture);
            var style = bold ? " s=\"1\"" : "";
            switch (values[i])
            {
                case null: break;
                case decimal or int or long or double:
                    sheet.Append($"<c r=\"{reference}\"{style}><v>{Convert.ToString(values[i], CultureInfo.InvariantCulture)}</v></c>");
                    break;
                default:
                    var text = Convert.ToString(values[i], CultureInfo.InvariantCulture) ?? "";
                    sheet.Append($"<c r=\"{reference}\" t=\"inlineStr\"{style}><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>");
                    break;
            }
        }
        sheet.Append("</row>");
    }

    private static string Column(int index)
    {
        var name = "";
        for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }

    private static string Escape(string value) =>
        SecurityElement.Escape(new string(value.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray())) ?? "";

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }
}
