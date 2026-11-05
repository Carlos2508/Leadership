import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { CourseView } from './courses';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

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
  status: 'Scored' | 'Missing' | 'Excused' | null;
  version: string | null;
}
interface GradeSheet {
  course: CourseView;
  period: SheetPeriod;
  periods: SheetPeriod[];
  canEdit: boolean;
  requiresReason: boolean;
  rows: SheetRow[];
}
interface EditableRow extends SheetRow {
  draftScore: number | null;
  draftStatus: 'Scored' | 'Missing' | 'Excused' | '';
}

/** Period grade sheet of a course: capture by the assigned teacher, corrections after publication by coordination. */
@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  template: `
    <app-shell [title]="sheet()?.course?.subject ?? 'Libreta'">
      <a class="back-link" [routerLink]="['/workspace/courses', id()]"><app-icon name="arrow" />Volver a la lista</a>
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a esta libreta</h2>
          <p>Solo el personal académico y los docentes asignados pueden verla.</p>
        </section>
      } @else if (sheet(); as s) {
        <div class="page-heading">
          <div>
            <span class="eyebrow">LIBRETA · CICLO {{ s.course.year }}</span>
            <h1>{{ s.course.subject }}</h1>
            <p>{{ s.course.grade }} · {{ s.course.section ? 'Sección ' + s.course.section : 'Todo el grado' }}</p>
          </div>
        </div>
        @if (notice()) {
          <p class="feedback success-feedback" role="status" tabindex="-1" #noticeRef>{{ notice() }}</p>
        }
        @if (error()) {
          <p class="feedback error-feedback" role="alert">{{ error() }}</p>
        }
        <nav class="period-tabs" aria-label="Períodos del ciclo">
          @for (period of s.periods; track period.id) {
            <a
              [routerLink]="[]"
              [queryParams]="{ period: period.id }"
              [class.active]="period.id === s.period.id"
              [attr.aria-current]="period.id === s.period.id ? 'page' : null"
              (click)="select(period.id)"
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
            <form (ngSubmit)="save()">
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
                    @for (row of rows(); track row.studentId) {
                      <tr>
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
                              (ngModelChange)="row.draftStatus !== 'Scored' ? (row.draftScore = null) : null"
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
                              type="number"
                              min="0"
                              max="100"
                              step="0.01"
                              inputmode="decimal"
                              [name]="'score-' + row.studentId"
                              [(ngModel)]="row.draftScore"
                              (ngModelChange)="row.draftScore !== null && row.draftStatus === '' ? (row.draftStatus = 'Scored') : null"
                              [disabled]="!s.canEdit || row.draftStatus !== 'Scored' && row.draftStatus !== ''"
                          /></label>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              @if (s.canEdit) {
                <div class="sheet-actions">
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
        <p class="loading-state card" role="status">Cargando libreta…</p>
      }
    </app-shell>
  `,
})
export class CourseGrades {
  readonly id = input.required<string>();
  readonly period = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;
  readonly sheet = signal<GradeSheet | null>(null);
  readonly rows = signal<EditableRow[]>([]);
  readonly notFound = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly recorded = computed(() => this.rows().filter((x) => x.status).length);
  reason = '';

  constructor() {
    queueMicrotask(() => void this.load(this.period()));
  }
  date(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  dirty(): boolean {
    return this.rows().some((x) => this.changed(x));
  }
  private changed(row: EditableRow): boolean {
    return row.draftStatus !== '' && (row.draftStatus !== row.status || (row.draftScore ?? null) !== (row.score ?? null));
  }
  async select(periodId: string): Promise<void> {
    this.notice.set('');
    await this.load(periodId);
  }
  async load(periodId?: string): Promise<void> {
    this.error.set('');
    try {
      const query = periodId ? `?periodId=${periodId}` : '';
      const sheet = await this.api.get<GradeSheet>(`/api/v1/courses/${encodeURIComponent(this.id())}/grades${query}`);
      this.sheet.set(sheet);
      this.rows.set(sheet.rows.map((x) => ({ ...x, draftScore: x.score, draftStatus: x.status ?? '' })));
      this.reason = '';
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
  async save(): Promise<void> {
    const sheet = this.sheet();
    if (!sheet || this.busy()) return;
    const changed = this.rows().filter((x) => this.changed(x));
    if (changed.some((x) => x.draftStatus === 'Scored' && (x.draftScore === null || x.draftScore === undefined))) {
      this.error.set('Escribe la nota de cada estudiante marcado «Con nota».');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.post<{ changed: number }>(`/api/v1/courses/${sheet.course.id}/grades`, {
        periodId: sheet.period.id,
        reason: this.reason || null,
        entries: changed.map((x) => ({
          studentId: x.studentId,
          score: x.draftStatus === 'Scored' ? Number(x.draftScore) : null,
          status: x.draftStatus,
          version: x.version,
        })),
      });
      this.notice.set(`Se guardaron ${result.changed} notas.`);
      await this.load(sheet.period.id);
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
