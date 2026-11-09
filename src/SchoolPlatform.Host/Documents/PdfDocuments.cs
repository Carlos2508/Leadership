using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Finance;

namespace SchoolPlatform.Host.Documents;

public sealed record ReportLine(string Subject, decimal? Score, string Status);
/// <summary>One period of one student. Withheld carries the reason and no results.</summary>
public sealed record ReportSection(string Period, ReportLine[] Lines, string? Withheld);
public sealed record StudentReport(string StudentName, string StudentCode, string Placement, string Year, ReportSection[] Sections);
public sealed record SheetLine(string StudentName, string StudentCode, decimal? Score, string? Status);
public sealed record CourseSheetDocument(string Subject, string Placement, string Year, string Period, bool Published, SheetLine[] Lines);
public sealed record CardData(string StudentName, string StudentCode, string Placement, string Year, string Token, byte[]? Photo);

/// <summary>Brand images of this installation (paths validated at startup), read once.</summary>
public sealed class BrandAssets(IWebHostEnvironment environment)
{
    private readonly ConcurrentDictionary<string, byte[]> cache = new();

    public byte[] Read(string path) => cache.GetOrAdd(path, x =>
        File.ReadAllBytes(Path.Combine(environment.WebRootPath, x.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
}

/// <summary>
/// Printable PDFs rendered on the server from data already authorized by the caller. Institutional texts, logos and the
/// accent color come only from config/institution.json; the document design (graphite bands, typography, layout) is the
/// platform's own (docs/DESIGN.md). Grades documents show period results only: no annual averages or rounding, which
/// remain pending institutional policy; pass/fail colors appear only if the school configures Documents:PassingScore.
/// </summary>
public sealed partial class PdfDocuments(IOptions<InstitutionOptions> options, BrandAssets assets, TimeProvider time)
{
    private InstitutionOptions School => options.Value;
    private CultureInfo Culture => CultureInfo.GetCultureInfo(School.Locale.Culture);

    // Platform document palette: graphite bands and warm neutrals; the accent comes from the institution theme.
    private const string Graphite = "#1B2025";
    private const string Graphite2 = "#2B3238";
    private const string Ink = "#1E2328";
    private const string Muted = "#6B6F73";
    private const string Line = "#E7E2D6";
    private const string Warm = "#FBF8F1";
    private const string Success = "#1F7A4D";
    private const string Danger = "#B42318";
    private string Accent => School.Theme.Accent.Length > 0 ? School.Theme.Accent : "#E8A33A";
    private string AccentSoft => Mix(Accent, "#FFFFFF", 0.86);
    private string AccentDeep => Mix(Accent, "#000000", 0.32);

    static PdfDocuments()
    {
        // Community license: valid for organizations under the revenue threshold of the QuestPDF license (see ADR-009).
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        // Figtree (SIL OFL, Documents/Fonts/OFL-Figtree.txt), static weights embedded in the assembly.
        var assembly = typeof(PdfDocuments).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(x => x.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            FontManager.RegisterFontFromStream(stream);
        }
    }

    // ===================================================================== receipts

    /// <summary>Receipt from its immutable snapshot. The issuing account is shown only on staff copies.</summary>
    public byte[] Receipt(ReceiptView view, byte[]? logo, string? logoType, bool showIssuer)
    {
        var r = view.Receipt;
        var subtitle = Join(" · ", r.LegalName, r.TaxId.Length > 0 ? $"NIT {r.TaxId}" : "");
        return Document.Create(document => document.Page(page =>
        {
            Base(page, PageSizes.Letter);
            page.Header().Element(x => Band(x, logo, logoType, r.InstitutionName, subtitle.Length > 0 ? subtitle : School.CampusLabel,
                "COMPROBANTE DE PAGO", t =>
                {
                    t.Span("Recibo ").FontColor("#FFFFFF");
                    t.Span($"No. {r.Number:D6}").FontColor(Accent);
                }, $"Emitido {Instant(r.IssuedAt)}"));
            page.Content().Column(col =>
            {
                col.Item().Element(x => InfoStrip(x,
                    ("Recibimos de", r.PayerName, null),
                    ("Fecha de pago", LongDate(r.ReceivedOn), null),
                    ("Forma de pago", Method(r.Method), r.Reference.Length > 0 ? $"Ref. {r.Reference}" : null)));
                col.Item().PaddingHorizontal(34).PaddingTop(22).Column(body =>
                {
                    body.Spacing(14);
                    body.Item().Row(row =>
                    {
                        row.RelativeItem().Element(x => SectionTitle(x, "Detalle del pago"));
                        row.AutoItem().AlignMiddle().Element(x => StatusChip(x, view.Reversed ? "ANULADO" : "PAGO CONFIRMADO", view.Reversed ? Danger : Success));
                    });
                    body.Item().Border(1).BorderColor(Line).CornerRadius(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(34); c.RelativeColumn(5); c.RelativeColumn(4); c.RelativeColumn(2); });
                        table.Header(h =>
                        {
                            h.Cell().Element(DarkHead).Text("N.º");
                            h.Cell().Element(DarkHead).Text("CONCEPTO");
                            h.Cell().Element(DarkHead).Text("ESTUDIANTE");
                            h.Cell().Element(x => DarkHead(x).AlignRight()).Text("MONTO");
                        });
                        var i = 0;
                        foreach (var line in r.Lines)
                        {
                            var zebra = i++ % 2 == 1;
                            var number = i;
                            table.Cell().Element(x => Cell(x, zebra)).Text($"{number:00}").FontColor(Muted);
                            table.Cell().Element(x => Cell(x, zebra)).Column(c =>
                            {
                                var conceptLine = Join(" · ", line.Concept, line.Period ?? "");
                                c.Item().Text(line.Description.Length > 0 ? line.Description : conceptLine).SemiBold();
                                if (line.Description.Length > 0 && !line.Description.Equals(conceptLine, StringComparison.OrdinalIgnoreCase))
                                    c.Item().Text(conceptLine).FontSize(8).FontColor(Muted);
                            });
                            table.Cell().Element(x => Cell(x, zebra)).Column(c =>
                            {
                                c.Item().Text(line.StudentName).SemiBold();
                                c.Item().Text($"Código {line.StudentCode}").FontSize(8).FontColor(Muted);
                            });
                            table.Cell().Element(x => Cell(x, zebra).AlignRight()).Text(Money(line.Amount, r.Currency)).SemiBold();
                        }
                    });
                    // Total block: amount in figures and words, as on a paper receipt.
                    body.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(18).AlignMiddle().Column(c =>
                        {
                            c.Item().Text("CANTIDAD EN LETRAS").FontSize(7).Bold().LetterSpacing(0.12f).FontColor(AccentDeep);
                            c.Item().PaddingTop(3).Text(AmountInWords(r.Amount, r.Currency)).FontSize(10.5f).SemiBold().FontColor(Ink);
                        });
                        row.ConstantItem(210).Background(Graphite).CornerRadius(12).Padding(14).Column(c =>
                        {
                            c.Item().Text("TOTAL RECIBIDO").FontSize(7.5f).Bold().LetterSpacing(0.14f).FontColor(Accent);
                            c.Item().AlignRight().Text(Money(r.Amount, r.Currency)).FontSize(24).ExtraBold().FontColor("#FFFFFF");
                        });
                    });
                    if (view.Reversed)
                        body.Item().Background("#FDECEA").CornerRadius(10).Padding(12).Text(text =>
                        {
                            text.Span("Pago anulado").Bold().FontColor(Danger);
                            if (view.ReversedAt is { } at) text.Span($" el {Instant(at)}").FontColor(Danger);
                            if (!string.IsNullOrWhiteSpace(view.ReversalReason)) text.Span($". Motivo: {view.ReversalReason.TrimEnd('.', ' ')}").FontColor(Danger);
                            text.Span(". Este recibo no acredita un pago vigente.").FontColor(Danger);
                        });
                    // Contact, signature area and notes.
                    body.Item().PaddingTop(26).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("CONTACTO").FontSize(7).Bold().LetterSpacing(0.12f).FontColor(AccentDeep);
                            if (r.Address.Length > 0) c.Item().PaddingTop(3).Text(r.Address).FontSize(8.5f).FontColor(Muted);
                            c.Item().Text(Join(" · ", r.Phone, r.Email)).FontSize(8.5f).FontColor(Muted);
                        });
                        row.ConstantItem(40);
                        row.ConstantItem(200).Column(c =>
                        {
                            c.Item().Height(34);
                            c.Item().BorderTop(1).BorderColor(Ink).PaddingTop(4).AlignCenter()
                                .Text(showIssuer && r.IssuedBy.Length > 0 ? $"Recibido por {r.IssuedBy}" : "Firma y sello").FontSize(8.5f).FontColor(Muted);
                        });
                    });
                    body.Item().PaddingTop(10).Background(Warm).CornerRadius(10).Padding(10).Text(t =>
                    {
                        t.Span("Comprobante interno de pago. No es una factura fiscal. ").FontSize(8).FontColor(Muted);
                        if (r.Footer.Length > 0) t.Span(r.Footer).FontSize(8).FontColor(Muted);
                    });
                });
            });
            page.Background().AlignBottom().AlignRight().PaddingBottom(34).Width(300).Height(170).Svg(Arc(Accent, 0.09));
            page.Footer().Element(x => FooterBand(x, "Recibo de pago"));
            if (view.Reversed)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-28).Text("ANULADO").FontSize(104).Black().FontColor("#F8DCD8");
        })).GeneratePdf();
    }

    // ===================================================================== grades

    /// <summary>Report card: one page per student, subjects as rows and each published period as a column.</summary>
    public byte[] GradeReports(IReadOnlyList<StudentReport> reports)
    {
        var logo = Logo(School.Assets.ReportCardLogo);
        return Document.Create(document =>
        {
            foreach (var report in reports)
                document.Page(page =>
                {
                    Base(page, PageSizes.Letter.Landscape());
                    page.Header().Element(x => Band(x, logo.Bytes, logo.Type, School.DisplayName, School.CampusLabel,
                        $"DOCUMENTO ACADÉMICO · {report.Year.ToUpper(Culture)}", t =>
                        {
                            t.Span("Boletín de ").FontColor("#FFFFFF");
                            t.Span("calificaciones").FontColor(Accent);
                        }, "Resultados de los períodos publicados por el colegio"));
                    page.Content().Column(col =>
                    {
                        var sections = report.Sections;
                        var available = sections.Where(x => x.Withheld is null).ToArray();
                        col.Item().Element(x => InfoStrip(x,
                            (School.Terms.StudentPlural.TrimEnd('s'), report.StudentName, $"Código: {report.StudentCode}"),
                            ("Grado", report.Placement.Length > 0 ? report.Placement : "—", School.DisplayName),
                            ("Período evaluado", sections.Length == 1 ? sections[0].Period : $"{available.Length} de {sections.Length} publicados",
                                Cycle(report.Year))));
                        col.Item().PaddingHorizontal(30).PaddingTop(12).Column(body =>
                        {
                            if (sections.Length == 0)
                            {
                                body.Item().Element(x => NoticeCard(x, "Aún no hay períodos publicados",
                                    "Las calificaciones aparecerán aquí cuando el colegio publique cada período."));
                                return;
                            }
                            if (available.Length == 0)
                            {
                                body.Item().Element(x => NoticeCard(x, "Informe retenido", sections[0].Withheld!));
                                return;
                            }
                            var subjects = available.SelectMany(x => x.Lines).Select(x => x.Subject).Distinct()
                                .OrderBy(x => x, StringComparer.Create(Culture, true)).ToArray();
                            body.Item().Row(row =>
                            {
                                row.RelativeItem().Element(x => SectionTitle(x, "Detalle de calificaciones"));
                                row.AutoItem().AlignBottom().Text($"{subjects.Length} ASIGNATURAS  ·  CALIFICACIÓN SOBRE 100 PUNTOS")
                                    .FontSize(7.5f).Bold().LetterSpacing(0.1f).FontColor(Muted);
                            });
                            body.Item().PaddingTop(8).Element(x => GradeTable(x, sections, subjects));
                            var held = sections.Where(x => x.Withheld is not null).ToArray();
                            var withheld = held.Length == 0 ? null : $"{string.Join(", ", held.Select(x => x.Period))}: {held[0].Withheld}";
                            body.Item().PaddingTop(9).Row(row =>
                            {
                                row.RelativeItem().AlignMiddle().Element(x => Legend(x, withheld));
                                var latest = available[^1];
                                var scored = latest.Lines.Count(x => x.Status == "Scored");
                                row.AutoItem().AlignMiddle().PaddingLeft(16).BorderLeft(1).BorderColor(Line).PaddingLeft(16).Row(r =>
                                {
                                    r.AutoItem().AlignMiddle().Text($"MATERIAS CON NOTA · {latest.Period.ToUpper(Culture)}").FontSize(7.5f).Bold()
                                        .LetterSpacing(0.1f).FontColor(Muted);
                                    r.AutoItem().AlignMiddle().PaddingLeft(10).Text(t =>
                                    {
                                        t.Span(scored.ToString(Culture)).FontSize(26).ExtraBold().FontColor(AccentDeep);
                                        t.Span($"/{latest.Lines.Length}").FontSize(11).SemiBold().FontColor(Muted);
                                    });
                                });
                            });
                        });
                    });
                    page.Footer().Element(x => FooterBand(x, $"Boletín de calificaciones · {Cycle(report.Year)}"));
                });
        }).GeneratePdf();
    }

    private void GradeTable(IContainer container, ReportSection[] sections, string[] subjects)
    {
        var lastAvailable = Array.FindLastIndex(sections, x => x.Withheld is null);
        // Up to ~16 subjects fit one landscape page; longer lists use tighter rows.
        var compact = subjects.Length <= 10 ? 5.5f : subjects.Length <= 14 ? 4f : 3f;
        container.Border(1).BorderColor(Line).CornerRadius(10).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(34);
                c.RelativeColumn(4.2f);
                foreach (var _ in sections) c.RelativeColumn(1.25f);
            });
            table.Header(h =>
            {
                h.Cell().ColumnSpan(2).Background(Graphite2).PaddingVertical(6).PaddingHorizontal(10)
                    .Text("CURSO / ASIGNATURA").FontSize(7.5f).Bold().LetterSpacing(0.12f).FontColor(Accent);
                h.Cell().ColumnSpan((uint)sections.Length).Background(Graphite2).PaddingVertical(6).AlignCenter()
                    .Text("RESULTADO POR PERÍODO").FontSize(7.5f).Bold().LetterSpacing(0.12f).FontColor(Accent);
                h.Cell().Element(DarkHead).Text("N.º");
                h.Cell().Element(DarkHead).Text("Nombre de la asignatura");
                for (var i = 0; i < sections.Length; i++)
                {
                    var highlight = i == lastAvailable;
                    h.Cell().Background(highlight ? Accent : Graphite).PaddingVertical(7).PaddingHorizontal(4).AlignCenter()
                        .Text(sections[i].Period).FontSize(8).Bold().FontColor(highlight ? Graphite : "#FFFFFF");
                }
            });
            var row = 0;
            foreach (var subject in subjects)
            {
                var zebra = row++ % 2 == 1;
                var number = row;
                table.Cell().Element(x => Cell(x, zebra, vertical: compact)).Text($"{number:00}").FontColor(Muted);
                table.Cell().Element(x => Cell(x, zebra, vertical: compact)).Text(subject).SemiBold();
                for (var i = 0; i < sections.Length; i++)
                {
                    var section = sections[i];
                    var highlight = i == lastAvailable;
                    var line = section.Lines.FirstOrDefault(x => x.Subject == subject);
                    table.Cell().Element(x => Cell(x, zebra, highlight ? AccentSoft : null, compact).AlignCenter().AlignMiddle())
                        .Element(x => ScoreText(x, section, line, highlight));
                }
            }
        });
    }

    private void ScoreText(IContainer container, ReportSection section, ReportLine? line, bool highlight)
    {
        if (section.Withheld is not null) { container.Text("Retenido").FontSize(8).Italic().FontColor(Muted); return; }
        if (line is null) { container.Text("—").FontColor(Muted); return; }
        switch (line.Status)
        {
            case "Scored":
                var color = School.Documents.PassingScore is { } pass ? (line.Score >= pass ? Success : Danger) : Ink;
                container.Text(line.Score?.ToString("0.##", Culture) ?? "—").FontSize(highlight ? 13 : 11).ExtraBold().FontColor(color);
                break;
            case "Missing":
                container.Text("Pendiente").FontSize(8).SemiBold().FontColor("#7A4B00");
                break;
            case "Excused":
                container.Text("Exonerado").FontSize(8).SemiBold().FontColor(Muted);
                break;
            default:
                container.Text("—").FontColor(Muted);
                break;
        }
    }

    private void Legend(IContainer container, string? withheld) => container.Column(col =>
    {
        if (withheld is not null) col.Item().PaddingBottom(4).Text(withheld).FontSize(8).SemiBold().FontColor("#7A4B00");
        col.Item().Element(x => LegendRow(x));
    });

    private void LegendRow(IContainer container) => container.Row(row =>
    {
        if (School.Documents.PassingScore is { } pass)
        {
            row.AutoItem().AlignMiddle().Width(7).Height(7).CornerRadius(4).Background(Success);
            row.AutoItem().AlignMiddle().PaddingLeft(5).PaddingRight(14).Text($"Aprobada (desde {pass.ToString("0.##", Culture)})").FontSize(8).SemiBold();
            row.AutoItem().AlignMiddle().Width(7).Height(7).CornerRadius(4).Background(Danger);
            row.AutoItem().AlignMiddle().PaddingLeft(5).PaddingRight(14).Text("Por reforzar").FontSize(8).SemiBold();
        }
        row.RelativeItem().AlignMiddle().Text("Se muestran los resultados de cada período publicado. No incluye promedios anuales ni redondeos.")
            .FontSize(7.5f).FontColor(Muted);
    });

    // ===================================================================== course sheet

    public byte[] CourseSheet(CourseSheetDocument sheet)
    {
        var logo = Logo(School.Assets.ReportCardLogo);
        return Document.Create(document => document.Page(page =>
        {
            Base(page, PageSizes.Letter);
            page.Header().Element(x => Band(x, logo.Bytes, logo.Type, School.DisplayName, School.CampusLabel,
                $"LIBRETA DEL CURSO · {sheet.Year.ToUpper(Culture)}", t => t.Span(sheet.Subject).FontColor("#FFFFFF"), sheet.Placement));
            page.Content().Column(col =>
            {
                var recorded = sheet.Lines.Count(x => x.Status is not null);
                col.Item().Element(x => InfoStrip(x,
                    ("Materia", sheet.Subject, null),
                    ("Período", sheet.Period, sheet.Published ? "Publicado" : "En captura"),
                    ("Registradas", $"{recorded} de {sheet.Lines.Length}", "Documento interno docente")));
                col.Item().PaddingHorizontal(34).PaddingTop(18).Column(body =>
                {
                    body.Item().Element(x => SectionTitle(x, "Resultados del período"));
                    body.Item().PaddingTop(10).Border(1).BorderColor(Line).CornerRadius(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(34); c.RelativeColumn(5); c.RelativeColumn(2); c.RelativeColumn(2.4f); c.RelativeColumn(1.6f); });
                        table.Header(h =>
                        {
                            h.Cell().Element(DarkHead).Text("N.º");
                            h.Cell().Element(DarkHead).Text("ESTUDIANTE");
                            h.Cell().Element(DarkHead).Text("CÓDIGO");
                            h.Cell().Element(DarkHead).Text("SITUACIÓN");
                            h.Cell().Background(Accent).PaddingVertical(7).PaddingHorizontal(8).AlignCenter().Text("NOTA").FontSize(7.5f).Bold().FontColor(Graphite);
                        });
                        var i = 0;
                        foreach (var line in sheet.Lines)
                        {
                            var zebra = i++ % 2 == 1;
                            var number = i;
                            table.Cell().Element(x => Cell(x, zebra)).Text($"{number:00}").FontColor(Muted);
                            table.Cell().Element(x => Cell(x, zebra)).Text(line.StudentName).SemiBold();
                            table.Cell().Element(x => Cell(x, zebra)).Text(line.StudentCode).FontColor(Muted);
                            table.Cell().Element(x => Cell(x, zebra)).Text(line.Status switch
                            {
                                "Scored" => "Con nota", "Missing" => "Pendiente de entregar", "Excused" => "Exonerado", _ => "Sin registrar"
                            }).FontColor(line.Status is null ? Muted : Ink);
                            table.Cell().Element(x => Cell(x, zebra, AccentSoft).AlignCenter())
                                .Text(line.Status == "Scored" ? line.Score?.ToString("0.##", Culture) ?? "—" : "—").FontSize(11).ExtraBold();
                        }
                    });
                    body.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(220).BorderTop(1).BorderColor(Ink).PaddingTop(4).AlignCenter().Text("Firma del docente").FontSize(8.5f).FontColor(Muted);
                    });
                });
            });
            page.Footer().Element(x => FooterBand(x, "Libreta del curso"));
        })).GeneratePdf();
    }

    // ===================================================================== student ID cards (CR80: 85.6 × 53.98 mm)

    /// <summary>layout "card": one card-size page per side (PVC printers). layout "sheet": letter pages with front and back side by side, to cut and fold.</summary>
    public byte[] Cards(IReadOnlyList<CardData> cards, string layout)
    {
        var logo = Logo(School.Assets.CardLogo);
        var cardSize = new PageSize(85.6f, 53.98f, Unit.Millimetre);
        if (layout == "card")
            return Document.Create(document =>
            {
                foreach (var card in cards)
                {
                    document.Page(page => { page.Size(cardSize); page.Margin(0); page.DefaultTextStyle(Font); page.Content().Element(x => CardFront(x, card, logo)); });
                    document.Page(page => { page.Size(cardSize); page.Margin(0); page.DefaultTextStyle(Font); page.Content().Element(x => CardBack(x, card)); });
                }
            }).GeneratePdf();
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(12, Unit.Millimetre);
            page.DefaultTextStyle(Font);
            page.Content().Column(col =>
            {
                col.Spacing(6, Unit.Millimetre);
                foreach (var card in cards)
                    col.Item().ShowEntire().Row(row =>
                    {
                        row.Spacing(6, Unit.Millimetre);
                        row.ConstantItem(85.6f, Unit.Millimetre).Height(53.98f, Unit.Millimetre).Border(0.3f).BorderColor("#CCCCCC")
                            .Element(x => CardFront(x, card, logo));
                        row.ConstantItem(85.6f, Unit.Millimetre).Height(53.98f, Unit.Millimetre).Border(0.3f).BorderColor("#CCCCCC")
                            .Element(x => CardBack(x, card));
                    });
            });
        })).GeneratePdf();
    }

    private void CardFront(IContainer container, CardData card, (byte[]? Bytes, string? Type) logo)
    {
        container.Background("#FFFFFF").Layers(layers =>
        {
            layers.Layer().AlignRight().AlignBottom().Width(34, Unit.Millimetre).Height(34, Unit.Millimetre).Svg(Arc(Accent, 0.12));
            layers.PrimaryLayer().Column(col =>
            {
                col.Item().Height(14, Unit.Millimetre).Background(Graphite).PaddingHorizontal(3, Unit.Millimetre).Row(row =>
                {
                    row.ConstantItem(10, Unit.Millimetre).AlignMiddle().Element(x => LogoBadge(x, logo.Bytes, logo.Type, 10, Unit.Millimetre));
                    row.RelativeItem().PaddingLeft(2.2f, Unit.Millimetre).AlignMiddle().Column(c =>
                    {
                        c.Item().Text(School.DisplayName).FontSize(7.6f).ExtraBold().FontColor("#FFFFFF").ClampLines(1);
                        c.Item().Text(School.CampusLabel).FontSize(5.2f).SemiBold().FontColor(Accent).ClampLines(1);
                    });
                    row.AutoItem().AlignMiddle().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("CARNET").FontSize(5).Bold().LetterSpacing(0.2f).FontColor(Accent);
                        c.Item().AlignRight().Text(YearOnly(card.Year)).FontSize(8).ExtraBold().FontColor("#FFFFFF");
                    });
                });
                col.Item().Height(0.9f, Unit.Millimetre).Background(Accent);
                col.Item().PaddingHorizontal(3.2f, Unit.Millimetre).PaddingTop(2.6f, Unit.Millimetre).Row(row =>
                {
                    row.ConstantItem(20, Unit.Millimetre).Height(25, Unit.Millimetre).Border(0.9f).BorderColor(Accent).CornerRadius(3)
                        .Padding(0.6f).Background(Warm).Element(x =>
                        {
                            if (card.Photo is { } photo) x.CornerRadius(2).Image(photo).FitArea();
                            else x.AlignCenter().AlignMiddle().Text(Initials(card.StudentName)).FontSize(17).ExtraBold().FontColor(AccentDeep);
                        });
                    row.RelativeItem().PaddingLeft(3, Unit.Millimetre).Column(c =>
                    {
                        c.Item().Text(School.Terms.StudentPlural.TrimEnd('s').ToUpper(Culture)).FontSize(5).Bold().LetterSpacing(0.2f).FontColor(AccentDeep);
                        c.Item().PaddingTop(0.6f, Unit.Millimetre).Text(card.StudentName).FontSize(9.4f).ExtraBold().FontColor(Ink).LineHeight(1.05f).ClampLines(2);
                        c.Item().PaddingTop(1, Unit.Millimetre).Text(card.Placement.Length > 0 ? card.Placement : "—").FontSize(6.3f).SemiBold().FontColor(Muted).ClampLines(2);
                        c.Item().PaddingTop(1.6f, Unit.Millimetre).AlignLeft().Background(Graphite).CornerRadius(2.5f)
                            .PaddingHorizontal(2, Unit.Millimetre).PaddingVertical(0.7f, Unit.Millimetre)
                            .Text(t =>
                            {
                                t.Span("CÓDIGO ").FontSize(5).Bold().FontColor(Accent);
                                t.Span(card.StudentCode).FontSize(6.8f).ExtraBold().FontColor("#FFFFFF");
                            });
                    });
                    row.ConstantItem(16.5f, Unit.Millimetre).Column(c =>
                    {
                        c.Item().Height(16.5f, Unit.Millimetre).Background("#FFFFFF").Svg(Barcodes.QrSvg(card.Token));
                        c.Item().AlignCenter().Text("ESCANEAR").FontSize(4.2f).Bold().LetterSpacing(0.2f).FontColor(Muted);
                    });
                });
                col.Item().ExtendVertical().AlignBottom().Height(5.2f, Unit.Millimetre).Background(Graphite).PaddingHorizontal(3.2f, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text(School.Tagline).FontSize(5.4f).SemiBold().FontColor("#FFFFFF").ClampLines(1);
                    row.AutoItem().AlignMiddle().Text($"VÁLIDO {Cycle(card.Year).ToUpper(Culture)}").FontSize(4.8f).Bold().LetterSpacing(0.15f).FontColor(Accent);
                });
            });
        });
    }

    private void CardBack(IContainer container, CardData card)
    {
        var emergency = School.Contact.Phone;
        container.Background("#FFFFFF").Column(col =>
        {
            col.Item().Height(9, Unit.Millimetre).Background(Graphite).PaddingHorizontal(3.5f, Unit.Millimetre).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text("VALIDACIÓN INSTITUCIONAL").FontSize(5.6f).Bold().LetterSpacing(0.16f).FontColor(Accent);
                row.AutoItem().AlignMiddle().Text(School.ShortName).FontSize(6.5f).ExtraBold().FontColor("#FFFFFF");
            });
            col.Item().Height(0.7f, Unit.Millimetre).Background(Accent);
            col.Item().PaddingHorizontal(3.5f, Unit.Millimetre).PaddingTop(2, Unit.Millimetre).Column(c =>
            {
                c.Spacing(0.9f, Unit.Millimetre);
                if (School.Documents.CardNotice.Length > 0)
                    c.Item().Text(School.Documents.CardNotice).FontSize(5.6f).SemiBold().FontColor(Ink).ClampLines(2);
                foreach (var rule in School.Documents.CardRules.Take(3))
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(2.6f, Unit.Millimetre).PaddingTop(0.9f, Unit.Millimetre).Width(1.2f, Unit.Millimetre).Height(1.2f, Unit.Millimetre)
                            .CornerRadius(1).Background(Accent);
                        r.RelativeItem().Text(rule).FontSize(5.3f).FontColor(Muted).ClampLines(2);
                    });
            });
            col.Item().ExtendVertical().AlignBottom().PaddingHorizontal(3.5f, Unit.Millimetre).PaddingBottom(2.4f, Unit.Millimetre).Row(row =>
            {
                row.RelativeItem().AlignBottom().Column(c =>
                {
                    if (emergency.Length > 0)
                    {
                        c.Item().Text("EMERGENCIA").FontSize(4.6f).Bold().LetterSpacing(0.18f).FontColor(AccentDeep);
                        c.Item().Text(emergency).FontSize(7.4f).ExtraBold().FontColor(Ink);
                    }
                    if (School.Contact.Address.Length > 0)
                        c.Item().PaddingTop(0.8f, Unit.Millimetre).Text(School.Contact.Address).FontSize(4.8f).FontColor(Muted).ClampLines(2);
                });
                row.ConstantItem(45, Unit.Millimetre).AlignBottom().Border(0.4f).BorderColor(Line).CornerRadius(2).Padding(1.2f, Unit.Millimetre).Column(c =>
                {
                    c.Item().Height(8.5f, Unit.Millimetre).AlignCenter().Svg(Barcodes.Code128Svg(card.Token)).FitArea();
                    c.Item().AlignCenter().Text(card.Token).FontSize(5.4f).SemiBold().LetterSpacing(0.25f).FontColor(Ink);
                });
            });
        });
    }

    // ===================================================================== shared pieces

    private static TextStyle Font(TextStyle style) => style.FontFamily("Figtree").FontColor(Ink);

    private static void Base(PageDescriptor page, PageSize size)
    {
        page.Size(size);
        page.Margin(0);
        page.PageColor("#FFFFFF");
        page.DefaultTextStyle(x => Font(x).FontSize(9.5f));
    }

    /// <summary>Graphite header band with the logo badge, the institution, a gold eyebrow and a title with an accent word.</summary>
    private void Band(IContainer container, byte[]? logo, string? type, string name, string tagline, string eyebrow, Action<TextDescriptor> title, string subtitle)
    {
        container.Column(col =>
        {
            col.Item().Background(Graphite).Layers(layers =>
            {
                layers.Layer().AlignRight().Width(230).Height(130).Svg(Arc(Accent, 0.22));
                layers.PrimaryLayer().PaddingHorizontal(30).PaddingVertical(16).Row(row =>
                {
                    if (logo is not null)
                    {
                        row.ConstantItem(58).AlignMiddle().Element(x => LogoBadge(x, logo, type, 58, Unit.Point));
                        row.ConstantItem(12);
                    }
                    row.ConstantItem(170).AlignMiddle().Column(c =>
                    {
                        c.Item().Text(name.ToUpper(Culture)).FontSize(13).ExtraBold().LetterSpacing(0.02f).FontColor("#FFFFFF").LineHeight(1.05f);
                        if (tagline.Length > 0) c.Item().PaddingTop(3).Text(tagline.ToUpper(Culture)).FontSize(7).Bold().LetterSpacing(0.14f).FontColor(Accent);
                    });
                    row.ConstantItem(26).AlignMiddle().AlignCenter().Width(1).Height(48).Background("#FFFFFF33");
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Item().Text(eyebrow).FontSize(7.5f).Bold().LetterSpacing(0.16f).FontColor(Accent);
                        c.Item().PaddingTop(2).Text(t => { t.DefaultTextStyle(s => s.FontSize(22).ExtraBold()); title(t); });
                        if (subtitle.Length > 0) c.Item().PaddingTop(2).Text(subtitle).FontSize(8.5f).FontColor("#C9CDD1");
                    });
                });
            });
            col.Item().Height(4).Background(Accent);
        });
    }

    private void InfoStrip(IContainer container, params (string Label, string Value, string? Note)[] items)
    {
        container.Background(Warm).BorderBottom(1).BorderColor(Line).PaddingHorizontal(30).PaddingVertical(11).Row(row =>
        {
            for (var i = 0; i < items.Length; i++)
            {
                var (label, value, note) = items[i];
                var first = i == 0;
                if (!first) row.ConstantItem(1).Background(Line);
                row.RelativeItem(first ? 1.6f : 1).PaddingLeft(first ? 0 : 18).Column(c =>
                {
                    c.Item().Text(label.ToUpper(Culture)).FontSize(7).Bold().LetterSpacing(0.14f).FontColor(AccentDeep);
                    c.Item().PaddingTop(2).Text(value).FontSize(first ? 14 : 12).ExtraBold().FontColor(Ink).ClampLines(2);
                    if (!string.IsNullOrWhiteSpace(note)) c.Item().PaddingTop(1).Text(note).FontSize(8).FontColor(Muted);
                });
            }
        });
    }

    private void SectionTitle(IContainer container, string title) => container.Row(row =>
    {
        row.ConstantItem(4).Height(16).CornerRadius(2).Background(Accent);
        row.RelativeItem().PaddingLeft(8).AlignMiddle().Text(title).FontSize(13).ExtraBold().FontColor(Ink);
    });

    private static void StatusChip(IContainer container, string text, string color) =>
        container.Border(1).BorderColor(color).CornerRadius(20).PaddingHorizontal(10).PaddingVertical(3)
            .Text(text).FontSize(7.5f).Bold().LetterSpacing(0.1f).FontColor(color);

    private void NoticeCard(IContainer container, string title, string text) =>
        container.Background("#FFF5E5").Border(1).BorderColor(AccentSoft).CornerRadius(14).Padding(22).Column(c =>
        {
            c.Item().Text(title).FontSize(16).ExtraBold().FontColor(AccentDeep);
            var body = text.StartsWith(title + ":", StringComparison.OrdinalIgnoreCase) ? text[(title.Length + 1)..].Trim() : text;
            c.Item().PaddingTop(6).Text(char.ToUpper(body[0], Culture) + body[1..]).FontSize(10.5f).FontColor(Ink);
        });

    private void FooterBand(IContainer container, string document)
    {
        container.Background(Graphite).PaddingHorizontal(30).PaddingVertical(10).Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text(t =>
            {
                t.Span(School.ShortName.ToUpper(Culture)).FontSize(8).ExtraBold().FontColor(Accent);
                if (School.Tagline.Length > 0) t.Span($"  ·  {School.Tagline}").FontSize(8).SemiBold().FontColor("#FFFFFF");
            });
            row.AutoItem().AlignMiddle().Text(t =>
            {
                t.Span($"{document}  ·  Generado {Instant(time.GetUtcNow())}  ·  ").FontSize(7.5f).FontColor("#C9CDD1");
                t.CurrentPageNumber().FontSize(7.5f).Bold().FontColor(Accent);
                t.Span("/").FontSize(7.5f).FontColor("#C9CDD1");
                t.TotalPages().FontSize(7.5f).FontColor("#C9CDD1");
            });
        });
    }

    private static IContainer DarkHead(IContainer container) =>
        container.Background(Graphite).PaddingVertical(7).PaddingHorizontal(10)
            .DefaultTextStyle(x => x.FontSize(7.5f).Bold().LetterSpacing(0.06f).FontColor("#FFFFFF"));

    private static IContainer Cell(IContainer container, bool zebra, string? background = null, float vertical = 6) =>
        container.Background(background ?? (zebra ? Warm : "#FFFFFF")).BorderBottom(0.6f).BorderColor(Line).PaddingVertical(vertical).PaddingHorizontal(10)
            .AlignMiddle();

    private static void LogoBadge(IContainer container, byte[]? bytes, string? type, float size, Unit unit)
    {
        if (bytes is null) return;
        var badge = container.Width(size, unit).Height(size, unit).Background("#FFFFFF").CornerRadius(size / 2, unit).Padding(size * 0.07f, unit);
        if (type == "image/svg+xml") badge.Svg(System.Text.Encoding.UTF8.GetString(bytes)).FitArea();
        else badge.Image(bytes).FitArea();
    }

    private (byte[]? Bytes, string? Type) Logo(string path) =>
        string.IsNullOrEmpty(path) ? (null, null) : (assets.Read(path), path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "image/png");

    /// <summary>Decorative concentric arcs in the accent color (top-right of bands and cards).</summary>
    private static string Arc(string color, double opacity) => string.Create(CultureInfo.InvariantCulture,
        $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 230 130'><g fill='none' stroke='{color}' stroke-opacity='{opacity}'>" +
        $"<circle cx='205' cy='-10' r='105' stroke-width='22'/><circle cx='205' cy='-10' r='150' stroke-width='3'/></g></svg>");

    private string Money(decimal amount, string currency)
    {
        try
        {
            if (new RegionInfo(Culture.Name).ISOCurrencySymbol == currency) return amount.ToString("C", Culture);
        }
        catch (ArgumentException) { }
        return $"{currency} {amount.ToString("N2", Culture)}";
    }

    private string Instant(DateTimeOffset value)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(School.Locale.TimeZone);
        return TimeZoneInfo.ConvertTime(value, zone).ToString("d 'de' MMMM yyyy, HH:mm", Culture);
    }

    private string LongDate(DateOnly date) => date.ToString("d 'de' MMMM 'de' yyyy", Culture);

    private static string Cycle(string year) => year.StartsWith("Ciclo", StringComparison.OrdinalIgnoreCase) ? year : $"Ciclo {year}";

    private static string YearOnly(string year) => year.Split(' ', '·').FirstOrDefault(x => x.Length > 0) ?? year;

    private static string Method(string method) => method switch
    {
        "Cash" => "Efectivo", "Card" => "Tarjeta", "Transfer" => "Transferencia", "Deposit" => "Depósito", "Check" => "Cheque", "Other" => "Otro", _ => method
    };

    private static string Join(string separator, params string[] parts) => string.Join(separator, parts.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));

    private static string Mix(string hex, string with, double amount)
    {
        static (int R, int G, int B) Parse(string h) =>
            (Convert.ToInt32(h.Substring(1, 2), 16), Convert.ToInt32(h.Substring(3, 2), 16), Convert.ToInt32(h.Substring(5, 2), 16));
        var (r1, g1, b1) = Parse(hex);
        var (r2, g2, b2) = Parse(with);
        int Blend(int a, int b) => (int)Math.Round(a + (b - a) * amount);
        return $"#{Blend(r1, r2):X2}{Blend(g1, g2):X2}{Blend(b1, b2):X2}";
    }

    // ---------- amount in words (Spanish), for receipts

    private static readonly Dictionary<string, (string Singular, string Plural)> CurrencyNames = new()
    {
        ["GTQ"] = ("quetzal", "quetzales"), ["USD"] = ("dólar", "dólares"), ["MXN"] = ("peso", "pesos"), ["HNL"] = ("lempira", "lempiras"),
        ["CRC"] = ("colón", "colones"), ["NIO"] = ("córdoba", "córdobas"), ["COP"] = ("peso", "pesos"), ["PEN"] = ("sol", "soles"), ["EUR"] = ("euro", "euros")
    };

    /// <summary>"Ciento cincuenta quetzales con 00/100".</summary>
    public static string AmountInWords(decimal amount, string currency)
    {
        var whole = (long)Math.Floor(amount);
        var cents = (int)Math.Round((amount - whole) * 100);
        var name = CurrencyNames.TryGetValue(currency, out var n) ? (whole == 1 ? n.Singular : n.Plural) : currency;
        var words = Words(whole);
        // Apocope before the currency noun: "un quetzal", "veintiún quetzales", "treinta y un quetzales".
        if (words.EndsWith("veintiuno", StringComparison.Ordinal)) words = words[..^"veintiuno".Length] + "veintiún";
        else if (words.EndsWith("uno", StringComparison.Ordinal)) words = words[..^1];
        var text = $"{words} {name} con {cents:00}/100";
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Words(long n)
    {
        if (n == 0) return "cero";
        var parts = new List<string>();
        if (n >= 1_000_000)
        {
            var millions = n / 1_000_000;
            parts.Add(millions == 1 ? "un millón" : Apocope(Words(millions)) + " millones");
            n %= 1_000_000;
        }
        if (n >= 1000)
        {
            var thousands = (int)(n / 1000);
            parts.Add(thousands == 1 ? "mil" : Apocope(Hundreds(thousands)) + " mil");
            n %= 1000;
        }
        if (n > 0) parts.Add(Hundreds((int)n));
        return string.Join(' ', parts);
    }

    private static string Apocope(string words) =>
        words.EndsWith("veintiuno", StringComparison.Ordinal) ? words[..^"veintiuno".Length] + "veintiún"
        : words.EndsWith("uno", StringComparison.Ordinal) ? words[..^1] : words;

    private static string Hundreds(int n)
    {
        string[] units = ["", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez", "once", "doce", "trece", "catorce",
            "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve", "veinte", "veintiuno", "veintidós", "veintitrés", "veinticuatro",
            "veinticinco", "veintiséis", "veintisiete", "veintiocho", "veintinueve"];
        string[] tens = ["", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"];
        string[] hundreds = ["", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos"];
        if (n == 100) return "cien";
        var result = new List<string>();
        if (n >= 100) { result.Add(hundreds[n / 100]); n %= 100; }
        if (n >= 30) result.Add(n % 10 == 0 ? tens[n / 10] : $"{tens[n / 10]} y {units[n % 10]}");
        else if (n > 0) result.Add(units[n]);
        return string.Join(' ', result);
    }
}
