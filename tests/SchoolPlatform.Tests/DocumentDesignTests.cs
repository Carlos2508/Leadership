using Microsoft.Extensions.DependencyInjection;
using SchoolPlatform.Host.Documents;
using SchoolPlatform.Host.Finance;

namespace SchoolPlatform.Tests;

/// <summary>
/// Printable documents rendered from synthetic data (no database): content checks, and an optional visual review copy
/// when SCHOOLPLATFORM_PDF_DUMP points to a folder.
/// </summary>
public sealed class DocumentDesignTests
{
    private static readonly string[] Subjects =
    [
        "Matemática", "Comunicación y Lenguaje", "Idioma Extranjero", "Ciencias Naturales", "Ciencias Sociales", "Formación Ciudadana",
        "Educación Física", "Expresión Artística", "Tecnología", "Productividad y Desarrollo", "Música", "Teatro"
    ];

    private static async Task<T> WithDocuments<T>(Func<PdfDocuments, T> render, Dictionary<string, string?>? config = null)
    {
        await using var app = new TestApplication(configuration: config ?? []);
        return render(app.Services.GetRequiredService<PdfDocuments>());
    }

    private static string Text(byte[] pdf, string name)
    {
        if (Environment.GetEnvironmentVariable("SCHOOLPLATFORM_PDF_DUMP") is { Length: > 0 } dump)
            File.WriteAllBytes(Path.Combine(dump, name), pdf);
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdf);
        return string.Join('\n', document.GetPages().Select(x => x.Text));
    }

    [Theory]
    [InlineData(1, "GTQ", "Un quetzal con 00/100")]
    [InlineData(21, "GTQ", "Veintiún quetzales con 00/100")]
    [InlineData(150, "GTQ", "Ciento cincuenta quetzales con 00/100")]
    [InlineData(1250.5, "GTQ", "Mil doscientos cincuenta quetzales con 50/100")]
    [InlineData(31000, "USD", "Treinta y un mil dólares con 00/100")]
    [InlineData(2_000_100, "GTQ", "Dos millones cien quetzales con 00/100")]
    public void Amounts_are_written_in_words(decimal amount, string currency, string expected) =>
        Assert.Equal(expected, PdfDocuments.AmountInWords(amount, currency));

    [Fact]
    public async Task Receipt_shows_amount_in_words_status_and_hides_issuer_on_family_copy()
    {
        var snapshot = new ReceiptSnapshot(3, "Colegio de prueba", "", "", "Dirección de prueba", "5555-0000", "info@example.test", "", "",
            "Persona Ficticia Pagadora", new DateOnly(2026, 2, 3), new DateTimeOffset(2026, 2, 3, 16, 30, 0, TimeSpan.Zero), "Transfer", "TRX-1029",
            "GTQ", 1250.50m,
            [
                new("Ana Ficticia", "QA-001", "Colegiatura", "Cuota 1", "Colegiatura · Cuota 1", 650m),
                new("Bruno Ficticio", "QA-002", "Colegiatura", "Cuota 1", "Colegiatura · Cuota 1", 600.50m)
            ], "operador-prueba");
        var family = Text(await WithDocuments(x => x.Receipt(new ReceiptView(Guid.NewGuid(), snapshot, false, null, null), null, null, false)),
            "recibo-familia.pdf");
        Assert.Contains("No. 000003", family);
        Assert.Contains("Mil doscientos cincuenta quetzales con 50/100", family);
        Assert.Contains("PAGO CONFIRMADO", family);
        Assert.DoesNotContain("operador-prueba", family);

        var staff = Text(await WithDocuments(x => x.Receipt(new ReceiptView(Guid.NewGuid(), snapshot, true, snapshot.IssuedAt.AddDays(1), "Reverso ficticio de prueba."),
            null, null, true)), "recibo-anulado-staff.pdf");
        Assert.Contains("ANULADO", staff);
        Assert.Contains("operador-prueba", staff);
    }

    [Fact]
    public async Task Report_card_lists_every_subject_by_period_without_pass_judgement_unless_configured()
    {
        var periods = new[] { "Bimestre 1", "Bimestre 2", "Bimestre 3" };
        var random = new Random(7);
        var sections = periods.Select((p, i) => new ReportSection(p,
            Subjects.Select((s, j) => j == 4 && i == 1 ? new ReportLine(s, null, "Missing")
                : j == 7 ? new ReportLine(s, null, "Excused")
                : new ReportLine(s, random.Next(48, 100), "Scored")).ToArray(), null)).ToList();
        sections.Add(new ReportSection("Bimestre 4", [], "Resultados no disponibles: hay pagos pendientes del corte de solvencia de este período."));
        var report = new StudentReport("Ana Sofía Ficticia López", "QA-001", "Primero básico · Sección A", "2026", sections.ToArray());

        var text = Text(await WithDocuments(x => x.GradeReports([report])), "boletin.pdf");
        Assert.Contains("Boletín de", text);
        Assert.Contains("Comunicación y Lenguaje", text);
        Assert.Contains("Retenido", text);
        Assert.DoesNotContain("Aprobada", text);

        var withPolicy = Text(await WithDocuments(x => x.GradeReports([report]), new() { ["Institution:Documents:PassingScore"] = "60" }),
            "boletin-con-nota-minima.pdf");
        Assert.Contains("Aprobada (desde 60)", withPolicy);

        var single = new StudentReport("Bruno Ficticio", "QA-002", "Primero básico · Sección A", "2026",
            [new ReportSection("Bimestre 1", [], "Informe retenido: pagos pendientes del corte «Bimestre 1».")]);
        Assert.Contains("Informe retenido", Text(await WithDocuments(x => x.GradeReports([single])), "boletin-retenido.pdf"));
    }

    [Fact]
    public async Task Course_sheet_and_cards_render()
    {
        var sheet = new CourseSheetDocument("Matemática", "Primero básico · Sección A", "2026", "Bimestre 1", false,
            Enumerable.Range(1, 18).Select(i => new SheetLine($"Estudiante Ficticio {i:00}", $"QA-{i:000}", i % 6 == 0 ? null : 55 + i * 2,
                i % 6 == 0 ? null : i % 9 == 0 ? "Missing" : "Scored")).ToArray());
        Assert.Contains("Estudiante Ficticio 18", Text(await WithDocuments(x => x.CourseSheet(sheet)), "libreta.pdf"));

        var cards = new[]
        {
            new CardData(CardKind.Student, "Ana Sofía", "Ficticia López", "QA-001", "Primero básico · Sección A", "Ciclo 2027", "Q0RFWRQMHPRP", null),
            new CardData(CardKind.Student, "María de los Ángeles", "Ficticia Hernández de la Cruz", "QA-002", "Segundo básico", "2027", "C48ZGYKPB9E1", null),
            new CardData(CardKind.Staff, "Carlos Andrés", "Docente Ficticio", "", "Docente de Matemática", "2027", "H7KMQ2TRVX4P", null)
        };
        var card = Text(await WithDocuments(x => x.Cards(cards[..1], "card")), "carnet.pdf");
        Assert.Contains("Q0RFWRQMHPRP", card);
        Assert.Contains("IDENTIFICACIÓN ESTUDIANTIL", card);
        Assert.Contains("VÁLIDO 2027", card);
        var staff = Text(await WithDocuments(x => x.Cards(cards[2..], "card")), "carnet-personal.pdf");
        Assert.Contains("IDENTIFICACIÓN DE PERSONAL", staff);
        Assert.Contains("Docente de Matemática", staff);
        Assert.Contains("H7KMQ2TRVX4P", staff);
        Assert.DoesNotContain("acredita al estudiante", staff);
        Assert.Contains("QA-001", Text(await WithDocuments(x => x.Cards(cards, "sheet")), "carnets-hoja.pdf"));
    }
}
