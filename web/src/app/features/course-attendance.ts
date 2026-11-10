import {
  ChangeDetectorRef,
  Component,
  OnDestroy,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { combineLatest, distinctUntilChanged, map } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { CourseView } from './courses';
import { Shell } from '../shared/shell';
import { CourseNav } from '../shared/course-nav';
import { Icon } from '../shared/icon';
import { PluralPipe, plural } from '../shared/plural.pipe';

type Status = 'Present' | 'Absent' | 'Late' | 'Excused';
interface Row {
  studentId: string;
  name: string;
  code: string;
  status: Status | null;
  note: string;
  version: string | null;
  draft: Status;
  draftNote: string;
}
interface Sheet {
  course: CourseView;
  date: string;
  canEdit: boolean;
  readOnlyReason: string | null;
  rows: Omit<Row, 'draft' | 'draftNote'>[];
}

/** Daily attendance of a course for the assigned teacher; everyone starts as present until changed. */
@Component({
  imports: [Shell, Icon, CourseNav, FormsModule, PluralPipe],
  template: `
    <app-shell title="Asistencia">
      <app-course-nav [courseId]="id()" active="attendance" />
      @if (course(); as c) {
        <div class="page-heading">
          <div>
            <span class="eyebrow">ASISTENCIA</span>
            <h1>{{ c.subject }}</h1>
            <p>{{ c.grade }} · {{ c.section ? 'Sección ' + c.section : 'Todo el grado' }}</p>
          </div>
        </div>
      }
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
        @if (!sheet()) {
          <button class="button outline" (click)="load()">Reintentar asistencia</button>
        }
      }
      @if (!notFound()) {
        <section class="card directory-card" [attr.aria-busy]="loading()">
          <div class="filters">
            <div class="day-picker">
              <button
                type="button"
                class="icon-button day-step"
                (click)="shift(-1)"
                aria-label="Día anterior"
                [disabled]="!date || busy()"
              >
                <app-icon name="arrow" />
              </button>
              <label class="select-field date-field"
                ><span>Fecha</span
                ><input
                  type="date"
                  name="date"
                  [(ngModel)]="date"
                  (change)="changeDate()"
                  [disabled]="busy()"
              /></label>
              <button
                type="button"
                class="icon-button day-step next"
                (click)="shift(1)"
                aria-label="Día siguiente"
                [disabled]="!date || busy()"
              >
                <app-icon name="arrow" />
              </button>
            </div>
          </div>
        </section>
      }
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a este curso</h2>
          <p>Solo el personal académico y los docentes asignados pueden registrar asistencia.</p>
        </section>
      } @else if (loading()) {
        <p class="loading-state card" role="status">Cargando asistencia…</p>
      } @else if (sheet(); as s) {
        <section class="card directory-card">
          <div class="filters">
            @if (!s.canEdit) {
              <p class="feedback info-feedback" role="note">
                {{ s.readOnlyReason || 'Esta lista es de consulta.' }}
              </p>
            }
            <p class="directory-summary attendance-counts" aria-live="polite">
              @if (dirty()) {
                <span class="pill gold">Cambios sin guardar</span>
              } @else if (recorded() && recorded() === rows().length) {
                <span class="pill mint">Registrada</span>
              } @else if (recorded()) {
                <span class="pill gold"
                  >Registro parcial · {{ recorded() }} de {{ rows().length }}</span
                >
              } @else {
                <span class="pill gold">Sin registrar</span>
              }
              @if (recorded() || dirty()) {
                {{ counts().present | plural: 'presente' : 'presentes' }} ·
                {{ counts().absent | plural: 'ausencia' : 'ausencias' }} ·
                {{ counts().late | plural: 'tardanza' : 'tardanzas' }} ·
                {{ counts().excused | plural: 'justificada' : 'justificadas' }}
              } @else if (rows().length && s.canEdit) {
                Todos aparecen como presentes; marca solo las excepciones y guarda.
              }
            </p>
          </div>
          @if (rows().length) {
            <form (ngSubmit)="save()">
              <ul class="attendance-list">
                @for (row of rows(); track row.studentId) {
                  <li>
                    <span class="person-copy"
                      ><strong>{{ row.name }}</strong
                      ><small>{{ row.code }}</small></span
                    >
                    <fieldset class="attendance-options" [disabled]="!s.canEdit || busy()">
                      <legend class="sr-only">Asistencia de {{ row.name }}</legend>
                      @for (option of options; track option.value) {
                        <label
                          class="attendance-option"
                          [class.selected]="row.draft === option.value"
                          [attr.data-status]="option.value"
                          ><input
                            type="radio"
                            [name]="'status-' + row.studentId"
                            [value]="option.value"
                            [(ngModel)]="row.draft"
                            (ngModelChange)="touch()"
                          /><span>{{ option.label }}</span></label
                        >
                      }
                    </fieldset>
                    @if (row.draft !== 'Present') {
                      <label class="note-field"
                        ><span class="sr-only">Observación de {{ row.name }}</span
                        ><input
                          [name]="'note-' + row.studentId"
                          [(ngModel)]="row.draftNote"
                          (ngModelChange)="touch()"
                          maxlength="200"
                          placeholder="Observación (opcional)"
                          [disabled]="!s.canEdit || busy()"
                      /></label>
                    }
                  </li>
                }
              </ul>
              @if (s.canEdit) {
                <div class="sheet-actions">
                  <button
                    class="button outline"
                    type="button"
                    (click)="allPresent()"
                    [disabled]="busy()"
                  >
                    Todos presentes
                  </button>
                  <button class="button" type="submit" [disabled]="busy() || !canSave()">
                    {{ busy() ? 'Guardando…' : 'Guardar asistencia' }}
                  </button>
                </div>
              }
            </form>
          } @else {
            <p class="empty-inline padded">No hay estudiantes en el curso en esa fecha.</p>
          }
        </section>
      }
    </app-shell>
  `,
})
export class CourseAttendance implements OnDestroy {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private request = 0;
  private saving: Promise<void> | null = null;
  private leaving: Promise<boolean> | null = null;
  readonly course = signal<CourseView | null>(null);
  readonly sheet = signal<Sheet | null>(null);
  readonly rows = signal<Row[]>([]);
  readonly notFound = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly loading = signal(true);
  readonly recorded = computed(() => this.rows().filter((x) => x.status !== null).length);
  readonly dirty = computed(() =>
    this.rows().some(
      (x) =>
        x.draft !== (x.status ?? 'Present') ||
        (x.draft === 'Present' ? '' : x.draftNote.trim()) !==
          (x.status === 'Present' || x.status === null ? '' : x.note),
    ),
  );
  readonly canSave = computed(
    () =>
      this.sheet()?.canEdit &&
      this.rows().length > 0 &&
      (this.dirty() || this.recorded() < this.rows().length),
  );
  readonly counts = computed(() => {
    const rows = this.rows();
    return {
      present: rows.filter((x) => x.draft === 'Present').length,
      absent: rows.filter((x) => x.draft === 'Absent').length,
      late: rows.filter((x) => x.draft === 'Late').length,
      excused: rows.filter((x) => x.draft === 'Excused').length,
    };
  });
  readonly options: { value: Status; label: string }[] = [
    { value: 'Present', label: 'Presente' },
    { value: 'Absent', label: 'Ausente' },
    { value: 'Late', label: 'Tarde' },
    { value: 'Excused', label: 'Justificada' },
  ];
  date = '';

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(
        map(([params, query]) => [params.get('id')!, query.get('date') ?? undefined] as const),
        distinctUntilChanged((a, b) => a[0] === b[0] && a[1] === b[1]),
        takeUntilDestroyed(),
      )
      .subscribe(([id, date]) => {
        if (id !== this.course()?.id) this.course.set(null);
        void this.load(date, id);
      });
  }
  ngOnDestroy(): void {
    this.request++;
  }
  canLeave(): Promise<boolean> {
    if (this.leaving) return this.leaving;
    this.leaving = this.prepareToLeave().finally(() => {
      this.leaving = null;
    });
    return this.leaving;
  }
  private async prepareToLeave(): Promise<boolean> {
    if (this.saving) await this.saving;
    if (!this.dirty()) return true;
    return this.alerts.confirm({
      title: 'Asistencia sin guardar',
      text: 'Conserva tus marcas y observaciones para guardarlas antes de continuar.',
      confirmText: 'Descartar y continuar',
      cancelText: 'Seguir editando',
      danger: true,
    });
  }
  touch(): void {
    this.rows.set([...this.rows()]);
  }
  formatted(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  allPresent(): void {
    for (const row of this.rows()) row.draft = 'Present';
    this.touch();
  }
  /** Adjacent calendar day: the school's teaching calendar is not inferred. */
  async shift(days: number): Promise<void> {
    if (!this.date) return;
    const value = new Date(this.date + 'T12:00:00Z');
    if (Number.isNaN(value.getTime())) return;
    value.setUTCDate(value.getUTCDate() + days);
    this.date = value.toISOString().slice(0, 10);
    await this.changeDate();
  }
  async changeDate(): Promise<void> {
    if (!this.date || !(await this.router.navigate([], { queryParams: { date: this.date } })))
      this.date = this.sheet()?.date ?? this.route.snapshot.queryParamMap.get('date') ?? '';
    this.changeDetector.markForCheck();
  }
  async load(
    date = this.route.snapshot.queryParamMap.get('date') ?? undefined,
    id = this.id(),
  ): Promise<void> {
    const request = ++this.request;
    this.loading.set(true);
    this.notFound.set(false);
    this.error.set('');
    this.sheet.set(null);
    this.rows.set([]);
    this.date = date ?? '';
    try {
      const query = date ? `?date=${encodeURIComponent(date)}` : '';
      const sheet = await this.api.get<Sheet>(
        `/api/v1/courses/${encodeURIComponent(id)}/attendance${query}`,
      );
      if (request !== this.request) return;
      this.course.set(sheet.course);
      this.date = sheet.date;
      if (!date) {
        await this.router.navigate([], { queryParams: { date: sheet.date }, replaceUrl: true });
        return;
      }
      this.sheet.set(sheet);
      this.rows.set(
        sheet.rows.map((x) => ({ ...x, draft: x.status ?? 'Present', draftNote: x.note })),
      );
    } catch (error) {
      if (request !== this.request) return;
      if (error instanceof HttpErrorResponse && error.status === 404) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }
  save(): Promise<void> {
    if (this.saving) return this.saving;
    this.saving = this.saveChanges().finally(() => {
      this.saving = null;
    });
    return this.saving;
  }
  private async saveChanges(): Promise<void> {
    const sheet = this.sheet();
    if (!sheet || !this.canSave() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.post<{ changed: number }>(
        `/api/v1/courses/${sheet.course.id}/attendance`,
        {
          date: sheet.date,
          entries: this.rows().map((x) => ({
            studentId: x.studentId,
            status: x.draft,
            note: x.draft === 'Present' ? null : x.draftNote,
            version: x.version,
          })),
        },
      );
      if (result.changed)
        this.alerts.success(
          `Asistencia guardada (${plural(result.changed, 'cambio', 'cambios')}).`,
        );
      else this.alerts.info('No había cambios.');
      await this.load();
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
