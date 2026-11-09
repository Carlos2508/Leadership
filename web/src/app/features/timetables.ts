import { Component, ElementRef, ViewChild, ViewEncapsulation, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { GradeView, YearDetail, YearSummary, todayIn } from '../core/academic-api';
import { BoardCell, PublishedTime, TimeCatalog, TimePeriod, TimeSummary } from '../core/timetables';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { TimetableBoard } from '../shared/timetable-board';
@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, TimetableBoard],
  templateUrl: './timetables.html',
  // Timetable styles load with these lazy pages, not with the initial bundle.
  styleUrls: ['../../styles/_timetable.scss', '../../styles/_timetable-list.scss'],
  encapsulation: ViewEncapsulation.None,
})
export class Timetables {
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  private readonly school = inject(InstitutionService).current;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  @ViewChild('printDialog') private printDialog!: ElementRef<HTMLDialogElement>;
  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly detail = signal<YearDetail | null>(null);
  readonly summaries = signal<TimeSummary[]>([]);
  readonly catalog = signal<TimeCatalog>({ rooms: [], templates: [], assignments: [] });
  readonly published = signal<PublishedTime | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly cells = signal<BoardCell[]>([]);
  readonly boards = signal<{ title: string; cells: BoardCell[] }[]>([]);
  readonly periods = signal<TimePeriod[]>([]);
  readonly days = signal<number[]>([]);
  yearId = '';
  gradeId = '';
  sectionId = '';
  on = '';
  viewer = 'group';
  staffId = '';
  paper = 'letter';
  printScope = 'selection';
  private generation = 0;
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe(() => void this.load());
  }
  get manager() {
    return this.session.can('academic.read');
  }
  get canManage() {
    return this.session.can('academic.manage');
  }
  sections() {
    return this.detail()?.sections.filter((s) => s.gradeLevelId === this.gradeId) || [];
  }
  staff() {
    return [
      ...new Map(
        this.catalog().assignments.map((a) => [a.staffId, { id: a.staffId, name: a.name }]),
      ).values(),
    ].sort((a, b) => a.name.localeCompare(b.name));
  }
  async load(): Promise<void> {
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    this.published.set(null);
    this.cells.set([]);
    this.boards.set([]);
    this.periods.set([]);
    this.days.set([]);
    this.printDialog?.nativeElement.close();
    try {
      const q = this.route.snapshot.queryParamMap;
      const years = await this.api.get<YearSummary[]>(
        this.manager ? '/api/v1/academic/years' : '/api/v1/me/timetables/years',
      );
      if (generation !== this.generation) return;
      this.years.set(years);
      const requested = q.get('year');
      const year = requested
        ? years.find((y) => y.id === requested)
        : years.find((y) => y.isDefault) || years[0];
      if (requested && !year) throw new Error('No encontramos el ciclo solicitado para tu cuenta.');
      if (!year) return;
      this.yearId = year.id;
      const today = todayIn(this.school.locale.timeZone);
      this.on =
        q.get('on') ||
        (today < year.startsOn ? year.startsOn : today > year.endsOn ? year.endsOn : today);
      this.viewer =
        q.get('view') ||
        (this.manager
          ? 'group'
          : this.session.myCourses().length
            ? 'teacher'
            : `student:${this.session.studentId() || this.session.myStudents()[0]?.id || ''}`);
      this.staffId = q.get('staff') || '';
      this.gradeId = q.get('grade') || '';
      this.sectionId = q.get('section') || '';
      if (this.manager) {
        const [detail, grades, summaries, catalog] = await Promise.all([
          this.api.get<YearDetail>(`/api/v1/academic/years/${year.id}`),
          this.api.get<GradeView[]>('/api/v1/academic/grades'),
          this.api.get<TimeSummary[]>(`/api/v1/years/${year.id}/timetables`),
          this.api.get<TimeCatalog>(`/api/v1/years/${year.id}/timetables/catalog`),
        ]);
        if (generation !== this.generation) return;
        this.detail.set(detail);
        this.grades.set(grades);
        this.summaries.set(summaries);
        this.catalog.set(catalog);
        this.gradeId = this.gradeId || grades[0]?.id || '';
        if (this.gradeId && !grades.some((g) => g.id === this.gradeId))
          throw new Error('No encontramos el grado solicitado.');
        if (this.viewer === 'staff' && !this.staffId) this.staffId = this.staff()[0]?.id || '';
      }
      const path = this.readPath();
      if (!path) return;
      const view = await this.api.get<PublishedTime>(path);
      if (generation !== this.generation) return;
      this.published.set(view);
      this.periods.set(view.structure.periods.map((p) => ({ ...p, id: `${p.start}-${p.end}` })));
      this.days.set(view.structure.days);
      // For a grade with several sections use the group cards below to select one; the agenda includes the section label.
      this.cells.set(
        view.slots.map((s) => ({
          periodId: `${s.start}-${s.end}`,
          day: s.day,
          kind: s.kind,
          title: s.subject + (s.section && !this.sectionId ? ' · ' + s.section : ''),
          teacher: s.teacher,
          room: s.room,
        })),
      );
      const groups = new Map<string, { title: string; cells: BoardCell[] }>();
      view.slots.forEach((slot, index) => {
        const key =
          this.manager && this.viewer === 'group' && !this.sectionId
            ? slot.gradeId + ':' + (slot.sectionId || '')
            : 'selection';
        const group = groups.get(key) || {
          title: slot.grade + (slot.section ? ' · ' + slot.section : ' · Todo el grado'),
          cells: [],
        };
        group.cells.push(this.cells()[index]);
        groups.set(key, group);
      });
      this.boards.set([...groups.values()]);
    } catch (e) {
      if (generation === this.generation)
        this.error.set(
          e instanceof Error && !(e as { status?: number }).status
            ? e.message
            : this.api.message(e),
        );
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }
  readPath(): string | null {
    const query = new URLSearchParams({ yearId: this.yearId, on: this.on });
    if (this.manager && (this.viewer === 'group' || this.viewer === 'staff')) {
      const q = new URLSearchParams({ on: this.on });
      if (this.viewer === 'group') {
        if (this.gradeId) q.set('gradeId', this.gradeId);
        if (this.sectionId) q.set('sectionId', this.sectionId);
      } else {
        if (!this.staffId) return null;
        q.set('staffId', this.staffId);
      }
      return `/api/v1/years/${this.yearId}/timetables/published?${q}`;
    }
    if (this.viewer === 'teacher') return `/api/v1/me/timetables/teacher?${query}`;
    if (this.viewer.startsWith('student:') && this.viewer.slice(8))
      return `/api/v1/me/timetables/students/${this.viewer.slice(8)}?${query}`;
    return null;
  }
  async change(): Promise<void> {
    await this.router.navigate([], {
      queryParams: {
        year: this.yearId,
        grade: this.gradeId || null,
        section: this.sectionId || null,
        on: this.on,
        view: this.viewer,
        staff: this.staffId || null,
      },
    });
  }
  editParams(summary?: TimeSummary) {
    return {
      year: this.yearId,
      grade: summary?.gradeId || this.gradeId,
      section: (summary ? summary.sectionId : this.sectionId) || null,
    };
  }
  openPrint(): void {
    this.paper = 'letter';
    this.printScope = 'selection';
    this.printDialog.nativeElement.showModal();
  }
  printUrl(): string | null {
    const path = this.readPath();
    if (!path) return null;
    let url = path.replace('?', '.pdf?') + `&paper=${this.paper}`;
    if (this.printScope === 'all' && this.manager)
      url = `/api/v1/years/${this.yearId}/timetables/published.pdf?on=${this.on}&paper=${this.paper}`;
    return url;
  }
  visibleGroups() {
    return this.summaries().filter((s) => !this.gradeId || s.gradeId === this.gradeId);
  }
  async selectGroup(s: TimeSummary): Promise<void> {
    this.gradeId = s.gradeId;
    this.sectionId = s.sectionId || '';
    this.viewer = 'group';
    await this.change();
  }
}
