using System.Text;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Grades;

namespace SchoolPlatform.Tests;

/// <summary>Spreadsheet import/export helpers and evaluation scales (no database).</summary>
public sealed class SpreadsheetTests
{
    [Fact]
    public void Written_xlsx_is_read_back_with_text_numbers_and_no_formulas()
    {
        var bytes = SpreadsheetWriter.Write("Prueba", ["Colegio sintético"], ["Nombres", "Apellidos", "Nota"],
            [["Ana", "Pérez & Hijos", 93.5m], ["=CMD()", "<b>", null]]);
        using var stream = new MemoryStream(bytes);
        var rows = SpreadsheetReader.Read(stream, "prueba.xlsx");
        Assert.Equal("Colegio sintético", rows[0][0]);
        Assert.Equal(new[] { "Nombres", "Apellidos", "Nota" }, rows[1]);
        Assert.Equal(new[] { "Ana", "Pérez & Hijos", "93.5" }, rows[2]);
        // Text that looks like a formula stays text.
        Assert.Equal("=CMD()", rows[3][0]);
        Assert.Equal("<b>", rows[3][1]);
    }

    [Theory]
    [InlineData(";")]
    [InlineData(",")]
    public void Csv_from_excel_is_read_with_either_separator_and_quotes(string separator)
    {
        var csv = $"Nombres{separator}Apellidos{separator}Grado\r\n\"Ana{separator} Lucía\"{separator}Pérez{separator}1P\r\n\r\nLuis{separator}\"Gómez \"\"Lucho\"\"\"{separator}\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray());
        var rows = SpreadsheetReader.Read(stream, "lista.csv");
        Assert.Equal(3, rows.Count);
        Assert.Equal($"Ana{separator} Lucía", rows[1][0]);
        Assert.Equal("Gómez \"Lucho\"", rows[2][1]);
    }

    [Fact]
    public void Old_xls_files_are_rejected_with_a_clear_message()
    {
        using var stream = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0]);
        Assert.Throws<InvalidDataException>(() => SpreadsheetReader.Read(stream, "viejo.xls"));
    }

    [Theory]
    [InlineData("15/03/2018", 2018, 3, 15)]
    [InlineData("2018-03-15", 2018, 3, 15)]
    [InlineData("43174", 2018, 3, 15)]
    public void Dates_accept_common_formats_and_excel_serials(string value, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), SpreadsheetReader.ParseDate(value));

    [Fact]
    public void Qualitative_scale_matches_stage_without_case_and_labels_values()
    {
        var policy = new AcademicPolicyOptions
        {
            Scales =
            [
                new EvaluationScale
                {
                    Name = "Preprimaria", Stages = ["Preprimaria"],
                    Levels = [new() { Code = "L", Label = "Logrado", Value = 100 }, new() { Code = "EP", Label = "En proceso", Value = 70 }]
                }
            ]
        };
        Assert.Equal("En proceso", policy.ScaleFor("preprimaria")?.LabelFor(70));
        Assert.Null(policy.ScaleFor("Primaria"));
        Assert.Null(policy.ScaleFor("Preprimaria")?.LabelFor(55));
        Assert.False(policy.GradesRequireClearance);
    }
}
