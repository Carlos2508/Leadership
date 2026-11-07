import { Component, ElementRef, OnDestroy, ViewChild, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
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
import { CyclePipe } from '../shared/cycle.pipe';
import { UpperCasePipe } from '@angular/common';

type ScoreState = 'Scored' | 'Missing' | 'Excused';
interface SheetPeriod {
  id: string;
  sequence: number;
  name: string;
  startsOn: string;
  endsOn: string;
  published: boolean;
}
interface SheetRow {
  studentId: string;
  name: string;
  code: string;
  score: number | null;
  status: ScoreState | null;
  version: string | null;
}
interface GradeSheet {
  course: CourseView;
  period: SheetPeriod;
  periods: SheetPeriod[];
  canEdit: boolean;
  requiresReason: boolean;
  rows: SheetRow[];
  /** Qualitative scale of this grade's stage (e.g. Preprimaria); null for numeric 0–100. */
  scale: { name: string; levels: { code: string; label: string; value: number }[] } | null;
}
interface EditableRow extends SheetRow {
  draftText: string;
  draftStatus: ScoreState | '';
}
interface SaveResult {
  changed: number;
  rows: { studentId: string; score: number | null; status: ScoreState; version: string }[];
}

/** Parses a typed score: number, null when empty, undefined when invalid (accepts a decimal comma). */
export function parseScore(text: string): number | null | undefined {
  const value = text.trim().replace(',', '.');
  if (value === '') return null;
  if (!/^\d{1,3}(\.\d{1,2})?$/.test(value)) return undefined;
  const score = Number(value);
  return score >= 0 && score <= 100 ? score : undefined;
}

/**
 * Period grade sheet of a course: spreadsheet-like capture by the assigned teacher (Enter moves down, a pasted
 * column fills consecutive students, debounced autosave with row versions), and corrections after publication by
 * coordination, which always need a reason and are never autosaved.
 */
@Component({
  imports: [Shell, Icon, CourseNav, FormsModule, RouterLink, CyclePipe, UpperCasePipe],
  template: `
    <app-shell [title]="sheet()?.course?.subject ?? 'Libreta'">
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
      } @else if (sheet(); as s) {
        <div class="page-heading">
          <div>
            <span class="eyebrow">LIBRETA · {{ s.course.year | cycle | uppercase }}</span>
            <h1>{{ s.course.subject }}</h1>
            <p>{{ s.course.grade }} · {{ s.course.section ? 'Sección ' + s.course.section : 'Todo el grado' }}</p>
          </div>
          <a class="button outline" [href]="'/api/v1/courses/' + s.course.id + '/grades.pdf?periodId=' + s.period.id" target="_blank" rel="noopener"
            >Libreta en PDF</a
          >
        </div>
        @if (notice()) {
          <p class="feedback success-feedback" role="status" tabindex="-1" #noticeRef>{{ notice() }}</p>
        }
        <nav class="period-tabs" aria-label="Períodos del ciclo">
          @for (period of s.periods; track period.id) {
            <a
              [routerLink]="[]"
              [queryParams]="{ period: period.id }"
              [class.active]="period.id === s.period.id"
              [attr.aria-current]="period.id === s.period.id ? 'page' : null"
              >{{ period.name }}
              @if (period.published) {
                <span class="pill mint">Publicado</span>
              }</a
            >
          }
        </nav>
        <section class="card directory-card">
          <p class="directory-summary period-summary">
            {{ date(s.period.startsOn) }} – {{ date(s.period.endsOn) }} · {{ recorded() }} de {{ rows().length }} registradas
            @if (s.period.published) {
              · Publicado: {{ s.canEdit ? 'los cambios se registran como corrección con motivo.' : 'solo coordinación puede corregir.' }}
            }
          </p>
          @if (rows().length) {
            @if (s.canEdit) {
              <p class="sheet-hint" id="sheet-hint">
                <app-icon name="info" />
                <span
                  >Escribe la nota y pulsa <kbd>Enter</kbd> para pasar al siguiente estudiante. Puedes pegar una columna copiada de
                  Excel (E = exonerado, P = pendiente).{{ s.requiresReason ? '' : ' Los cambios se guardan solos.' }}</span
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
                      <th scope="col">{{ s.scale ? 'Logro (' + s.scale.name + ')' : 'Nota (0–100)' }}</th>
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
                          @if (s.scale) {
                            <label
                              ><span class="sr-only">Logro de {{ row.name }}</span
                              ><select
                                class="inline-select"
                                [name]="'level-' + row.studentId"
                                [(ngModel)]="row.draftText"
                                (ngModelChange)="scoreChanged(row)"
                                [disabled]="!s.canEdit || (row.draftStatus !== 'Scored' && row.draftStatus !== '')"
                              >
                                <option value="">—</option>
                                @for (level of s.scale.levels; track level.value) {
                                  <option [value]="'' + level.value">{{ level.label }}</option>
                                }
                              </select></label
                            >
                          } @else {
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
                  <button class="button" type="submit" [disabled]="busy() || !dirty()">
                    {{ busy() ? 'Guardando…' : 'Guardar notas' }}
                  </button>
                </div>
              }
            </form>
          } @else {
            <p class="empty-inline padded">No hubo estudiantes en este curso durante el período.</p>
          }
        </section>
      } @else {
        <button class="button outline" (click)="retryLoad()">Reintentar libreta</button>
      }
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
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;
  readonly sheet = signal<GradeSheet | null>(null);
  readonly rows = signal<EditableRow[]>([]);
  readonly notFound = signal(false);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly saveState = signal('');
  readonly recorded = computed(() => this.rows().filter((x) => x.status).length);
  reason = '';
  private timer: ReturnType<typeof setTimeout> | undefined;
  private queued = false;
  private saving: Promise<void> | null = null;
  private leaving: Promise<boolean> | null = null;
  private loadRequest = 0;

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap]).pipe(
      map(([params, query]) => [params.get('id')!, query.get('period') ?? undefined] as const),
      distinctUntilChanged((a, b) => a[0] === b[0] && a[1] === b[1]),
      takeUntilDestroyed(),
    ).subscribe(([id, period]) => void this.load(period, id));
  }
  ngOnDestroy(): void {
    clearTimeout(this.timer);
    this.loadRequest++;
  }
  canLeave(): Promise<boolean> {
    if (this.leaving) return this.leaving;
    this.leaving = this.prepareToLeave().finally(() => { this.leaving = null; });
    return this.leaving;
  }
  private async prepareToLeave(): Promise<boolean> {
    clearTimeout(this.timer);
    if (this.saving) await this.saving;
    if (this.autosaves() && this.dirty()) await this.save(true);
    clearTimeout(this.timer);
    if (!this.dirty()) return true;
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
  private autosaves(): boolean {
    const s = this.sheet();
    return !!s && s.canEdit && !s.requiresReason;
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
    this.notice.set('');
    if (!this.autosaves()) return;
    this.saveState.set('Cambios sin guardar…');
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.save(true), 1200);
  }
  selectText(event: FocusEvent): void {
    (event.target as HTMLInputElement).select();
  }
  /** Enter/↓ go to the next student, Shift+Enter/↑ to the previous one, like a spreadsheet column. */
  move(event: KeyboardEvent, index: number): void {
    const down = (event.key === 'Enter' && !event.shiftKey) || event.key === 'ArrowDown';
    const up = (event.key === 'Enter' && event.shiftKey) || event.key === 'ArrowUp';
    if (!down && !up) return;
    event.preventDefault();
    const inputs = Array.from(this.host.nativeElement.querySelectorAll<HTMLInputElement>('input.score-input')).filter((x) => !x.disabled);
    const target = down
      ? inputs.find((x) => Number(x.dataset['row']) > index)
      : inputs.reverse().find((x) => Number(x.dataset['row']) < index);
    if (target) target.focus();
    else if (down && event.key === 'Enter' && this.autosaves() && this.dirty()) void this.save(true);
  }
  /** Pasting a column (Excel/Sheets) fills consecutive students from this row; E/P mark exonerated/pending. */
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
  retryLoad(): void {
    void this.load(this.route.snapshot.queryParamMap.get('period') ?? undefined);
  }
  async load(periodId?: string, courseId = this.id()): Promise<void> {
    const request = ++this.loadRequest;
    this.loading.set(true);
    this.notFound.set(false);
    this.error.set('');
    this.notice.set('');
    this.sheet.set(null);
    this.rows.set([]);
    clearTimeout(this.timer);
    try {
      const query = periodId ? `?periodId=${periodId}` : '';
      const sheet = await this.api.get<GradeSheet>(`/api/v1/courses/${encodeURIComponent(courseId)}/grades${query}`);
      if (request !== this.loadRequest) return;
      this.sheet.set(sheet);
      this.rows.set(sheet.rows.map((x) => ({ ...x, draftText: x.score === null ? '' : String(x.score), draftStatus: x.status ?? '' })));
      this.reason = '';
      this.saveState.set('');
    } catch (error) {
      if (request !== this.loadRequest) return;
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    } finally {
      if (request === this.loadRequest) this.loading.set(false);
    }
  }
  save(auto: boolean): Promise<void> {
    if (this.saving) {
      this.queued = true;
      return this.saving;
    }
    this.saving = this.saveChanges(auto).finally(() => { this.saving = null; });
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
      // Name the students: on a long list the marked boxes may be out of sight.
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
      // Apply saved values and new versions in place: anything typed meanwhile stays as a pending change.
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
    }
    if (this.queued) {
      this.queued = false;
      if (this.dirty()) this.touch();
    }
  }
}
