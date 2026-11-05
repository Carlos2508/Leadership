using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Options;
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
/// Printable PDFs rendered on the server from data already authorized by the caller. Institutional texts, logos and
/// colors come only from config/institution.json. Grades documents show period results only: no annual averages or
/// rounding, which remain pending institutional policy.
/// </summary>
public sealed class PdfDocuments(IOptions<InstitutionOptions> options, BrandAssets assets, TimeProvider time)
{
    private InstitutionOptions School => options.Value;
    private CultureInfo Culture => CultureInfo.GetCultureInfo(School.Locale.Culture);
    private string Ink => "#1F2A24";
    private string Muted => "#5E6B63";
    private string Line => "#D9E2DA";

    static PdfDocuments()
    {
        // Community license: valid for organizations under the revenue threshold of the QuestPDF license (see ADR-009).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ---------- receipts

    /// <summary>Receipt from its immutable snapshot. The issuing account is shown only on staff copies.</summary>
    public byte[] Receipt(ReceiptView view, byte[]? logo, string? logoType, bool showIssuer)
    {
        var r = view.Receipt;
        return Document.Create(document => document.Page(page =>
        {
            Base(page, PageSizes.Letter);
            page.Header().Element(x => Header(x, logo, logoType, r.InstitutionName,
                [r.LegalName, r.TaxId.Length > 0 ? $"NIT {r.TaxId}" : "", r.Address, Join(" · ", r.Phone, r.Email)],
                "Recibo de pago", $"No. {r.Number}"));
            page.Content().PaddingTop(18).Column(col =>
            {
                col.Spacing(14);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(x => Field(x, "Recibimos de", r.PayerName));
                    row.ConstantItem(150).Element(x => Field(x, "Fecha de pago", r.ReceivedOn.ToString("d", Culture)));
                    row.ConstantItem(150).Element(x => Field(x, "Forma de pago", Method(r.Method) + (r.Reference.Length > 0 ? $" · {r.Reference}" : "")));
                });
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(2); });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("Detalle");
                        h.Cell().Element(HeadCell).AlignRight().Text("Monto");
                    });
                    foreach (var line in r.Lines)
                    {
                        table.Cell().Element(BodyCell).Column(c =>
                        {
                            c.Item().Text(line.Description.Length > 0 ? line.Description : Join(" · ", line.Concept, line.Period ?? "")).SemiBold();
                            c.Item().Text($"{line.StudentName} · {line.StudentCode}").FontSize(8).FontColor(Muted);
                        });
                        table.Cell().Element(BodyCell).AlignRight().Text(Money(line.Amount, r.Currency));
                    }
                    table.Cell().PaddingTop(8).AlignRight().Text("Total recibido").SemiBold();
                    table.Cell().PaddingTop(8).AlignRight().Text(Money(r.Amount, r.Currency)).Bold().FontSize(13).FontColor(School.Theme.Primary);
                });
                col.Item().Text(text =>
                {
                    text.Span("Emitido ").FontColor(Muted);
                    text.Span(Instant(r.IssuedAt)).FontColor(Muted);
                    if (showIssuer && r.IssuedBy.Length > 0) text.Span($" por {r.IssuedBy}").FontColor(Muted);
                });
                col.Item().Text("Comprobante interno de pago. No es una factura fiscal.").FontSize(8).FontColor(Muted);
                if (view.Reversed)
                    col.Item().Background("#FDECEA").Padding(10).Text(text =>
                    {
                        text.Span("Pago anulado").Bold().FontColor("#B42318");
                        if (view.ReversedAt is { } at) text.Span($" el {Instant(at)}").FontColor("#B42318");
                        if (!string.IsNullOrWhiteSpace(view.ReversalReason)) text.Span($". Motivo: {view.ReversalReason}").FontColor("#B42318");
                    });
            });
            page.Footer().Element(x => Footer(x, r.Footer));
            if (view.Reversed)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-30).Text("ANULADO").FontSize(96).Bold().FontColor("#F5C2BC");
        })).GeneratePdf();
    }

    // ---------- grades

    public byte[] GradeReports(IReadOnlyList<StudentReport> reports)
    {
        var logo = Logo(School.Assets.ReportCardLogo);
        return Document.Create(document =>
        {
            foreach (var report in reports)
                document.Page(page =>
                {
                    Base(page, PageSizes.Letter);
                    page.Header().Element(x => Header(x, logo.Bytes, logo.Type, School.DisplayName, [School.CampusLabel, School.Contact.Address],
                        "Informe de calificaciones", Cycle(report.Year)));
                    page.Content().PaddingTop(18).Column(col =>
                    {
                        col.Spacing(14);
                        col.Item().Row(row =>
                        {
                            row.RelativeItem(2).Element(x => Field(x, School.Terms.StudentPlural.TrimEnd('s'), report.StudentName));
                            row.RelativeItem().Element(x => Field(x, "Código", report.StudentCode));
                            row.RelativeItem(2).Element(x => Field(x, "Grado", report.Placement));
                        });
                        if (report.Sections.Length == 0)
                            col.Item().Element(Notice).Text("Aún no hay períodos publicados.");
                        foreach (var section in report.Sections)
                        {
                            col.Item().PaddingTop(4).Text(section.Period).FontSize(12).Bold().FontColor(School.Theme.Deep);
                            if (section.Withheld is { } withheld)
                            {
                                col.Item().Element(Notice).Text(withheld);
                                continue;
                            }
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });
                                table.Header(h =>
                                {
                                    h.Cell().Element(HeadCell).Text("Materia");
                                    h.Cell().Element(HeadCell).AlignRight().Text("Resultado del período");
                                });
                                foreach (var line in section.Lines)
                                {
                                    table.Cell().Element(BodyCell).Text(line.Subject);
                                    table.Cell().Element(BodyCell).AlignRight().Text(Result(line.Score, line.Status))
                                        .SemiBold().FontColor(line.Status == "Scored" ? Ink : Muted);
                                }
                            });
                        }
                        col.Item().PaddingTop(6).Text("Resultados de períodos publicados por el colegio. No incluye promedios anuales ni redondeos.")
                            .FontSize(8).FontColor(Muted);
                    });
                    page.Footer().Element(x => Footer(x, School.Documents.ReportCardFooter));
                });
        }).GeneratePdf();
    }

    public byte[] CourseSheet(CourseSheetDocument sheet)
    {
        var logo = Logo(School.Assets.ReportCardLogo);
        return Document.Create(document => document.Page(page =>
        {
            Base(page, PageSizes.Letter);
            page.Header().Element(x => Header(x, logo.Bytes, logo.Type, School.DisplayName, [School.CampusLabel],
                "Libreta del curso", Cycle(sheet.Year)));
            page.Content().PaddingTop(18).Column(col =>
            {
                col.Spacing(12);
                col.Item().Row(row =>
                {
                    row.RelativeItem(2).Element(x => Field(x, "Materia", sheet.Subject));
                    row.RelativeItem(2).Element(x => Field(x, "Grado", sheet.Placement));
                    row.RelativeItem().Element(x => Field(x, "Período", sheet.Period + (sheet.Published ? " · publicado" : " · en captura")));
                });
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.ConstantColumn(28); c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(2); });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("#");
                        h.Cell().Element(HeadCell).Text("Estudiante");
                        h.Cell().Element(HeadCell).Text("Código");
                        h.Cell().Element(HeadCell).AlignRight().Text("Resultado");
                    });
                    var i = 0;
                    foreach (var line in sheet.Lines)
                    {
                        table.Cell().Element(BodyCell).Text((++i).ToString(Culture)).FontColor(Muted);
                        table.Cell().Element(BodyCell).Text(line.StudentName);
                        table.Cell().Element(BodyCell).Text(line.StudentCode).FontColor(Muted);
                        table.Cell().Element(BodyCell).AlignRight().Text(line.Status is null ? "Sin registrar" : Result(line.Score, line.Status));
                    }
                });
                col.Item().Text($"{sheet.Lines.Count(x => x.Status is not null)} de {sheet.Lines.Length} registradas.").FontColor(Muted);
            });
            page.Footer().Element(x => Footer(x, "Documento interno de trabajo docente."));
        })).GeneratePdf();
    }

    // ---------- student ID cards (CR80: 85.6 × 53.98 mm)

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
                    document.Page(page => { page.Size(cardSize); page.Margin(0); page.Content().Element(x => CardFront(x, card, logo)); });
                    document.Page(page => { page.Size(cardSize); page.Margin(0); page.Content().Element(x => CardBack(x, card)); });
                }
            }).GeneratePdf();
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(12, Unit.Millimetre);
            page.Content().Column(col =>
            {
                col.Spacing(6, Unit.Millimetre);
                foreach (var card in cards)
                    col.Item().ShowEntire().Row(row =>
                    {
                        row.Spacing(6, Unit.Millimetre);
                        row.ConstantItem(85.6f, Unit.Millimetre).Height(53.98f, Unit.Millimetre).Border(0.4f).BorderColor(Line)
                            .Element(x => CardFront(x, card, logo));
                        row.ConstantItem(85.6f, Unit.Millimetre).Height(53.98f, Unit.Millimetre).Border(0.4f).BorderColor(Line)
                            .Element(x => CardBack(x, card));
                    });
            });
        })).GeneratePdf();
    }

    private void CardFront(IContainer container, CardData card, (byte[]? Bytes, string? Type) logo)
    {
        container.Background("#FFFFFF").Column(col =>
        {
            col.Item().Height(13, Unit.Millimetre).Background(School.Theme.Deep).PaddingHorizontal(3, Unit.Millimetre).Row(row =>
            {
                row.ConstantItem(10, Unit.Millimetre).AlignMiddle().Height(10, Unit.Millimetre).Element(x => Image(x, logo.Bytes, logo.Type));
                row.RelativeItem().PaddingLeft(2, Unit.Millimetre).AlignMiddle().Column(c =>
                {
                    c.Item().Text(School.DisplayName).FontSize(8).Bold().FontColor("#FFFFFF");
                    c.Item().Text("Carnet escolar").FontSize(6).FontColor(School.Theme.Accent);
                });
            });
            col.Item().PaddingHorizontal(3, Unit.Millimetre).PaddingTop(2.5f, Unit.Millimetre).Row(row =>
            {
                row.ConstantItem(21, Unit.Millimetre).Height(26, Unit.Millimetre).Border(0.6f).BorderColor(Line).Background("#F2F4F1")
                    .Element(x =>
                    {
                        if (card.Photo is { } photo) x.Image(photo).FitArea();
                        else x.AlignCenter().AlignMiddle().Text(Initials(card.StudentName)).FontSize(16).Bold().FontColor(Muted);
                    });
                row.RelativeItem().PaddingLeft(3, Unit.Millimetre).Column(c =>
                {
                    c.Spacing(1.2f, Unit.Millimetre);
                    c.Item().Text(card.StudentName).FontSize(9).Bold().FontColor(Ink).ClampLines(2);
                    c.Item().Text(card.Placement).FontSize(7).FontColor(Ink).ClampLines(2);
                    c.Item().Text($"Código {card.StudentCode}").FontSize(7).FontColor(Muted);
                    c.Item().Text(Cycle(card.Year)).FontSize(7).FontColor(Muted);
                    c.Item().PaddingTop(1, Unit.Millimetre).AlignLeft().Background(School.Theme.Primary).PaddingHorizontal(2, Unit.Millimetre)
                        .PaddingVertical(0.6f, Unit.Millimetre).Text(School.Terms.StudentPlural.TrimEnd('s')).FontSize(6.5f).SemiBold().FontColor("#FFFFFF");
                });
                row.ConstantItem(17, Unit.Millimetre).Height(17, Unit.Millimetre).Svg(Barcodes.QrSvg(card.Token));
            });
        });
    }

    private void CardBack(IContainer container, CardData card)
    {
        container.Background("#FFFFFF").Padding(3.5f, Unit.Millimetre).Column(col =>
        {
            col.Spacing(1.2f, Unit.Millimetre);
            col.Item().Text(School.DisplayName).FontSize(7.5f).Bold().FontColor(School.Theme.Deep);
            foreach (var rule in School.Documents.CardRules.Take(4))
                col.Item().Text($"• {rule}").FontSize(5.8f).FontColor(Ink).ClampLines(2);
            if (School.Documents.CardNotice.Length > 0) col.Item().Text(School.Documents.CardNotice).FontSize(5.8f).Italic().FontColor(Muted).ClampLines(2);
            var contact = Join(" · ", School.Contact.Phone, School.Contact.Address);
            if (contact.Length > 0) col.Item().Text(contact).FontSize(5.8f).FontColor(Muted).ClampLines(2);
            col.Item().ExtendVertical().AlignBottom().Column(c =>
            {
                // Centered, at its natural ratio: ~0.3 mm modules read well with handheld 1D scanners.
                c.Item().AlignCenter().Height(12, Unit.Millimetre).Svg(Barcodes.Code128Svg(card.Token));
                c.Item().AlignCenter().Text(card.Token).FontSize(6).LetterSpacing(0.15f).FontColor(Ink);
            });
        });
    }

    // ---------- shared pieces

    private void Base(PageDescriptor page, PageSize size)
    {
        page.Size(size);
        page.Margin(16, Unit.Millimetre);
        page.PageColor("#FFFFFF");
        page.DefaultTextStyle(x => x.FontSize(10).FontColor(Ink));
    }

    private void Header(IContainer container, byte[]? logo, string? type, string name, string[] lines, string title, string subtitle)
    {
        container.BorderBottom(2).BorderColor(School.Theme.Primary).PaddingBottom(10).Row(row =>
        {
            if (logo is not null) row.ConstantItem(54).Height(54).Element(x => Image(x, logo, type));
            row.RelativeItem().PaddingLeft(logo is null ? 0 : 12).Column(col =>
            {
                col.Item().Text(name).FontSize(14).Bold().FontColor(School.Theme.Deep);
                foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x)))
                    col.Item().Text(line).FontSize(8).FontColor(Muted);
            });
            row.ConstantItem(170).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(title).FontSize(12).Bold();
                col.Item().AlignRight().Text(subtitle).FontSize(10).FontColor(School.Theme.Primary).SemiBold();
            });
        });
    }

    private void Footer(IContainer container, string text)
    {
        container.BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(text) ? School.DisplayName : text).FontSize(7.5f).FontColor(Muted);
            row.ConstantItem(170).AlignRight().Text(x =>
            {
                x.Span($"Generado {Instant(time.GetUtcNow())} · ").FontSize(7.5f).FontColor(Muted);
                x.CurrentPageNumber().FontSize(7.5f).FontColor(Muted);
                x.Span("/").FontSize(7.5f).FontColor(Muted);
                x.TotalPages().FontSize(7.5f).FontColor(Muted);
            });
        });
    }

    private void Field(IContainer container, string label, string value) =>
        container.Column(col =>
        {
            col.Item().Text(label.ToUpper(Culture)).FontSize(7).SemiBold().LetterSpacing(0.08f).FontColor(Muted);
            col.Item().Text(value).FontSize(10.5f).SemiBold();
        });

    private IContainer HeadCell(IContainer container) =>
        container.Background("#F1F7F2").BorderBottom(0.8f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6)
            .DefaultTextStyle(x => x.FontSize(8).SemiBold().FontColor(Muted));

    private IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6);

    private IContainer Notice(IContainer container) =>
        container.Background("#FFF7E6").Padding(10).DefaultTextStyle(x => x.FontColor("#7A4B00"));

    private static void Image(IContainer container, byte[]? bytes, string? type)
    {
        if (bytes is null) return;
        if (type == "image/svg+xml") container.Svg(System.Text.Encoding.UTF8.GetString(bytes)).FitArea();
        else container.Image(bytes).FitArea();
    }

    private (byte[]? Bytes, string? Type) Logo(string path) =>
        string.IsNullOrEmpty(path) ? (null, null) : (assets.Read(path), path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "image/png");

    private string Result(decimal? score, string status) => status switch
    {
        "Scored" => score?.ToString("0.##", Culture) ?? "",
        "Missing" => "Pendiente de entregar",
        "Excused" => "Exonerado",
        _ => "Sin registrar"
    };

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
        return TimeZoneInfo.ConvertTime(value, zone).ToString("g", Culture);
    }

    private static string Cycle(string year) => year.StartsWith("Ciclo", StringComparison.OrdinalIgnoreCase) ? year : $"Ciclo {year}";

    private static string Method(string method) => method switch
    {
        "Cash" => "Efectivo", "Card" => "Tarjeta", "Transfer" => "Transferencia", "Deposit" => "Depósito", "Check" => "Cheque", "Other" => "Otro", _ => method
    };

    private static string Join(string separator, params string[] parts) => string.Join(separator, parts.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
}
