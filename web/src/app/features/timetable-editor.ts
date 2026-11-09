import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  ViewChild,
  ViewEncapsulation,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { PendingChanges } from '../core/pending-changes';
import {
  BoardCell,
  TimeAssignment,
  TimeCatalog,
  TimeCell,
  TimeCourse,
  TimeIssue,
  TimePeriod,
  TimeStructure,
  TimeSummary,
  TimeView,
  clock,
  minutes,
  weekdayNames,
} from '../core/timetables';
import { GradeView, YearDetail, YearSummary } from '../core/academic-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { TimetableBoard } from '../shared/timetable-board';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, TimetableBoard],
  templateUrl: './timetable-editor.html',
  // Timetable styles load with this lazy page, not with the initial bundle.
  styleUrls: ['../../styles/_timetable.scss', '../../styles/_timetable-editor.scss'],
  encapsulation: ViewEncapsulation.None,
})
export class TimetableEditor implements PendingChanges {
  private readonly cdr = inject(ChangeDetectorRef);
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  @ViewChild('cellDialog') private cellDialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('structureDialog') private structureDialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('copyDialog') private copyDialog!: ElementRef<HTMLDialogElement>;
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly dialogError = signal('');
  readonly catalog = signal<TimeCatalog>({ rooms: [], templates: [], assignments: [] });
  readonly courses = signal<TimeCourse[]>([]);
  readonly structure = signal<TimeStructure>({ days: [1, 2, 3, 4, 5], periods: [] });
  readonly cells = signal<TimeCell[]>([]);
  readonly issues = signal<TimeIssue[]>([]);
  readonly saved = signal<TimeView | null>(null);
  readonly touched = signal(false);
  readonly pending = signal<{ path: string; body: object; kind: 'save' | 'publish' } | null>(null);
  readonly board = computed<BoardCell[]>(() =>
    this.cells().map((c) => ({
      periodId: c.periodId,
      day: c.day,
      kind: c.kind,
      title:
        c.kind === 'Break'
          ? this.structure().periods.find((p) => p.id === c.periodId)?.name || 'Recreo'
          : this.courses().find((x) => x.id === c.courseId)?.subject || 'Materia por revisar',
      teacher:
        this.catalog().assignments.find((a) => a.id === c.assignmentId)?.name ||
        (c.kind === 'Lesson' ? 'Selecciona docente' : ''),
      room: this.catalog().rooms.find((r) => r.id === c.roomId)?.name || '',
      warning: this.issues().some((i) => i.day === c.day && i.periodId === c.periodId),
    })),
  );
  readonly names = weekdayNames;
  readonly clock = clock;
  yearId = '';
  gradeId = '';
  sectionId = '';
  group = '';
  year: YearSummary | null = null;
  startsOn = '';
  through = '';
  active: { period: TimePeriod; day: number } | null = null;
  cellForm = { kind: 'Lesson', courseId: '', assignmentId: '', roomId: '', days: [] as number[] };
  periodForm = { name: '', start: '', end: '' };
  working: TimeStructure = { days: [], periods: [] };
  templateName = '';
  templateId = '';
  roomName = '';
  copyYears: YearSummary[] = [];
  copyYear = '';
  copyItems: TimeSummary[] = [];
  copySource = '';
  copyMode = 'structure';
  copyDayFrom = 1;
  copyDayTo = 2;
  private revisionId: string | null = null;
  private version: string | null = null;
  constructor() {
    void this.init();
  }
  async init(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const q = this.route.snapshot.queryParamMap;
      this.yearId = q.get('year') || '';
      this.gradeId = q.get('grade') || '';
      this.sectionId = q.get('section') || '';
      if (!this.yearId || !this.gradeId)
        throw new Error('Selecciona primero el ciclo y grupo desde Horarios.');
      const [detail, grades, courses, catalog, summaries] = await Promise.all([
        this.api.get<YearDetail>(`/api/v1/academic/years/${this.yearId}`),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
        this.api.get<TimeCourse[]>(`/api/v1/years/${this.yearId}/courses?gradeId=${this.gradeId}`),
        this.api.get<TimeCatalog>(this.base + '/catalog'),
        this.api.get<TimeSummary[]>(this.base),
      ]);
      this.year = detail.year;
      if (this.year.status === 'Closed')
        throw new Error('El ciclo está cerrado. Puedes consultar sus horarios publicados.');
      const grade = grades.find((g) => g.id === this.gradeId);
      const section = detail.sections.find((s) => s.id === this.sectionId);
      if (!grade || (this.sectionId && (!section || section.gradeLevelId !== this.gradeId)))
        throw new Error('El grupo no pertenece a ese ciclo.');
      this.group = grade.name + (section ? ' · ' + section.name : ' · Todo el grado');
      this.courses.set(courses.filter((c) => !c.sectionId || c.sectionId === this.sectionId));
      this.catalog.set(catalog);
      const summary = summaries.find(
        (s) => s.gradeId === this.gradeId && (s.sectionId || '') === this.sectionId,
      );
      const id = summary?.draftId || q.get('revision') || summary?.publishedId;
      if (id) {
        const view = await this.api.get<TimeView>(this.base + '/revisions/' + id);
        if (view.gradeId !== this.gradeId || (view.sectionId || '') !== this.sectionId)
          throw new Error('Ese horario corresponde a otro grupo.');
        this.apply(view);
        if (view.state === 'Published') {
          this.revisionId = null;
          this.version = null;
          this.saved.set(null);
        }
      } else {
        this.startsOn = this.year.startsOn;
        this.through = this.year.endsOn;
      }
    } catch (e) {
      this.error.set(
        e instanceof Error && !(e instanceof HttpErrorResponse) ? e.message : this.api.message(e),
      );
    } finally {
      this.loading.set(false);
    }
  }
  get base() {
    return `/api/v1/years/${this.yearId}/timetables`;
  }
  mark(): void {
    this.touched.set(true);
    this.issues.set([]);
  }
  private apply(view: TimeView): void {
    this.structure.set(structuredClone(view.structure));
    this.cells.set(structuredClone(view.cells));
    this.startsOn = view.startsOn;
    this.through = view.through;
    this.revisionId = view.state === 'Draft' ? view.id : null;
    this.version = view.state === 'Draft' ? view.version : null;
    this.saved.set(view.state === 'Draft' ? view : null);
    this.issues.set(view.issues);
    this.touched.set(false);
  }
  openCell(active: { period: TimePeriod; day: number }): void {
    if (this.pending() || this.busy()) return;
    this.active = active;
    this.dialogError.set('');
    this.roomName = '';
    const cell = this.cells().find((c) => c.periodId === active.period.id && c.day === active.day);
    this.cellForm = {
      kind: cell?.kind || 'Lesson',
      courseId: cell?.courseId || '',
      assignmentId: cell?.assignmentId || '',
      roomId: cell?.roomId || '',
      days: [active.day],
    };
    this.cellDialog.nativeElement.showModal();
  }
  assignments(): TimeAssignment[] {
    return this.catalog().assignments.filter((a) => a.courseId === this.cellForm.courseId);
  }
  pickCourse(): void {
    const eligible = this.assignments().filter(
      (a) => a.startsOn <= this.startsOn && (!a.endsOn || a.endsOn >= this.through),
    );
    this.cellForm.assignmentId = eligible.length === 1 ? eligible[0].id : '';
  }
  toggleDay(day: number): void {
    this.cellForm.days = this.cellForm.days.includes(day)
      ? this.cellForm.days.filter((d) => d !== day)
      : [...this.cellForm.days, day];
  }
  putCell(): void {
    const f = this.cellForm;
    if (!this.active || !f.days.length || (f.kind === 'Lesson' && !f.courseId)) {
      this.dialogError.set('Selecciona una materia y al menos un día.');
      return;
    }
    const period = this.active.period.id;
    const changes = f.days.map((day) => ({
      periodId: period,
      day,
      kind: f.kind as 'Lesson' | 'Break',
      courseId: f.kind === 'Lesson' ? f.courseId : null,
      assignmentId: f.kind === 'Lesson' ? f.assignmentId || null : null,
      roomId: f.kind === 'Lesson' ? f.roomId || null : null,
    }));
    this.cells.set([
      ...this.cells().filter((c) => c.periodId !== period || !f.days.includes(c.day)),
      ...changes,
    ]);
    this.mark();
    this.cellDialog.nativeElement.close();
  }
  removeCell(): void {
    if (!this.active) return;
    this.cells.update((cs) =>
      cs.filter((c) => c.periodId !== this.active!.period.id || c.day !== this.active!.day),
    );
    this.mark();
    this.cellDialog.nativeElement.close();
  }
  async addRoom(): Promise<void> {
    this.dialogError.set('');
    if (!this.roomName.trim()) return;
    try {
      const result = await this.api.post<{ id: string }>(this.base + '/rooms', {
        name: this.roomName,
      });
      this.catalog.set(await this.api.get<TimeCatalog>(this.base + '/catalog'));
      this.cellForm.roomId = result.id;
      this.roomName = '';
    } catch (e) {
      this.dialogError.set(this.api.message(e));
    }
  }
  openStructure(): void {
    this.working = structuredClone(this.structure());
    this.periodForm = { name: '', start: '', end: '' };
    this.templateName = '';
    this.templateId = '';
    this.dialogError.set('');
    this.structureDialog.nativeElement.showModal();
  }
  toggleWorkingDay(day: number): void {
    this.working.days = this.working.days.includes(day)
      ? this.working.days.filter((d) => d !== day)
      : [...this.working.days, day].sort();
  }
  addPeriod(): void {
    const f = this.periodForm;
    const start = minutes(f.start),
      end = minutes(f.end);
    if (
      !f.name.trim() ||
      !Number.isFinite(start) ||
      !Number.isFinite(end) ||
      start >= end ||
      this.working.periods.some((p) => p.start < end && start < p.end)
    ) {
      this.dialogError.set('Escribe nombre y horas válidas, sin superponer períodos.');
      return;
    }
    this.working.periods.push({ id: crypto.randomUUID(), name: f.name.trim(), start, end });
    this.working.periods.sort((a, b) => a.start - b.start);
    this.periodForm = { name: '', start: f.end, end: '' };
    this.dialogError.set('');
  }
  async applyStructure(): Promise<void> {
    if (!this.working.days.length || !this.working.periods.length) {
      this.dialogError.set('Agrega al menos un día y un período.');
      return;
    }
    const removed = this.cells().filter(
      (c) =>
        !this.working.days.includes(c.day) ||
        !this.working.periods.some((p) => p.id === c.periodId),
    );
    if (
      removed.length &&
      !(await this.alerts.confirm({
        title: 'Se retirarán bloques del borrador',
        text: `La nueva estructura deja fuera ${removed.length} bloques. El horario publicado se conserva.`,
        confirmText: 'Aplicar estructura',
        danger: true,
      }))
    )
      return;
    this.structure.set(structuredClone(this.working));
    this.cells.update((cs) =>
      cs.filter(
        (c) =>
          this.working.days.includes(c.day) &&
          this.working.periods.some((p) => p.id === c.periodId),
      ),
    );
    this.mark();
    this.structureDialog.nativeElement.close();
  }
  chooseTemplate(): void {
    const t = this.catalog().templates.find((t) => t.id === this.templateId);
    if (t) this.working = structuredClone(t.structure);
  }
  async saveTemplate(): Promise<void> {
    try {
      await this.api.post(this.base + '/templates', {
        name: this.templateName,
        structure: this.working,
      });
      this.catalog.set(await this.api.get<TimeCatalog>(this.base + '/catalog'));
      this.alerts.success('Estructura guardada para reutilizar.');
    } catch (e) {
      this.dialogError.set(this.api.message(e));
    }
  }
  async copyDay(): Promise<void> {
    if (this.copyDayFrom === this.copyDayTo) return;
    if (
      this.cells().some((c) => c.day === Number(this.copyDayTo)) &&
      !(await this.alerts.confirm({
        title: 'Reemplazar el día de destino',
        text: 'Se copiarán las clases y recreos del día elegido dentro de este borrador.',
        confirmText: 'Copiar día',
      }))
    )
      return;
    this.cells.update((cs) => [
      ...cs.filter((c) => c.day !== Number(this.copyDayTo)),
      ...cs
        .filter((c) => c.day === Number(this.copyDayFrom))
        .map((c) => ({ ...c, day: Number(this.copyDayTo) })),
    ]);
    this.mark();
  }
  async openCopy(): Promise<void> {
    this.dialogError.set('');
    this.copyYear = this.yearId;
    this.copySource = '';
    this.copyItems = [];
    this.copyDialog.nativeElement.showModal();
    try {
      this.copyYears = await this.api.get<YearSummary[]>('/api/v1/academic/years');
      await this.loadCopy();
    } catch (e) {
      this.dialogError.set(this.api.message(e));
    }
  }
  async loadCopy(): Promise<void> {
    const year = this.copyYear;
    this.copySource = '';
    try {
      const rows = await this.api.get<TimeSummary[]>(`/api/v1/years/${year}/timetables`);
      if (this.copyYear === year) this.copyItems = rows;
    } catch (e) {
      this.dialogError.set(this.api.message(e));
    } finally {
      this.copyItems = [...this.copyItems];
      this.cdr.markForCheck();
    }
  }
  async applyCopy(): Promise<void> {
    if (!this.copySource) return;
    this.dialogError.set('');
    try {
      const view = await this.api.get<TimeView>(
        `/api/v1/years/${this.copyYear}/timetables/revisions/${this.copySource}`,
      );
      const sourceCourses = await this.api.get<TimeCourse[]>(
        `/api/v1/years/${this.copyYear}/courses?gradeId=${view.gradeId}`,
      );
      if (
        this.cells().length &&
        !(await this.alerts.confirm({
          title: 'Reemplazar el borrador en pantalla',
          text: 'Se conservará la versión publicada. Revisa las materias y docentes después de copiar.',
          confirmText: 'Copiar horario',
        }))
      )
        return;
      this.structure.set(structuredClone(view.structure));
      let omitted = 0;
      const cells =
        this.copyMode === 'structure'
          ? []
          : view.cells.flatMap((c) => {
              if (c.kind === 'Break') return [c];
              const subject = sourceCourses.find((s) => s.id === c.courseId)?.subjectId;
              const target = this.courses().find((s) => s.subjectId === subject);
              if (!target) {
                omitted++;
                return [];
              }
              const oldTeacher =
                this.copyYear === this.yearId
                  ? this.catalog().assignments.find((a) => a.id === c.assignmentId)
                  : null;
              const eligible = this.catalog().assignments.filter(
                (a) =>
                  a.courseId === target.id &&
                  a.startsOn <= this.startsOn &&
                  (!a.endsOn || a.endsOn >= this.through),
              );
              return [
                {
                  ...c,
                  courseId: target.id,
                  assignmentId:
                    eligible.find((a) => a.staffId === oldTeacher?.staffId)?.id ||
                    (eligible.length === 1 ? eligible[0].id : null),
                },
              ];
            });
      this.cells.set(cells);
      this.mark();
      this.copyDialog.nativeElement.close();
      this.alerts.info(
        omitted
          ? `Se omitieron ${omitted} bloques cuyas materias no existen en el destino. Revisa el borrador.`
          : 'Copia preparada. Revisa docentes y aulas antes de publicar.',
      );
    } catch (e) {
      this.dialogError.set(this.api.message(e));
    }
  }
  async save(): Promise<void> {
    if (this.busy()) return;
    this.pending.set(
      this.pending() || {
        kind: 'save',
        path: this.base,
        body: {
          requestId: crypto.randomUUID(),
          gradeId: this.gradeId,
          sectionId: this.sectionId || null,
          revisionId: this.revisionId,
          version: this.version,
          startsOn: this.startsOn,
          through: this.through,
          structure: structuredClone(this.structure()),
          cells: structuredClone(this.cells()),
          reason: 'Preparación y revisión del horario escolar.',
        },
      },
    );
    await this.send();
  }
  async publish(): Promise<void> {
    if (!this.saved() || this.touched() || this.busy() || this.pending()) return;
    const reason = await this.alerts.reason({
      title: 'Publicar horario',
      text: `${this.group}. Se mostrará a docentes y familias durante la vigencia indicada.`,
      confirmText: 'Publicar horario',
      label: 'Motivo de publicación',
    });
    if (!reason) return;
    this.pending.set({
      kind: 'publish',
      path: this.base + '/revisions/' + this.saved()!.id + '/publish',
      body: { requestId: crypto.randomUUID(), version: this.version, reason },
    });
    await this.send();
  }
  private async send(): Promise<void> {
    const command = this.pending();
    if (!command) return;
    this.busy.set(true);
    this.error.set('');
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      const result = await Promise.race([
        this.api.post<{ revisionId: string; version: string }>(command.path, command.body),
        new Promise<never>((_, reject) => {
          timer = setTimeout(() => reject(new Error('timeout')), 20000);
        }),
      ]);
      this.version = result.version;
      this.revisionId = result.revisionId;
      if (command.kind === 'publish') {
        this.pending.set(null);
        this.touched.set(false);
        this.busy.set(false);
        this.alerts.success('Horario publicado.');
        await this.router.navigate(['/workspace/timetables'], {
          queryParams: {
            year: this.yearId,
            grade: this.gradeId,
            section: this.sectionId || null,
            on: this.startsOn,
          },
        });
      } else {
        const view = await this.api.get<TimeView>(this.base + '/revisions/' + result.revisionId);
        this.apply(view);
        this.pending.set(null);
        this.alerts.success(
          view.issues.length
            ? 'Borrador guardado. Hay detalles por revisar antes de publicar.'
            : 'Borrador guardado.',
        );
      }
    } catch (e) {
      if (e instanceof HttpErrorResponse && [400, 401, 403, 404, 409, 429].includes(e.status))
        this.pending.set(null);
      this.error.set(
        this.pending()
          ? 'No se confirmó la respuesta. Reintenta el mismo intento para evitar duplicados.'
          : this.api.message(e),
      );
    } finally {
      clearTimeout(timer);
      this.busy.set(false);
    }
  }
  async retry(): Promise<void> {
    await this.send();
  }
  async canLeave(): Promise<boolean> {
    if (this.busy()) return false;
    if (!this.touched() && !this.pending()) return true;
    return this.alerts.confirm({
      title: 'Hay cambios sin confirmar',
      text: this.pending()
        ? 'La operación podría haberse guardado. Reintenta antes de volver a capturar el horario.'
        : '¿Quieres salir sin guardar los cambios del horario?',
      confirmText: 'Salir',
    });
  }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(e: BeforeUnloadEvent) {
    if (this.touched() || this.pending()) {
      e.preventDefault();
      e.returnValue = '';
    }
  }
}
