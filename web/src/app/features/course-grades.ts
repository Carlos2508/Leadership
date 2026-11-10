import { Component, ElementRef, OnDestroy, ViewChild, ViewEncapsulation, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { combineLatest, distinctUntilChanged, map } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { UpperCasePipe } from '@angular/common';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { points as fmtPoints } from '../core/student-api';
import { CourseView } from './courses';
import { Shell } from '../shared/shell';
import { CourseNav } from '../shared/course-nav';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';

type ScoreState = 'Scored' | 'Missing' | 'Excused';
interface SheetPeriod { id: string; sequence: number; name: string; startsOn: string; endsOn: string; published: boolean }
interface SheetRow { studentId: string; name: string; code: string; score: number | null; status: ScoreState | null; version: string | null }
interface GradeSheet { course: CourseView; period: SheetPeriod; periods: SheetPeriod[]; canEdit: boolean; requiresReason: boolean; rows: SheetRow[] }
interface EditableRow extends SheetRow { draftText: string; draftStatus: ScoreState | '' }
interface SaveResult { changed: number; rows: { studentId: string; score: number | null; status: ScoreState; version: string }[] }
interface Activity { id: string; title: string; maxPoints: number; sequence: number; assignmentId: string | null; scored: number; version: string }
interface Cell { points: number | null; status: ScoreState; version: string }
interface BookRow { studentId: string; name: string; code: string; scores: Record<string, Cell>; total: number | null; totalStatus: ScoreState | null }
interface Template { id: string; name: string; items: { title: string; points: number }[]; total: number }
interface Book {
  course: CourseView; period: SheetPeriod; periods: SheetPeriod[]; canEdit: boolean; requiresReason: boolean; totalPoints: number;
  activities: Activity[]; rows: BookRow[]; templates: Template[];
}

/** Parses a typed score: number, null when empty, undefined when invalid (accepts a decimal comma). */
export function parseScore(text: string, max = 100): number | null | undefined {
  const value = text.trim().replace(',', '.');
  if (value === '') return null;
  if (!/^\d{1,3}(\.\d{1,2})?$/.test(value)) return undefined;
  const score = Number(value);
  return score >= 0 && score <= max ? score : undefined;
}

/** A typed activity cell: points, «N»/«P» (no entregó = 0) or «E» (exonerado); null when empty, undefined when invalid. */
export function parseCell(text: string, max: number): { status: ScoreState; points: number | null } | null | undefined {
  const value = text.trim().toUpperCase();
  if (value === '') return null;
  if (value === 'N' || value === 'P' || value === 'NE') return { status: 'Missing', points: null };
  if (value === 'E' || value === 'EX') return { status: 'Excused', points: null };
  const points = parseScore(value, max);
  return points === undefined || points === null ? undefined : { status: 'Scored', points };
}

function cellText(cell: Cell | undefined): string {
  if (!cell) return '';
  if (cell.status === 'Missing') return 'N';
  if (cell.status === 'Excused') return 'E';
  return cell.points === null ? '' : String(cell.points);
}

/**
 * Course gradebook. With activities (as in the school's previous system) the period result is the sum of the points of each
 * activity: the teacher types points in a grid (Enter moves down, arrows move across, a pasted column fills students, quiet
 * autosave). Without activities, the period result is typed directly (0–100). After publication only coordination corrects,
 * always with a reason and never autosaved.
 */
@Component({
  imports: [Shell, Icon, CourseNav, FormsModule, RouterLink, CyclePipe, UpperCasePipe],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../styles/_gradebook.scss', '../../styles/_activity-templates.scss'],
  template: `
    <app-shell [title]="course()?.subject ?? 'Libreta'">
      <app-course-nav [courseId]="id()" active="grades" />
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      @if (loading()) {
        <p class="loading-state card" role="status">Cargando libreta…</p>
      } @else if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a esta libreta</h2>
          <p>Solo el personal académico y los docentes asignados pueden verla.</p>
        </section>
      } @else if (book(); as b) {
        <div class="page-heading">
          <div>
            <span class="eyebrow">LIBRETA · {{ b.course.year | cycle | uppercase }}</span>
            <h1>{{ b.course.subject }}</h1>
            <p>{{ b.course.grade }} · {{ b.course.section ? 'Sección ' + b.course.section : 'Todo el grado' }}</p>
          </div>
          <a class="button outline" [href]="'/api/v1/courses/' + b.course.id + '/grades.pdf?periodId=' + b.period.id" target="_blank" rel="noopener"
            ><app-icon name="download" />Libreta en PDF</a
          >
        </div>
        <nav class="period-tabs" aria-label="Períodos del ciclo">
          @for (period of b.periods; track period.id) {
            <a
              [routerLink]="[]"
              [queryParams]="{ period: period.id }"
              [class.active]="period.id === b.period.id"
              [attr.aria-current]="period.id === b.period.id ? 'page' : null"
              >{{ period.name }}
              @if (period.published) {
                <span class="pill mint">Publicado</span>
              }</a
            >
          }
        </nav>

        @if (b.activities.length) {
          <!-- Activities mode: the period result is the sum of points. -->
          <section class="card gb-summary">
            <div class="gb-summary-copy">
              <strong>{{ b.activities.length }} {{ b.activities.length === 1 ? 'actividad' : 'actividades' }} · {{ fmt(b.totalPoints) }} de 100 pts</strong>
              <small>
                {{ date(b.period.startsOn) }} – {{ date(b.period.endsOn) }}
                @if (b.totalPoints < 100) {
                  · faltan {{ fmt(100 - b.totalPoints) }} pts por asignar
                }
                @if (b.period.published) {
                  · Publicado: {{ b.canEdit ? 'los cambios se registran como corrección con motivo.' : 'solo coordinación puede corregir.' }}
                }
              </small>
              <span class="gb-meter" aria-hidden="true"><span [style.width.%]="b.totalPoints"></span></span>
            </div>
            @if (b.canEdit) {
              <div class="gb-summary-actions">
                @if (b.templates.length) {
                  <button class="button outline compact-button" type="button" (click)="openTemplate()"><app-icon name="folder" />Usar plantilla</button>
                }
                <button class="button compact-button" type="button" (click)="openActivity()"><app-icon name="plus" />Agregar actividad</button>
              </div>
            }
          </section>
          @if (cells().length) {
            @if (b.canEdit) {
              <p class="sheet-hint" id="book-hint">
                <app-icon name="info" />
                <span
                  >Escribe los puntos de cada actividad. <kbd>Enter</kbd> baja al siguiente estudiante y las flechas cambian de columna.
                  <strong>N</strong> = no entregó (0 pts), <strong>E</strong> = exonerado. Puedes pegar una columna desde Excel.{{
                    b.requiresReason ? '' : ' Se guarda solo.'
                  }}</span
                >
              </p>
            }
            <form (ngSubmit)="saveBook(false)">
              <div class="table-scroll gb-scroll" tabindex="0" role="region" aria-label="Punteos por actividad">
                <table class="gb-table">
                  <caption class="sr-only">Punteo de cada estudiante por actividad y total del período</caption>
                  <thead>
                    <tr>
                      <th scope="col" class="gb-student">Estudiante</th>
                      @for (a of b.activities; track a.id) {
                        <th scope="col" class="gb-activity">
                          <button type="button" class="gb-activity-head" (click)="openActivity(a)" [disabled]="!b.canEdit" [title]="a.title">
                            <span class="gb-activity-title">{{ a.title }}</span>
                            <small>{{ fmt(a.maxPoints) }} pts@if (a.assignmentId) { · Tarea }</small>
                          </button>
                        </th>
                      }
                      <th scope="col" class="gb-total">Total<small>/ 100</small></th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of cells(); track row.studentId; let r = $index) {
                      <tr>
                        <th scope="row" class="gb-student">
                          <span class="person-copy"><strong>{{ row.name }}</strong><small>{{ row.code }}</small></span>
                        </th>
                        @for (a of b.activities; track a.id; let c = $index) {
                          <td [class.gb-invalid]="invalidCell(row.draft[a.id], a)" [class.gb-changed]="cellChanged(row, a)">
                            <input
                              class="gb-input"
                              type="text"
                              inputmode="decimal"
                              autocomplete="off"
                              [attr.data-r]="r"
                              [attr.data-c]="c"
                              [attr.aria-label]="'Punteo de ' + row.name + ' en ' + a.title"
                              [attr.aria-invalid]="invalidCell(row.draft[a.id], a) ? 'true' : null"
                              [attr.aria-describedby]="b.canEdit ? 'book-hint' : null"
                              [name]="'c-' + row.studentId + '-' + a.id"
                              [(ngModel)]="row.draft[a.id]"
                              (ngModelChange)="touchBook()"
                              (keydown)="moveCell($event, r, c)"
                              (paste)="pasteCell($event, r, a)"
                              (focus)="selectText($event)"
                              [disabled]="!b.canEdit"
                              [placeholder]="'—'"
                            />
                          </td>
                        }
                        <td class="gb-total" [attr.data-level]="level(total(row))">
                          <strong>{{ total(row) === null ? '—' : fmt(total(row)) }}</strong>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              @if (b.canEdit) {
                <div class="sheet-actions gb-actions">
                  <p class="save-state" role="status" aria-live="polite">
                    @if (saveState()) {
                      <app-icon [name]="saveIcon()" />{{ saveState() }}
                    }
                  </p>
                  @if (b.requiresReason) {
                    <label class="form-field"
                      ><span>Motivo de la corrección</span
                      ><input name="reason" [(ngModel)]="reason" required minlength="8" maxlength="500" autocomplete="off"
                    /></label>
                  }
                  <button class="button" type="submit" [disabled]="manualSaving() || !bookDirty()">
                    {{ manualSaving() ? 'Guardando…' : 'Guardar punteos' }}
                  </button>
                </div>
              }
            </form>
          } @else {
            <p class="empty-inline padded card">No hubo estudiantes en este curso durante el período.</p>
          }
        } @else if (sheet(); as s) {
          <!-- No activities: direct period result (0–100). -->
          @if (b.canEdit) {
            <section class="card gb-intro">
              <span class="gb-intro-icon"><app-icon name="clipboard" /></span>
              <div>
                <h2>Califica por actividades</h2>
                <p>
                  Define las actividades del período con su punteo (tareas, laboratorios, exámenes…). La nota del período será la suma de
                  los puntos, como en el sistema anterior. Las tareas con punteo aparecen aquí automáticamente.
                </p>
                <div class="gb-intro-actions">
                  @if (b.templates.length) {
                    <button class="button compact-button" type="button" (click)="openTemplate()"><app-icon name="folder" />Usar plantilla del colegio</button>
                  }
                  <button class="button outline compact-button" type="button" (click)="openActivity()"><app-icon name="plus" />Agregar actividad</button>
                </div>
              </div>
            </section>
          }
          <section class="card directory-card">
            <p class="directory-summary period-summary">
              Nota directa del período · {{ date(s.period.startsOn) }} – {{ date(s.period.endsOn) }} · {{ recorded() }} de {{ rows().length }} registradas
              @if (s.period.published) {
                · Publicado: {{ s.canEdit ? 'los cambios se registran como corrección con motivo.' : 'solo coordinación puede corregir.' }}
              }
            </p>
            @if (rows().length) {
              @if (s.canEdit) {
                <p class="sheet-hint" id="sheet-hint">
                  <app-icon name="info" />
                  <span
                    >Escribe la nota y pulsa <kbd>Enter</kbd> para pasar al siguiente estudiante. Puedes pegar una columna copiada de Excel
                    (E = exonerado, P = pendiente).{{ s.requiresReason ? '' : ' Los cambios se guardan solos.' }}</span
                  >
                </p>
              }
              <form (ngSubmit)="save(false)">
                <div class="table-scroll" tabindex="0" role="region" aria-label="Notas del período">
                  <table class="stack-table grade-table">
                    <caption class="sr-only">Nota del período por estudiante</caption>
                    <thead>
                      <tr>
                        <th scope="col">Estudiante</th>
                        <th scope="col">Situación</th>
                        <th scope="col">Nota (0–100)</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of rows(); track row.studentId; let i = $index) {
                        <tr [class.row-invalid]="invalid(row)" [class.row-pending]="changed(row)">
                          <td>
                            <span class="person-copy"><strong>{{ row.name }}</strong><small>{{ row.code }}</small></span>
                          </td>
                          <td data-label="Situación">
                            <label
                              ><span class="sr-only">Situación de {{ row.name }}</span
                              ><select
                                class="inline-select"
                                [name]="'status-' + row.studentId"
                                [(ngModel)]="row.draftStatus"
                                (ngModelChange)="statusChanged(row)"
                                [disabled]="!s.canEdit"
                              >
                                <option value="">Sin registrar</option>
                                <option value="Scored">Con nota</option>
                                <option value="Missing">Pendiente de entregar</option>
                                <option value="Excused">Exonerado</option>
                              </select></label
                            >
                          </td>
                          <td data-label="Nota">
                            <label
                              ><span class="sr-only">Nota de {{ row.name }}</span
                              ><input
                                class="score-input"
                                type="text"
                                inputmode="decimal"
                                autocomplete="off"
                                enterkeyhint="next"
                                [attr.data-row]="i"
                                [attr.aria-invalid]="invalid(row) ? 'true' : null"
                                [attr.aria-describedby]="s.canEdit ? 'sheet-hint' : null"
                                [name]="'score-' + row.studentId"
                                [(ngModel)]="row.draftText"
                                (ngModelChange)="scoreChanged(row)"
                                (keydown)="move($event, i)"
                                (paste)="paste($event, i)"
                                (focus)="selectText($event)"
                                [disabled]="!s.canEdit || (row.draftStatus !== 'Scored' && row.draftStatus !== '')"
                            /></label>
                            @if (invalid(row)) {
                              <small class="field-error">Entre 0 y 100, hasta dos decimales.</small>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                @if (s.canEdit) {
                  <div class="sheet-actions">
                    <p class="save-state" role="status" aria-live="polite">{{ saveState() }}</p>
                    @if (s.requiresReason) {
                      <label class="form-field"
                        ><span>Motivo de la corrección</span
                        ><input name="reason" [(ngModel)]="reason" required minlength="8" maxlength="500" autocomplete="off"
                      /></label>
                    }
                    <button class="button" type="submit" [disabled]="manualSaving() || !dirty()">
                      {{ manualSaving() ? 'Guardando…' : 'Guardar notas' }}
                    </button>
                  </div>
                }
              </form>
            } @else {
              <p class="empty-inline padded">No hubo estudiantes en este curso durante el período.</p>
            }
          </section>
        }
      } @else {
        <button class="button outline" (click)="retryLoad()">Reintentar libreta</button>
      }

      <dialog #activityDialog class="account-dialog" aria-labelledby="activity-title" (cancel)="onDialogCancel($event)">
        <div class="dialog-heading">
          <h2 id="activity-title">{{ editing() ? 'Editar actividad' : 'Nueva actividad' }}</h2>
          <button class="icon-button" (click)="closeDialog(activityDialog)" aria-label="Cerrar ventana" [disabled]="dialogBusy()"><app-icon name="close" /></button>
        </div>
        <form class="access-form" (ngSubmit)="saveActivity()">
          @if (editing()?.assignmentId) {
            <p class="feedback info-feedback">Esta actividad es una tarea publicada. Cambia su nombre o punteo desde «Actividades».</p>
          }
          <label class="form-field"
            ><span>Nombre de la actividad</span
            ><input name="activityTitle" [(ngModel)]="activityForm.title" required maxlength="120" autocomplete="off" placeholder="Ej.: Laboratorio 1" [disabled]="!!editing()?.assignmentId"
          /></label>
          <label class="form-field"
            ><span>Punteo (puntos que vale)</span
            ><input name="activityPoints" type="number" min="0.01" max="100" step="0.01" [(ngModel)]="activityForm.points" required [disabled]="!!editing()?.assignmentId"
          /></label>
          <p class="muted-copy">Disponibles en el período: {{ fmt(available()) }} pts de 100.</p>
          @if (book()?.requiresReason || (editing()?.scored ?? 0) > 0) {
            <label class="form-field"
              ><span>Motivo {{ editing() ? '(obligatorio para quitar una actividad con punteos)' : 'de la corrección' }}</span
              ><input name="activityReason" [(ngModel)]="activityForm.reason" maxlength="500" autocomplete="off"
            /></label>
          }
          @if (dialogError()) {
            <p class="feedback error-feedback" role="alert">{{ dialogError() }}</p>
          }
          <div class="dialog-actions">
            @if (editing() && !editing()!.assignmentId) {
              <button class="button outline danger-button" type="button" (click)="removeActivity()" [disabled]="dialogBusy()">Quitar actividad</button>
            }
            <button class="button outline" type="button" (click)="closeDialog(activityDialog)" [disabled]="dialogBusy()">Cancelar</button>
            @if (!editing()?.assignmentId) {
              <button class="button" type="submit" [disabled]="dialogBusy()">{{ dialogBusy() ? 'Guardando…' : 'Guardar actividad' }}</button>
            }
          </div>
        </form>
      </dialog>

      <dialog #templateDialog class="account-dialog" aria-labelledby="template-title" (cancel)="onDialogCancel($event)">
        <div class="dialog-heading">
          <h2 id="template-title">Usar plantilla de actividades</h2>
          <button class="icon-button" (click)="closeDialog(templateDialog)" aria-label="Cerrar ventana" [disabled]="dialogBusy()"><app-icon name="close" /></button>
        </div>
        <form class="access-form" (ngSubmit)="applyTemplate()">
          <label class="form-field"
            ><span>Plantilla</span
            ><select name="template" [(ngModel)]="templateId" required>
              @for (t of book()?.templates ?? []; track t.id) {
                <option [value]="t.id">{{ t.name }} · {{ fmt(t.total) }} pts</option>
              }
            </select></label
          >
          @if (chosenTemplate(); as t) {
            <ul class="gb-template-items">
              @for (item of t.items; track $index) {
                <li><span>{{ item.title }}</span><strong>{{ fmt(item.points) }} pts</strong></li>
              }
            </ul>
          }
          <p class="muted-copy">Se agregan las actividades que aún no existen en este período. Después puedes ajustarlas.</p>
          @if (dialogError()) {
            <p class="feedback error-feedback" role="alert">{{ dialogError() }}</p>
          }
          <div class="dialog-actions">
            <button class="button outline" type="button" (click)="closeDialog(templateDialog)" [disabled]="dialogBusy()">Cancelar</button>
            <button class="button" type="submit" [disabled]="dialogBusy() || !templateId">{{ dialogBusy() ? 'Aplicando…' : 'Aplicar plantilla' }}</button>
          </div>
        </form>
      </dialog>
    </app-shell>
  `,
})
export class CourseGrades implements OnDestroy {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly school = inject(InstitutionService).current;
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly route = inject(ActivatedRoute);
  @ViewChild('activityDialog') private activityDialog?: ElementRef<HTMLDialogElement>;
  @ViewChild('templateDialog') private templateDialog?: ElementRef<HTMLDialogElement>;
  readonly book = signal<Book | null>(null);
  readonly cells = signal<(BookRow & { draft: Record<string, string> })[]>([]);
  readonly sheet = signal<GradeSheet | null>(null);
  readonly rows = signal<EditableRow[]>([]);
  readonly notFound = signal(false);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly manualSaving = signal(false);
  readonly error = signal('');
  readonly saveState = signal('');
  readonly saveIcon = signal('check');
  readonly editing = signal<Activity | null>(null);
  readonly dialogBusy = signal(false);
  readonly dialogError = signal('');
  readonly course = computed(() => this.book()?.course ?? null);
  readonly recorded = computed(() => this.rows().filter((x) => x.status).length);
  readonly available = computed(() => 100 - (this.book()?.totalPoints ?? 0) + (this.editing()?.maxPoints ?? 0));
  readonly chosenTemplate = computed(() => this.book()?.templates.find((t) => t.id === this.templateSignal()) ?? null);
  private readonly templateSignal = signal('');
  activityForm = { title: '', points: 10 as number | null, reason: '' };
  reason = '';
  private timer: ReturnType<typeof setTimeout> | undefined;
  private queued = false;
  private saving: Promise<void> | null = null;
  private leaving: Promise<boolean> | null = null;
  private loadRequest = 0;

  get templateId(): string {
    return this.templateSignal();
  }
  set templateId(value: string) {
    this.templateSignal.set(value);
  }

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(
        map(([params, query]) => [params.get('id')!, query.get('period') ?? undefined] as const),
        distinctUntilChanged((a, b) => a[0] === b[0] && a[1] === b[1]),
        takeUntilDestroyed(),
      )
      .subscribe(([id, period]) => void this.load(period, id));
  }
  ngOnDestroy(): void {
    clearTimeout(this.timer);
    this.loadRequest++;
  }
  canLeave(): Promise<boolean> {
    if (this.leaving) return this.leaving;
    this.leaving = this.prepareToLeave().finally(() => {
      this.leaving = null;
    });
    return this.leaving;
  }
  private async prepareToLeave(): Promise<boolean> {
    clearTimeout(this.timer);
    if (this.saving) await this.saving;
    if (this.autosaves() && this.anyDirty()) await this.persist(true);
    clearTimeout(this.timer);
    if (!this.anyDirty()) return true;
    return this.alerts.confirm({
      title: 'Hay notas sin guardar',
      text: 'Los cambios siguen en esta libreta. Puedes seguir editando y guardarlos antes de salir.',
      confirmText: 'Salir sin guardar',
      cancelText: 'Seguir editando',
      danger: true,
    });
  }
  date(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  fmt(value: number | null): string {
    return fmtPoints(value);
  }
  selectText(event: FocusEvent): void {
    (event.target as HTMLInputElement).select();
  }
  private activityMode(): boolean {
    return !!this.book()?.activities.length;
  }
  private anyDirty(): boolean {
    return this.activityMode() ? this.bookDirty() : this.dirty();
  }
  private autosaves(): boolean {
    const b = this.book();
    return !!b && b.canEdit && !b.requiresReason;
  }
  private persist(auto: boolean): Promise<void> {
    return this.activityMode() ? this.saveBook(auto) : this.save(auto);
  }

  // ---------------------------------------------------------------- activities grid

  invalidCell(text: string | undefined, a: Activity): boolean {
    return parseCell(text ?? '', a.maxPoints) === undefined;
  }
  cellChanged(row: BookRow & { draft: Record<string, string> }, a: Activity): boolean {
    const text = row.draft[a.id] ?? '';
    return text.trim().toUpperCase() !== cellText(row.scores[a.id]).toUpperCase() && text.trim() !== '';
  }
  bookDirty(): boolean {
    const b = this.book();
    return !!b && this.cells().some((row) => b.activities.some((a) => this.cellChanged(row, a)));
  }
  total(row: BookRow & { draft: Record<string, string> }): number | null {
    const b = this.book();
    if (!b) return null;
    let sum = 0;
    let any = false;
    for (const a of b.activities) {
      const parsed = parseCell(row.draft[a.id] ?? '', a.maxPoints);
      if (!parsed) continue;
      any = true;
      if (parsed.status === 'Scored') sum += parsed.points ?? 0;
    }
    return any ? Math.round(sum * 100) / 100 : null;
  }
  /** Colour of a total relative to the points assigned so far (not to 100: the period may still be in progress). */
  level(value: number | null): string {
    const assigned = this.book()?.totalPoints ?? 0;
    if (value === null || !assigned) return 'none';
    const share = value / assigned;
    return share >= 0.8 ? 'high' : share >= 0.6 ? 'mid' : 'low';
  }
  touchBook(): void {
    this.cells.set([...this.cells()]);
    if (!this.autosaves()) return;
    this.saveIcon.set('clock');
    this.saveState.set('Cambios sin guardar…');
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.saveBook(true), 1200);
  }
  /** Enter/↓ next student, Shift+Enter/↑ previous, ←/→ previous/next activity (at the edges of the text). */
  moveCell(event: KeyboardEvent, r: number, c: number): void {
    const input = event.target as HTMLInputElement;
    let target: [number, number] | null = null;
    if ((event.key === 'Enter' && !event.shiftKey) || event.key === 'ArrowDown') target = [r + 1, c];
    else if ((event.key === 'Enter' && event.shiftKey) || event.key === 'ArrowUp') target = [r - 1, c];
    else if (event.key === 'ArrowRight' && input.selectionStart === input.value.length) target = [r, c + 1];
    else if (event.key === 'ArrowLeft' && input.selectionEnd === 0) target = [r, c - 1];
    if (!target) return;
    event.preventDefault();
    const next = this.host.nativeElement.querySelector<HTMLInputElement>(`input.gb-input[data-r="${target[0]}"][data-c="${target[1]}"]`);
    if (next) next.focus();
    else if (event.key === 'Enter' && this.autosaves() && this.bookDirty()) void this.saveBook(true);
  }
  pasteCell(event: ClipboardEvent, r: number, a: Activity): void {
    const lines = (event.clipboardData?.getData('text/plain') ?? '').replace(/\r/g, '').split('\n');
    if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
    if (lines.length < 2) return;
    event.preventDefault();
    const rows = this.cells();
    let filled = 0;
    lines.forEach((line, offset) => {
      const row = rows[r + offset];
      const value = line.split('\t')[0].trim();
      if (!row || value === '') return;
      row.draft[a.id] = value;
      filled++;
    });
    this.touchBook();
    this.saveState.set(`Se pegaron ${filled} ${filled === 1 ? 'punteo' : 'punteos'} en «${a.title}».`);
  }
  saveBook(auto: boolean): Promise<void> {
    if (this.saving) {
      this.queued = true;
      return this.saving;
    }
    this.saving = this.saveBookChanges(auto).finally(() => {
      this.saving = null;
    });
    return this.saving;
  }
  private async saveBookChanges(auto: boolean): Promise<void> {
    const b = this.book();
    if (!b) return;
    clearTimeout(this.timer);
    const entries: { activityId: string; studentId: string; points: number | null; status: ScoreState; version: string | null; text: string }[] = [];
    const wrong: string[] = [];
    for (const row of this.cells())
      for (const a of b.activities) {
        if (!this.cellChanged(row, a)) continue;
        const text = row.draft[a.id] ?? '';
        const parsed = parseCell(text, a.maxPoints);
        if (!parsed) {
          wrong.push(`${row.name} (${a.title})`);
          continue;
        }
        entries.push({ activityId: a.id, studentId: row.studentId, points: parsed.points, status: parsed.status, version: row.scores[a.id]?.version ?? null, text });
      }
    if (wrong.length && !auto) {
      this.error.set(`Revisa ${wrong.slice(0, 3).join(', ')}${wrong.length > 3 ? ` y ${wrong.length - 3} más` : ''}: cada punteo va de 0 al valor de la actividad (o N/E).`);
      return;
    }
    if (!entries.length) {
      if (auto) this.saveState.set(wrong.length ? 'Corrige los punteos marcados para guardarlos.' : '');
      return;
    }
    if (b.requiresReason && this.reason.trim().length < 8) {
      this.error.set('Escribe el motivo de la corrección (al menos 8 caracteres).');
      return;
    }
    this.busy.set(true);
    if (!auto) this.manualSaving.set(true);
    this.error.set('');
    if (auto) {
      this.saveIcon.set('refresh');
      this.saveState.set('Guardando…');
    }
    try {
      const result = await this.api.post<{ changed: number }>(`/api/v1/courses/${b.course.id}/activities/scores`, {
        periodId: b.period.id,
        reason: this.reason || null,
        entries: entries.map(({ text: _text, ...entry }) => entry),
      });
      // Refresh versions without losing what was typed meanwhile.
      const fresh = await this.api.get<Book>(`/api/v1/courses/${b.course.id}/activities?periodId=${b.period.id}`);
      const sent = new Map(entries.map((e) => [e.studentId + e.activityId, e.text]));
      const current = new Map(this.cells().map((row) => [row.studentId, row.draft]));
      this.book.set(fresh);
      this.cells.set(
        fresh.rows.map((row) => {
          const old = current.get(row.studentId) ?? {};
          const draft: Record<string, string> = {};
          for (const a of fresh.activities) {
            const typed = old[a.id];
            const wasSent = sent.has(row.studentId + a.id) && sent.get(row.studentId + a.id) === typed;
            draft[a.id] = typed !== undefined && !wasSent && typed.trim().toUpperCase() !== cellText(row.scores[a.id]).toUpperCase() ? typed : cellText(row.scores[a.id]);
          }
          return { ...row, draft };
        }),
      );
      const time = new Intl.DateTimeFormat(this.school.locale.culture, { hour: '2-digit', minute: '2-digit' }).format(new Date());
      this.saveIcon.set('check');
      this.saveState.set(`Guardado a las ${time}`);
      if (!auto) {
        this.alerts.success(result.changed === 1 ? 'Se guardó el punteo de 1 estudiante.' : `Se guardaron punteos de ${result.changed} estudiantes.`);
        if (b.requiresReason) this.reason = '';
      }
    } catch (error) {
      this.saveState.set('');
      this.error.set(this.api.message(error) + (auto ? ' Los cambios siguen en pantalla; vuelve a intentarlo con «Guardar punteos».' : ''));
    } finally {
      this.busy.set(false);
      this.manualSaving.set(false);
    }
    if (this.queued) {
      this.queued = false;
      if (this.bookDirty()) this.touchBook();
    }
  }

  // ---------------------------------------------------------------- activity and template dialogs

  openActivity(activity?: Activity): void {
    this.editing.set(activity ?? null);
    this.activityForm = { title: activity?.title ?? '', points: activity?.maxPoints ?? Math.min(10, this.available()), reason: '' };
    this.dialogError.set('');
    this.activityDialog?.nativeElement.showModal();
  }
  openTemplate(): void {
    this.templateId = this.book()?.templates[0]?.id ?? '';
    this.dialogError.set('');
    this.templateDialog?.nativeElement.showModal();
  }
  closeDialog(dialog: HTMLDialogElement): void {
    if (!this.dialogBusy()) dialog.close();
  }
  onDialogCancel(event: Event): void {
    if (this.dialogBusy()) event.preventDefault();
  }
  async saveActivity(): Promise<void> {
    const b = this.book();
    if (!b || this.dialogBusy()) return;
    if (this.anyDirty()) await this.persist(true);
    this.dialogBusy.set(true);
    this.dialogError.set('');
    try {
      const editing = this.editing();
      const body = { periodId: b.period.id, title: this.activityForm.title, maxPoints: Number(this.activityForm.points), version: editing?.version ?? null, reason: this.activityForm.reason || null };
      await this.api.post(editing ? `/api/v1/courses/${b.course.id}/activities/${editing.id}` : `/api/v1/courses/${b.course.id}/activities`, body);
      this.dialogBusy.set(false);
      this.activityDialog?.nativeElement.close();
      this.alerts.success(editing ? 'La actividad se actualizó.' : 'La actividad se agregó a la libreta.');
      await this.load(b.period.id);
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.dialogBusy.set(false);
    }
  }
  async removeActivity(): Promise<void> {
    const b = this.book();
    const a = this.editing();
    if (!b || !a || this.dialogBusy()) return;
    if (a.scored > 0 && this.activityForm.reason.trim().length < 8) {
      this.dialogError.set('La actividad tiene punteos: escribe el motivo para quitarla (al menos 8 caracteres).');
      return;
    }
    const ok = await this.alerts.confirm({
      title: `¿Quitar «${a.title}»?`,
      text: a.scored ? 'Sus punteos dejan de contar en la nota del período (quedan en el historial).' : 'La actividad se quitará del período.',
      confirmText: 'Quitar actividad',
      danger: true,
    });
    if (!ok) return;
    this.dialogBusy.set(true);
    try {
      await this.api.post(`/api/v1/courses/${b.course.id}/activities/${a.id}/remove`, { version: a.version, reason: this.activityForm.reason || null });
      this.dialogBusy.set(false);
      this.activityDialog?.nativeElement.close();
      this.alerts.success('La actividad se quitó.');
      await this.load(b.period.id);
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.dialogBusy.set(false);
    }
  }
  async applyTemplate(): Promise<void> {
    const b = this.book();
    if (!b || !this.templateId || this.dialogBusy()) return;
    if (this.anyDirty()) await this.persist(true);
    this.dialogBusy.set(true);
    this.dialogError.set('');
    try {
      const result = await this.api.post<{ added: number }>(`/api/v1/courses/${b.course.id}/activities/template`, { periodId: b.period.id, templateId: this.templateId });
      this.dialogBusy.set(false);
      this.templateDialog?.nativeElement.close();
      this.alerts.success(result.added ? `Se agregaron ${result.added} actividades.` : 'Las actividades de la plantilla ya estaban en el período.');
      await this.load(b.period.id);
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.dialogBusy.set(false);
    }
  }

  // ---------------------------------------------------------------- direct period result (no activities)

  dirty(): boolean {
    return this.rows().some((x) => this.changed(x));
  }
  changed(row: EditableRow): boolean {
    if (row.draftStatus === '') return false;
    const score = row.draftStatus === 'Scored' ? parseScore(row.draftText) : null;
    return row.draftStatus !== row.status || score !== (row.score ?? null);
  }
  invalid(row: EditableRow): boolean {
    return parseScore(row.draftText) === undefined;
  }
  private ready(row: EditableRow): boolean {
    if (!this.changed(row) || this.invalid(row)) return false;
    return row.draftStatus !== 'Scored' || parseScore(row.draftText) !== null;
  }
  statusChanged(row: EditableRow): void {
    if (row.draftStatus !== 'Scored') row.draftText = '';
    this.touch();
  }
  scoreChanged(row: EditableRow): void {
    if (row.draftText.trim() !== '' && row.draftStatus === '') row.draftStatus = 'Scored';
    this.touch();
  }
  private touch(): void {
    this.rows.set([...this.rows()]);
    if (!this.autosaves()) return;
    this.saveState.set('Cambios sin guardar…');
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.save(true), 1200);
  }
  move(event: KeyboardEvent, index: number): void {
    const down = (event.key === 'Enter' && !event.shiftKey) || event.key === 'ArrowDown';
    const up = (event.key === 'Enter' && event.shiftKey) || event.key === 'ArrowUp';
    if (!down && !up) return;
    event.preventDefault();
    const inputs = Array.from(this.host.nativeElement.querySelectorAll<HTMLInputElement>('input.score-input')).filter((x) => !x.disabled);
    const target = down ? inputs.find((x) => Number(x.dataset['row']) > index) : inputs.reverse().find((x) => Number(x.dataset['row']) < index);
    if (target) target.focus();
    else if (down && event.key === 'Enter' && this.autosaves() && this.dirty()) void this.save(true);
  }
  paste(event: ClipboardEvent, index: number): void {
    const text = event.clipboardData?.getData('text/plain') ?? '';
    const lines = text.replace(/\r/g, '').split('\n');
    if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
    if (lines.length < 2) return;
    event.preventDefault();
    const rows = this.rows();
    let filled = 0;
    lines.forEach((line, offset) => {
      const row = rows[index + offset];
      const cell = line.split('\t')[0].trim();
      if (!row || cell === '') return;
      const upper = cell.toUpperCase();
      if (upper === 'E' || upper.startsWith('EXON')) {
        row.draftStatus = 'Excused';
        row.draftText = '';
      } else if (upper === 'P' || upper.startsWith('PEND')) {
        row.draftStatus = 'Missing';
        row.draftText = '';
      } else {
        row.draftStatus = 'Scored';
        row.draftText = cell;
      }
      filled++;
    });
    const extra = Math.max(0, lines.length - (rows.length - index));
    this.touch();
    this.saveState.set(
      `Se pegaron ${filled} ${filled === 1 ? 'valor' : 'valores'}` +
        (extra ? `; ${extra} ${extra === 1 ? 'fila sobraba' : 'filas sobraban'}.` : '.') +
        (this.autosaves() ? ' Guardando…' : ''),
    );
  }
  save(auto: boolean): Promise<void> {
    if (this.saving) {
      this.queued = true;
      return this.saving;
    }
    this.saving = this.saveChanges(auto).finally(() => {
      this.saving = null;
    });
    return this.saving;
  }
  private async saveChanges(auto: boolean): Promise<void> {
    const sheet = this.sheet();
    if (!sheet) return;
    if (this.busy()) {
      this.queued = true;
      return;
    }
    clearTimeout(this.timer);
    const pending = this.rows().filter((x) => this.changed(x) || (x.draftStatus === 'Scored' && this.invalid(x)));
    const wrong = pending.filter((x) => !this.ready(x));
    if (!auto && wrong.length) {
      const who = wrong.slice(0, 3).map((x) => x.name).join(', ') + (wrong.length > 3 ? ` y ${wrong.length - 3} más` : '');
      this.error.set(`Revisa la nota de ${who}: cada estudiante «Con nota» necesita un valor entre 0 y 100.`);
      return;
    }
    const changed = pending.filter((x) => this.ready(x));
    if (!changed.length) {
      if (auto) this.saveState.set(pending.length ? 'Completa o corrige las notas marcadas para guardarlas.' : '');
      return;
    }
    this.busy.set(true);
    // The button only shows progress for an explicit save; autosave reports in the status line next to it.
    if (!auto) this.manualSaving.set(true);
    this.error.set('');
    if (auto) this.saveState.set('Guardando…');
    try {
      const result = await this.api.post<SaveResult>(`/api/v1/courses/${sheet.course.id}/grades`, {
        periodId: sheet.period.id,
        reason: this.reason || null,
        entries: changed.map((x) => ({
          studentId: x.studentId,
          score: x.draftStatus === 'Scored' ? parseScore(x.draftText) : null,
          status: x.draftStatus,
          version: x.version,
        })),
      });
      const saved = new Map(result.rows.map((x) => [x.studentId, x]));
      for (const row of this.rows()) {
        const done = saved.get(row.studentId);
        if (!done) continue;
        row.status = done.status;
        row.score = done.score;
        row.version = done.version;
      }
      this.rows.set([...this.rows()]);
      if (auto) {
        const time = new Intl.DateTimeFormat(this.school.locale.culture, { hour: '2-digit', minute: '2-digit' }).format(new Date());
        this.saveState.set(`Guardado a las ${time} · ${result.changed === 1 ? '1 nota' : `${result.changed} notas`}.`);
      } else {
        this.saveState.set('');
        this.alerts.success(result.changed === 1 ? 'Se guardó 1 nota.' : `Se guardaron ${result.changed} notas.`);
        if (sheet.requiresReason) this.reason = '';
      }
    } catch (error) {
      this.saveState.set('');
      this.error.set(this.api.message(error) + (auto ? ' Los cambios siguen en pantalla; inténtalo de nuevo con «Guardar notas».' : ''));
    } finally {
      this.busy.set(false);
      this.manualSaving.set(false);
    }
    if (this.queued) {
      this.queued = false;
      if (this.dirty()) this.touch();
    }
  }

  // ---------------------------------------------------------------- loading

  retryLoad(): void {
    void this.load(this.route.snapshot.queryParamMap.get('period') ?? undefined);
  }
  async load(periodId?: string, courseId = this.id()): Promise<void> {
    const request = ++this.loadRequest;
    this.loading.set(!this.book());
    this.notFound.set(false);
    this.error.set('');
    clearTimeout(this.timer);
    try {
      const query = periodId ? `?periodId=${periodId}` : '';
      const book = await this.api.get<Book>(`/api/v1/courses/${encodeURIComponent(courseId)}/activities${query}`);
      if (request !== this.loadRequest) return;
      let sheet: GradeSheet | null = null;
      if (!book.activities.length) {
        sheet = await this.api.get<GradeSheet>(`/api/v1/courses/${encodeURIComponent(courseId)}/grades?periodId=${book.period.id}`);
        if (request !== this.loadRequest) return;
      }
      this.book.set(book);
      this.cells.set(book.rows.map((row) => ({ ...row, draft: Object.fromEntries(book.activities.map((a) => [a.id, cellText(row.scores[a.id])])) })));
      this.sheet.set(sheet);
      this.rows.set((sheet?.rows ?? []).map((x) => ({ ...x, draftText: x.score === null ? '' : String(x.score), draftStatus: x.status ?? '' })));
      this.reason = '';
      this.saveState.set('');
    } catch (error) {
      if (request !== this.loadRequest) return;
      this.book.set(null);
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    } finally {
      if (request === this.loadRequest) this.loading.set(false);
    }
  }
}
