using QuestPDF.Fluent;
using QuestPDF.Helpers;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Timetables;

namespace SchoolPlatform.Host.Documents;
public sealed partial class PdfDocuments
{
    private static readonly string[] Weekdays = ["", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];
    public byte[] Timetables(IReadOnlyList<PublishedTimetable> views, bool splitGroups, string? paper)
    {
        if (paper is not (null or "letter" or "a4")) throw OperationException.Invalid("Selecciona carta o A4.");
        var pages = views.SelectMany(v => splitGroups ? Split(v) : [v]).ToArray();
        if (pages.Length == 0 || pages.All(v => v.Slots.Length == 0)) throw OperationException.NotFound("No hay un horario publicado para esa fecha y selección.");
        var logo = Logo(School.Assets.ReportCardLogo);
        return Document.Create(doc => {
            foreach (var view in pages.Where(v => v.Slots.Length > 0)) doc.Page(page => {
                Base(page, (paper == "a4" ? PageSizes.A4 : PageSizes.Letter).Landscape());
                page.Header().Element(c => Band(c, logo.Bytes, logo.Type, School.DisplayName, School.CampusLabel,
                    $"HORARIO PUBLICADO · {view.Year}", t => t.Span(view.Title).FontColor("#FFFFFF"), $"Versión vigente al {view.On.ToString("d", Culture)}"));
                var days = view.Structure.Days;
                var periods = view.Structure.Periods.Select(p => (p.Start,p.End)).ToArray();
                page.Content().PaddingTop(12).Table(table => {
                    table.ColumnsDefinition(cols => { cols.ConstantColumn(68); foreach (var _ in days) cols.RelativeColumn(); });
                    table.Header(h => {
                        h.Cell().Background(Graphite).Padding(8).Text("Hora").FontColor("#FFFFFF").SemiBold();
                        foreach (var d in days) h.Cell().Background(Graphite).Padding(8).Text(Weekdays[d]).FontColor("#FFFFFF").SemiBold();
                    });
                    foreach (var p in periods) {
                        table.Cell().Background(Warm).BorderBottom(0.5f).BorderColor(Line).Padding(7).Text($"{TimetableService.Clock(p.Start)}\n{TimetableService.Clock(p.End)}").FontSize(9).SemiBold();
                        foreach (var d in days) {
                            var cells = view.Slots.Where(s => s.Day == d && s.Start == p.Start && s.End == p.End).ToArray();
                            table.Cell().BorderBottom(0.5f).BorderColor(Line).Padding(7).Column(col => {
                                col.Spacing(4);
                                foreach (var cell in cells) col.Item().Column(c => {
                                    c.Item().Text(cell.Subject).FontSize(10).SemiBold().FontColor(cell.Kind == "Break" ? Muted : Ink);
                                    if (cell.Teacher.Length > 0) c.Item().Text(cell.Teacher).FontSize(8).FontColor(Muted);
                                    if (!splitGroups && cell.Kind != "Break") c.Item().Text(cell.Grade + (cell.Section == null ? "" : " · " + cell.Section)).FontSize(8);
                                    if (cell.Room.Length > 0) c.Item().Text(cell.Room).FontSize(8).FontColor(Muted);
                                });
                                if (cells.Length == 0) col.Item().Text("—").FontColor(Muted);
                            });
                        }
                    }
                });
                page.Footer().Element(c => FooterBand(c, $"Horario · {view.Year} · Vigente al {view.On.ToString("d", Culture)}"));
            });
        }).GeneratePdf();
    }
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
