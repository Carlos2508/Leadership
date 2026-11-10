import {
  Component,
  ElementRef,
  HostListener,
  ViewChild,
  ViewEncapsulation,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { PendingChanges } from '../core/pending-changes';
import { InstitutionService } from '../core/institution';
import {
  MyGrades,
  StudentAccessView,
  StudentTask,
  capitalize,
  isoDate,
  monthWeeks,
  points,
  subjectTone,
  weekdayShort,
} from '../core/student-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { StudentHeader } from '../shared/student-header';

type Filter = 'pending' | 'submitted' | 'reviewed' | 'overdue' | 'all';

/** Homework of a student: pending work first, history, a due-date calendar and the submission dialog (student only). */
@Component({
  imports: [Shell, Icon, FormsModule, StudentHeader],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../styles/_student-pages.scss', '../../styles/_student-tasks.scss'],
  template: `
    <app-shell title="Tareas">
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a esta información</h2>
          <p>Si crees que es un error, comunícate con la administración del colegio.</p>
        </section>
      } @else if (view(); as v) {
        <app-student-header [view]="v" title="Tareas" />
        @if (!v.isSelf && !v.faculties.includes('AcademicRecords')) {
          <section class="card empty-state">
            <app-icon name="shield" />
            <h2>Sin acceso a tareas</h2>
            <p>Tu vínculo no incluye el seguimiento académico.</p>
          </section>
        } @else if (error()) {
          <p class="feedback error-feedback" role="alert">
            {{ error() }} <button class="text-button" (click)="load()">Reintentar</button>
          </p>
        } @else if (tasks(); as list) {
          <section class="sp-stats" aria-label="Resumen de tareas">
            <button
              type="button"
              class="sp-stat"
              [class.active]="filter() === 'pending'"
              (click)="filter.set('pending')"
            >
              <span class="sp-stat-icon tone-gold"><app-icon name="clipboard" /></span>
              <span
                ><strong>{{ counts().pending }}</strong
                ><small>Por entregar</small></span
              >
            </button>
            <button
              type="button"
              class="sp-stat"
              [class.active]="filter() === 'overdue'"
              (click)="filter.set('overdue')"
            >
              <span class="sp-stat-icon tone-red"><app-icon name="clock" /></span>
              <span
                ><strong>{{ counts().overdue }}</strong
                ><small>Vencidas sin entregar</small></span
              >
            </button>
            <button
              type="button"
              class="sp-stat"
              [class.active]="filter() === 'submitted'"
              (click)="filter.set('submitted')"
            >
              <span class="sp-stat-icon tone-blue"><app-icon name="check" /></span>
              <span
                ><strong>{{ counts().submitted }}</strong
                ><small>Entregadas</small></span
              >
            </button>
            <button
              type="button"
              class="sp-stat"
              [class.active]="filter() === 'reviewed'"
              (click)="filter.set('reviewed')"
            >
              <span class="sp-stat-icon tone-green"><app-icon name="star" /></span>
              <span
                ><strong>{{ counts().reviewed }}</strong
                ><small>Revisadas</small></span
              >
            </button>
          </section>

          @if (notice()) {
            <p class="feedback success-feedback" role="status">{{ notice() }}</p>
          }

          <div class="sp-toolbar">
            <div class="segmented sp-segment" role="tablist" aria-label="Filtrar tareas">
              @for (f of filters; track f.value) {
                <button
                  type="button"
                  role="tab"
                  [attr.aria-selected]="filter() === f.value"
                  [class.active]="filter() === f.value"
                  (click)="filter.set(f.value)"
                >
                  {{ f.label }}
                </button>
              }
            </div>
            <div class="sp-toolbar-end">
              <label class="select-field sp-course-filter"
                ><span class="sr-only">Curso</span
                ><select [ngModel]="course()" (ngModelChange)="course.set($event)" name="course">
                  <option value="">Todos los cursos</option>
                  @for (c of courses(); track c) {
                    <option [value]="c">{{ c }}</option>
                  }
                </select></label
              >
              <div class="sp-view-toggle" role="group" aria-label="Vista">
                <button
                  type="button"
                  [class.active]="mode() === 'list'"
                  [attr.aria-pressed]="mode() === 'list'"
                  (click)="mode.set('list')"
                >
                  Lista
                </button>
                <button
                  type="button"
                  [class.active]="mode() === 'calendar'"
                  [attr.aria-pressed]="mode() === 'calendar'"
                  (click)="mode.set('calendar')"
                >
                  Calendario
                </button>
              </div>
            </div>
          </div>

          @if (mode() === 'calendar') {
            <section class="card sp-calendar" aria-label="Calendario de entregas">
              <header class="sp-calendar-head">
                <button
                  type="button"
                  class="icon-button"
                  (click)="shiftMonth(-1)"
                  aria-label="Mes anterior"
                >
                  <app-icon name="chevron" class="flip" />
                </button>
                <h2>{{ monthLabel() }}</h2>
                <button
                  type="button"
                  class="icon-button"
                  (click)="shiftMonth(1)"
                  aria-label="Mes siguiente"
                >
                  <app-icon name="chevron" />
                </button>
              </header>
              <div class="sp-month" role="group" [attr.aria-label]="monthLabel()">
                @for (d of weekdays; track d) {
                  <span class="sp-weekday" aria-hidden="true">{{ d }}</span>
                }
                @for (week of weeks(); track $index) {
                  @for (cell of week; track cell.date) {
                    <button
                      type="button"
                      class="sp-day"
                      [class.outside]="!cell.inside"
                      [class.today]="cell.date === today"
                      [class.selected]="cell.date === selectedDay()"
                      [attr.aria-label]="
                        longDate(cell.date) +
                        ': ' +
                        (byDay().get(cell.date)?.length ?? 0) +
                        ' entregas'
                      "
                      (click)="selectedDay.set(cell.date)"
                    >
                      <span class="sp-day-number">{{ cell.day }}</span>
                      @for (t of (byDay().get(cell.date) ?? []).slice(0, 3); track t.id) {
                        <span
                          class="sp-day-chip"
                          [attr.data-tone]="tone(t.course)"
                          [class.done]="!!t.submissionStatus"
                          >{{ t.title }}</span
                        >
                      }
                      @if ((byDay().get(cell.date)?.length ?? 0) > 3) {
                        <span class="sp-day-more"
                          >+{{ (byDay().get(cell.date)?.length ?? 0) - 3 }}</span
                        >
                      }
                    </button>
                  }
                }
              </div>
            </section>
          }

          <section
            class="sp-task-list"
            [attr.aria-label]="mode() === 'calendar' ? 'Tareas del día seleccionado' : 'Tareas'"
          >
            @if (mode() === 'calendar') {
              <h2 class="sp-section-title">
                {{
                  selectedDay()
                    ? 'Entregas del ' + longDate(selectedDay()!)
                    : 'Elige un día del calendario'
                }}
              </h2>
            }
            @for (task of visible(); track task.id) {
              <article class="card sp-task" [attr.data-tone]="tone(task.course)">
                <div class="sp-task-main">
                  <div class="sp-task-top">
                    <span class="sp-subject" [attr.data-tone]="tone(task.course)">{{
                      task.course
                    }}</span>
                    <span class="sp-status" [attr.data-state]="state(task)">{{
                      stateLabel(task)
                    }}</span>
                  </div>
                  <h3>{{ task.title }}</h3>
                  @if (task.instructions) {
                    <p class="sp-task-text">{{ task.instructions }}</p>
                  }
                  <div class="sp-task-meta">
                    <span [class.urgent]="urgent(task)"
                      ><app-icon name="clock" />{{ dueText(task) }}</span
                    >
                    @if (task.points !== null) {
                      <span
                        ><app-icon name="star" />Vale {{ fmt(task.points) }} pts{{
                          task.period ? ' · ' + task.period : ''
                        }}</span
                      >
                    }
                    @if (earned(task); as e) {
                      <span class="sp-earned"><app-icon name="check" />Obtuviste {{ e }}</span>
                    }
                    @if (task.submittedAt) {
                      <span
                        >Entregada {{ api.date(task.submittedAt)
                        }}{{ task.late ? ' · tarde' : '' }}</span
                      >
                    }
                  </div>
                  @if (task.feedback) {
                    <p class="sp-feedback">
                      <strong>Comentario del docente:</strong> {{ task.feedback }}
                    </p>
                  }
                </div>
                <div class="sp-task-actions">
                  @if (task.canSubmit) {
                    <button class="button compact-button" (click)="openSubmit(task)">
                      {{ task.submissionStatus ? 'Actualizar entrega' : 'Entregar' }}
                      <span class="sr-only">{{ task.title }}</span>
                    </button>
                  } @else {
                    <button class="button outline compact-button" (click)="openSubmit(task)">
                      Ver detalle <span class="sr-only">{{ task.title }}</span>
                    </button>
                  }
                </div>
              </article>
            } @empty {
              <section class="card empty-state sp-empty">
                <app-icon name="clipboard" />
                <h2>{{ emptyTitle() }}</h2>
                <p>
                  {{
                    list.length
                      ? 'Cambia el filtro para ver otras tareas.'
                      : 'Cuando tus docentes publiquen tareas aparecerán aquí con su fecha de entrega.'
                  }}
                </p>
              </section>
            }
          </section>
        } @else {
          <p class="loading-state card" role="status">Cargando tareas…</p>
        }
      } @else if (error()) {
        <p class="feedback error-feedback" role="alert">
          {{ error() }} <button class="text-button" (click)="load()">Reintentar</button>
        </p>
      } @else {
        <p class="loading-state card" role="status">Cargando…</p>
      }

      <dialog
        #submitDialog
        class="account-dialog wide-dialog"
        aria-labelledby="submit-title"
        (cancel)="onCancel($event)"
      >
        <div class="dialog-heading">
          <h2 id="submit-title">{{ selected()?.title }}</h2>
          <button
            class="icon-button"
            (click)="closeSubmit()"
            aria-label="Cerrar ventana"
            [disabled]="busy()"
          >
            <app-icon name="close" />
          </button>
        </div>
        <form class="access-form" (ngSubmit)="submitTask()">
          @if (selected(); as t) {
            <p class="sp-dialog-meta">
              <span class="sp-subject" [attr.data-tone]="tone(t.course)">{{ t.course }}</span>
              <span>{{ dueText(t) }}</span>
              @if (t.points !== null) {
                <span>· Vale {{ fmt(t.points) }} pts</span>
              }
            </p>
            @if (t.instructions) {
              <p class="assignment-text">{{ t.instructions }}</p>
            }
            @if (t.feedback) {
              <p class="sp-feedback"><strong>Comentario del docente:</strong> {{ t.feedback }}</p>
            }
            @if (t.canSubmit) {
              <label class="form-field"
                ><span>Tu entrega</span
                ><textarea
                  name="submission"
                  [(ngModel)]="submission"
                  required
                  maxlength="8000"
                  rows="7"
                ></textarea>
              </label>
              @if (t.submissionStatus) {
                <fieldset class="choice-group">
                  <legend>Archivos adjuntos</legend>
                  @if (filesLoading()) {
                    <p class="muted-copy" role="status">Consultando archivos…</p>
                  } @else if (filesError()) {
                    <p class="feedback error-feedback" role="alert">
                      {{ filesError() }}
                      <button type="button" class="text-button" (click)="loadFiles(t.id)">
                        Reintentar archivos
                      </button>
                    </p>
                  } @else {
                    @for (file of files(); track file.id) {
                      <div class="file-row">
                        <app-icon name="receipt" />
                        <a [href]="'/api/v1/files/' + file.id" download>{{ file.fileName }}</a>
                        <small>{{ api.size(file.size) }}</small>
                        <button
                          type="button"
                          class="text-button danger-text"
                          (click)="removeFile(file.id)"
                          [disabled]="busy()"
                        >
                          Quitar <span class="sr-only">{{ file.fileName }}</span>
                        </button>
                      </div>
                    } @empty {
                      <p class="muted-copy">
                        Sin archivos. Puedes adjuntar PDF, imágenes, documentos de Office o texto
                        (máx. 10 MB).
                      </p>
                    }
                  }
                  <label class="file-picker"
                    ><span class="button outline compact-button">Adjuntar archivo</span
                    ><input
                      type="file"
                      accept=".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.pptx,.txt"
                      (change)="uploadFile($event)"
                      [disabled]="busy()"
                  /></label>
                </fieldset>
              } @else {
                <p class="muted-copy">Después de enviar el texto podrás adjuntar archivos.</p>
              }
            } @else if (t.text) {
              <h3 class="subheading">Tu entrega</h3>
              <p class="assignment-text">{{ t.text }}</p>
            } @else if (!t.submissionStatus) {
              <p class="muted-copy">
                {{
                  view()?.isSelf
                    ? 'Esta tarea ya no recibe entregas.'
                    : 'Solo el estudiante puede entregar desde su cuenta.'
                }}
              </p>
            }
          }
          @if (submitError()) {
            <p class="feedback error-feedback" role="alert">{{ submitError() }}</p>
          }
          <div class="dialog-actions">
            <button
              class="button outline"
              type="button"
              (click)="closeSubmit()"
              [disabled]="busy()"
            >
              Cerrar
            </button>
            @if (selected()?.canSubmit) {
              <button class="button" type="submit" [disabled]="busy()">
                {{
                  busy()
                    ? 'Enviando…'
                    : selected()?.submissionStatus
                      ? 'Actualizar texto'
                      : 'Enviar entrega'
                }}
              </button>
            }
          </div>
        </form>
      </dialog>
    </app-shell>
  `,
})
export class MyStudentTasks implements PendingChanges {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  private readonly school = inject(InstitutionService).current;
  @ViewChild('submitDialog') private dialog?: ElementRef<HTMLDialogElement>;
  readonly view = signal<StudentAccessView | null>(null);
  readonly tasks = signal<StudentTask[] | null>(null);
  readonly grades = signal<MyGrades | null>(null);
  readonly notFound = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly filter = signal<Filter>('pending');
  readonly course = signal('');
  readonly mode = signal<'list' | 'calendar'>('list');
  readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly selectedDay = signal<string | null>(isoDate(new Date()));
  readonly selected = signal<StudentTask | null>(null);
  readonly files = signal<{ id: string; fileName: string; size: number }[]>([]);
  readonly filesLoading = signal(false);
  readonly filesError = signal('');
  readonly busy = signal(false);
  readonly submitError = signal('');
  readonly today = isoDate(new Date());
  readonly weekdays = weekdayShort;
  readonly filters: { value: Filter; label: string }[] = [
    { value: 'pending', label: 'Por entregar' },
    { value: 'submitted', label: 'Entregadas' },
    { value: 'reviewed', label: 'Revisadas' },
    { value: 'overdue', label: 'Vencidas' },
    { value: 'all', label: 'Todas' },
  ];
  submission = '';
  private submissionBaseline = '';
  private loadRequest = 0;
  private fileRequest = 0;
  private loadedFor = '';

  readonly courses = computed(() =>
    [...new Set((this.tasks() ?? []).map((t) => t.course))].sort((a, b) =>
      a.localeCompare(b, 'es'),
    ),
  );
  readonly counts = computed(() => {
    const list = this.tasks() ?? [];
    return {
      pending: list.filter((t) => this.kind(t) === 'pending').length,
      overdue: list.filter((t) => this.kind(t) === 'overdue').length,
      submitted: list.filter((t) => t.submissionStatus === 'Submitted').length,
      reviewed: list.filter((t) => t.submissionStatus === 'Reviewed').length,
    };
  });
  /** Earned points by homework, only from published and cleared periods (grades endpoint). */
  readonly earnedById = computed(() => {
    const map = new Map<string, string>();
    for (const period of this.grades()?.periods ?? [])
      for (const grade of period.grades)
        for (const a of grade.activities ?? [])
          if (a.assignmentId && a.status)
            map.set(
              a.assignmentId,
              a.status === 'Scored'
                ? `${points(a.points)} de ${points(a.maxPoints)} pts`
                : a.status === 'Missing'
                  ? `0 de ${points(a.maxPoints)} pts`
                  : 'exonerada',
            );
    return map;
  });
  readonly visible = computed(() => {
    let list = (this.tasks() ?? []).filter((t) => !this.course() || t.course === this.course());
    if (this.mode() === 'calendar') {
      const day = this.selectedDay();
      return list.filter((t) => isoDate(new Date(t.dueAt)) === day);
    }
    const f = this.filter();
    if (f === 'pending') list = list.filter((t) => this.kind(t) === 'pending');
    else if (f === 'overdue') list = list.filter((t) => this.kind(t) === 'overdue');
    else if (f === 'submitted') list = list.filter((t) => t.submissionStatus === 'Submitted');
    else if (f === 'reviewed') list = list.filter((t) => t.submissionStatus === 'Reviewed');
    const pendingFirst = f === 'pending' || f === 'overdue';
    return [...list].sort(
      (a, b) =>
        (pendingFirst ? 1 : -1) * (new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime()),
    );
  });
  readonly weeks = computed(() => monthWeeks(this.month().getFullYear(), this.month().getMonth()));
  readonly byDay = computed(() => {
    const map = new Map<string, StudentTask[]>();
    for (const t of this.tasks() ?? []) {
      if (this.course() && t.course !== this.course()) continue;
      const key = isoDate(new Date(t.dueAt));
      map.set(key, [...(map.get(key) ?? []), t]);
    }
    return map;
  });
  readonly monthLabel = computed(() =>
    capitalize(
      new Intl.DateTimeFormat(this.school.locale.culture, {
        month: 'long',
        year: 'numeric',
      }).format(this.month()),
    ),
  );

  constructor() {
    effect(() => {
      this.id();
      untracked(() => void this.load());
    });
  }

  async load(): Promise<void> {
    const request = ++this.loadRequest;
    const id = this.id();
    this.fileRequest++;
    this.dialog?.nativeElement.close();
    this.selected.set(null);
    this.view.set(null);
    this.tasks.set(null);
    this.grades.set(null);
    this.notice.set('');
    if (this.loadedFor !== id) {
      this.loadedFor = id;
      this.course.set('');
      this.filter.set('pending');
    }
    this.error.set('');
    this.notFound.set(false);
    try {
      const view = await this.api.get<StudentAccessView>(
        `/api/v1/me/students/${encodeURIComponent(id)}`,
      );
      if (request !== this.loadRequest) return;
      this.view.set(view);
      if (!view.isSelf && !view.faculties.includes('AcademicRecords')) return;
      const [tasks, grades] = await Promise.all([
        this.api.get<StudentTask[]>(`/api/v1/me/students/${view.student.id}/assignments`),
        this.api.get<MyGrades>(`/api/v1/me/students/${view.student.id}/grades`).catch(() => null),
      ]);
      if (request !== this.loadRequest) return;
      this.tasks.set(tasks);
      this.grades.set(grades);
      if (!tasks.some((t) => this.kind(t) === 'pending') && tasks.length) this.filter.set('all');
    } catch (error) {
      if (request !== this.loadRequest) return;
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400))
        this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }

  kind(task: StudentTask): 'pending' | 'overdue' | 'done' {
    if (task.submissionStatus) return 'done';
    if (task.closed) return 'overdue';
    return new Date(task.dueAt).getTime() < Date.now() && !task.allowLate
      ? 'overdue'
      : new Date(task.dueAt).getTime() < Date.now()
        ? 'overdue'
        : 'pending';
  }
  state(task: StudentTask): string {
    if (task.submissionStatus === 'Reviewed') return 'reviewed';
    if (task.submissionStatus) return 'submitted';
    return this.kind(task) === 'overdue' ? (task.canSubmit ? 'late' : 'overdue') : 'pending';
  }
  stateLabel(task: StudentTask): string {
    return {
      reviewed: 'Revisada',
      submitted: task.late ? 'Entregada tarde' : 'Entregada',
      late: 'Vencida · acepta tardías',
      overdue: 'Vencida',
      pending: 'Por entregar',
    }[this.state(task)]!;
  }
  urgent(task: StudentTask): boolean {
    const hours = (new Date(task.dueAt).getTime() - Date.now()) / 3600000;
    return !task.submissionStatus && hours <= 48;
  }
  dueText(task: StudentTask): string {
    const due = new Date(task.dueAt);
    const time = new Intl.DateTimeFormat(this.school.locale.culture, {
      hour: '2-digit',
      minute: '2-digit',
    }).format(due);
    const days = Math.round(
      (new Date(isoDate(due)).getTime() - new Date(this.today).getTime()) / 86400000,
    );
    if (days === 0) return `Vence hoy, ${time}`;
    if (days === 1) return `Vence mañana, ${time}`;
    if (days > 1 && days <= 7) return `Vence en ${days} días · ${this.api.date(task.dueAt)}`;
    if (days === -1) return `Venció ayer, ${time}`;
    if (days < -1 && days >= -14) return `Venció hace ${-days} días`;
    return `Entrega ${this.api.date(task.dueAt)}`;
  }
  earned(task: StudentTask): string | null {
    return this.earnedById().get(task.id) ?? null;
  }
  emptyTitle(): string {
    return {
      pending: '¡Todo al día!',
      overdue: 'Sin tareas vencidas',
      submitted: 'Sin entregas por revisar',
      reviewed: 'Aún no hay tareas revisadas',
      all: 'Sin tareas',
    }[this.filter()];
  }
  tone(course: string): number {
    return subjectTone(course);
  }
  fmt(value: number | null): string {
    return points(value);
  }
  longDate(value: string): string {
    return capitalize(
      new Intl.DateTimeFormat(this.school.locale.culture, {
        weekday: 'long',
        day: 'numeric',
        month: 'long',
      }).format(new Date(value + 'T12:00:00')),
    );
  }
  shiftMonth(step: number): void {
    const m = this.month();
    this.month.set(new Date(m.getFullYear(), m.getMonth() + step, 1));
  }

  openSubmit(task: StudentTask): void {
    this.selected.set(task);
    this.submission = task.text ?? '';
    this.submissionBaseline = this.submission;
    this.submitError.set('');
    this.files.set([]);
    this.filesError.set('');
    if (task.submissionStatus && task.canSubmit) void this.loadFiles(task.id);
    this.dialog?.nativeElement.showModal();
  }
  async loadFiles(taskId: string): Promise<void> {
    const request = ++this.fileRequest;
    this.files.set([]);
    this.filesLoading.set(true);
    this.filesError.set('');
    try {
      const files = await this.api.get<{ id: string; fileName: string; size: number }[]>(
        `/api/v1/me/assignments/${taskId}/files`,
      );
      if (request === this.fileRequest && this.selected()?.id === taskId) this.files.set(files);
    } catch (error) {
      if (request === this.fileRequest && this.selected()?.id === taskId)
        this.filesError.set(this.api.message(error));
    } finally {
      if (request === this.fileRequest) this.filesLoading.set(false);
    }
  }
  async closeSubmit(): Promise<void> {
    if (this.busy() || !(await this.confirmDraft())) return;
    this.fileRequest++;
    this.selected.set(null);
    this.dialog?.nativeElement.close();
  }
  onCancel(event: Event): void {
    event.preventDefault();
    void this.closeSubmit();
  }
  private async confirmDraft(): Promise<boolean> {
    return (
      !this.selected()?.canSubmit ||
      this.submission === this.submissionBaseline ||
      this.alerts.confirm({
        title: 'Hay una entrega sin guardar',
        text: '¿Quieres salir y descartar el texto que escribiste?',
        confirmText: 'Descartar texto',
        danger: true,
      })
    );
  }
  async canLeave(): Promise<boolean> {
    return !this.busy() && (await this.confirmDraft());
  }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent): void {
    if (
      this.busy() ||
      (this.selected()?.canSubmit && this.submission !== this.submissionBaseline)
    ) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  async submitTask(): Promise<void> {
    const task = this.selected();
    const view = this.view();
    if (!task || !view || this.busy()) return;
    this.busy.set(true);
    this.submitError.set('');
    try {
      await this.api.post(`/api/v1/me/assignments/${task.id}/submission`, {
        text: this.submission,
        version: task.version,
      });
      this.submissionBaseline = this.submission;
      this.tasks.set(
        await this.api.get<StudentTask[]>(`/api/v1/me/students/${view.student.id}/assignments`),
      );
      const updated = this.tasks()?.find((x) => x.id === task.id) ?? null;
      this.selected.set(updated);
      this.notice.set(`Tu entrega de «${task.title}» quedó registrada.`);
      // The homework is no longer pending: show it where it now belongs.
      if (this.filter() === 'pending' || this.filter() === 'overdue') this.filter.set('submitted');
      if (updated?.submissionStatus && !task.submissionStatus) void this.loadFiles(updated.id);
      else {
        this.busy.set(false);
        this.dialog?.nativeElement.close();
      }
    } catch (error) {
      this.submitError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  async uploadFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const task = this.selected();
    if (!file || !task || this.busy()) return;
    this.busy.set(true);
    this.submitError.set('');
    try {
      const data = new FormData();
      data.append('file', file);
      await this.api.upload(`/api/v1/me/assignments/${task.id}/files`, data);
      await this.loadFiles(task.id);
    } catch (error) {
      this.submitError.set(this.api.message(error));
    } finally {
      input.value = '';
      this.busy.set(false);
    }
  }
  async removeFile(fileId: string): Promise<void> {
    const task = this.selected();
    if (!task || this.busy()) return;
    if (
      !(await this.alerts.confirm({
        title: '¿Quitar este archivo de tu entrega?',
        confirmText: 'Quitar archivo',
        danger: true,
      }))
    )
      return;
    this.busy.set(true);
    try {
      await this.api.post(`/api/v1/me/assignments/${task.id}/files/${fileId}/remove`, {});
      await this.loadFiles(task.id);
    } catch (error) {
      this.submitError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
