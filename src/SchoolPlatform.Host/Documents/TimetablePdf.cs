using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Timetables;

namespace SchoolPlatform.Host.Documents;

/// <summary>
/// Printed weekly timetable, drawn like the board on screen: a grid with separated cells, each class or break as a coloured
/// block (subject, teacher, group and room), the period times on the left and the institution crest in the header band.
/// </summary>
public sealed partial class PdfDocuments
{
    private static readonly string[] Weekdays = ["", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];
    private const string GridLine = "#E4DED3";
    private const string BreakFill = "#F3F1EC";
    private const string BreakEdge = "#C9C3B8";

    public byte[] Timetables(IReadOnlyList<PublishedTimetable> views, bool splitGroups, string? paper)
    {
        if (paper is not (null or "letter" or "a4")) throw OperationException.Invalid("Selecciona carta o A4.");
        var pages = views.SelectMany(v => splitGroups ? Split(v) : [v]).ToArray();
        if (pages.Length == 0 || pages.All(v => v.Slots.Length == 0)) throw OperationException.NotFound("No hay un horario publicado para esa fecha y selección.");
        // The crest (round badge) reads well at band size; the report-card artwork is a full composition and looks tiny.
        var logo = Logo(FirstAsset(School.Assets.CardLogo, School.Assets.MainLogo, School.Assets.ReportCardLogo));
        return Document.Create(doc =>
        {
            foreach (var view in pages.Where(v => v.Slots.Length > 0))
                doc.Page(page =>
                {
                    Base(page, (paper == "a4" ? PageSizes.A4 : PageSizes.Letter).Landscape());
                    page.Header().Element(c => Band(c, logo.Bytes, logo.Type, School.DisplayName, School.CampusLabel,
                        $"HORARIO SEMANAL · {view.Year.ToUpper(Culture)}", t => t.Span(view.Title).FontColor("#FFFFFF"),
                        $"Versión vigente al {LongDate(view.On)}"));
                    page.Content().PaddingHorizontal(26).PaddingTop(14).PaddingBottom(10).Element(c => TimetableGrid(c, view, splitGroups));
                    page.Footer().Element(c => FooterBand(c, $"Horario · {view.Year} · Vigente al {view.On.ToString("d", Culture)}"));
                });
        }).GeneratePdf();
    }

    private void TimetableGrid(IContainer container, PublishedTimetable view, bool splitGroups)
    {
        var days = view.Structure.Days;
        var periods = view.Structure.Periods.GroupBy(p => (p.Start, p.End)).Select(g => g.First()).OrderBy(p => p.Start).ToArray();
        // Denser weeks use smaller type so a full week still reads on one landscape page when possible.
        var dense = periods.Length > 9 || days.Length > 6;
        var subjectSize = dense ? 8f : 9.5f;
        var detailSize = dense ? 6.5f : 7.5f;
        container.AlignTop().Border(1).BorderColor(GridLine).CornerRadius(10).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(dense ? 62 : 74);
                foreach (var _ in days) cols.RelativeColumn();
            });
            table.Header(h =>
            {
                h.Cell().Background(Graphite).PaddingVertical(9).PaddingHorizontal(10).AlignMiddle()
                    .Text("PERÍODO").FontSize(7).Bold().LetterSpacing(0.12f).FontColor(Accent);
                foreach (var d in days)
                    h.Cell().Background(Graphite).BorderLeft(0.5f).BorderColor("#22FFFFFF").PaddingVertical(8).PaddingHorizontal(10).Column(c =>
                    {
                        c.Item().Text(Weekdays[d]).FontSize(10).Bold().FontColor("#FFFFFF");
                        var lessons = view.Slots.Count(s => s.Day == d && s.Kind != "Break");
                        c.Item().Text(lessons == 1 ? "1 clase" : $"{lessons} clases").FontSize(7).FontColor("#C9CDD1");
                    });
            });
            foreach (var p in periods)
            {
                table.Cell().Background(Warm).BorderTop(0.5f).BorderColor(GridLine).PaddingVertical(7).PaddingHorizontal(9).Column(c =>
                {
                    c.Item().Text(TimetableService.Clock(p.Start)).FontSize(dense ? 9 : 10.5f).ExtraBold().FontColor(Ink);
                    c.Item().Text(TimetableService.Clock(p.End)).FontSize(dense ? 7.5f : 8.5f).FontColor(Muted);
                    if (!dense) c.Item().PaddingTop(3).Text(p.Name).FontSize(6.5f).FontColor(Muted).ClampLines(2);
                });
                foreach (var d in days)
                {
                    var cells = view.Slots.Where(s => s.Day == d && s.Start == p.Start && s.End == p.End).ToArray();
                    table.Cell().BorderTop(0.5f).BorderLeft(0.5f).BorderColor(GridLine).Padding(dense ? 3 : 4).Column(col =>
                    {
                        col.Spacing(3);
                        foreach (var cell in cells) col.Item().Element(x => TimetableBlock(x, cell, splitGroups, subjectSize, detailSize, dense));
                        if (cells.Length == 0)
                            col.Item().MinHeight(dense ? 22 : 38).AlignCenter().AlignMiddle().Text("Libre").FontSize(detailSize).FontColor("#B9B3A8");
                    });
                }
            }
        });
    }

    private void TimetableBlock(IContainer container, TimetableSlot cell, bool splitGroups, float subjectSize, float detailSize, bool dense)
    {
        var isBreak = cell.Kind == "Break";
        container.Background(isBreak ? BreakFill : AccentSoft).Border(0.6f).BorderColor(isBreak ? GridLine : Mix(Accent, "#FFFFFF", 0.55))
            .CornerRadius(5).Row(row =>
            {
                row.ConstantItem(3).Background(isBreak ? BreakEdge : Accent);
                row.RelativeItem().PaddingVertical(dense ? 3 : 5).PaddingHorizontal(dense ? 4 : 6).Column(c =>
                {
                    c.Spacing(1);
                    c.Item().Text(isBreak ? "DESCANSO" : "CLASE").FontSize(5.5f).Bold().LetterSpacing(0.12f).FontColor(isBreak ? Muted : AccentDeep);
                    c.Item().Text(cell.Subject).FontSize(subjectSize).Bold().FontColor(Ink).LineHeight(1.1f).ClampLines(2);
                    if (!isBreak && cell.Teacher.Length > 0) c.Item().Text(cell.Teacher).FontSize(detailSize).FontColor(Muted).ClampLines(1);
                    if (!splitGroups && !isBreak)
                        c.Item().Text(cell.Grade + (cell.Section == null ? "" : " · " + cell.Section)).FontSize(detailSize).FontColor(Ink).ClampLines(1);
                    if (!isBreak && cell.Room.Length > 0) c.Item().Text("Aula: " + cell.Room).FontSize(detailSize).FontColor(Muted).ClampLines(1);
                });
            });
    }

    private static string FirstAsset(params string[] paths) => paths.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";

    private static IEnumerable<PublishedTimetable> Split(PublishedTimetable view)
    {
        foreach (var grade in view.Slots.GroupBy(s => s.GradeId))
        {
            var sections = grade.Where(s => s.SectionId != null).Select(s => s.SectionId).Distinct().ToArray();
            if (sections.Length == 0) sections = [null];
            foreach (var section in sections)
            {
                var slots = grade.Where(s => s.SectionId == null || s.SectionId == section).ToArray();
                var label = slots.FirstOrDefault(s => s.SectionId == section)?.Section;
                yield return view with { Title = grade.First().Grade + (label == null ? " · Todo el grado" : " · " + label), Slots = slots,
                    Structure = TimetableService.Combine(view.Scopes.Where(s => s.GradeId == grade.Key && (s.SectionId == null || s.SectionId == section)).Select(s => s.Structure)) };
            }
        }
    }
}
