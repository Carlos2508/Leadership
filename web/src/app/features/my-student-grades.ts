import {
  Component,
  ViewEncapsulation,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { InstitutionService } from '../core/institution';
import { money } from '../core/finance-api';
import {
  MyGrade,
  MyGrades,
  MyGradesPeriod,
  StudentAccessView,
  points,
  subjectTone,
} from '../core/student-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { StudentHeader } from '../shared/student-header';

/**
 * Published results of a student: one card per subject with the points of each activity, and the evolution across the
 * published periods. Data comes only from the grades endpoint (AcademicRecords + clearance on every read); no averages.
 */
@Component({
  imports: [Shell, Icon, StudentHeader],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../styles/_student-pages.scss', '../../styles/_student-grades.scss'],
  template: `
    <app-shell title="Calificaciones">
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a esta información</h2>
          <p>Si crees que es un error, comunícate con la administración del colegio.</p>
        </section>
      } @else if (view(); as v) {
        <app-student-header [view]="v" title="Calificaciones">
          @if (available().length) {
            <a
              class="button outline"
              [href]="'/api/v1/me/students/' + v.student.id + '/grades.pdf'"
              target="_blank"
              rel="noopener"
              ><app-icon name="download" />Boletín en PDF</a
            >
          }
        </app-student-header>
        @if (!v.isSelf && !v.faculties.includes('AcademicRecords')) {
          <section class="card empty-state">
            <app-icon name="shield" />
            <h2>Sin acceso a calificaciones</h2>
            <p>Tu vínculo no incluye la consulta académica.</p>
          </section>
        } @else if (error()) {
          <p class="feedback error-feedback" role="alert">
            {{ error() }} <button class="text-button" (click)="load()">Reintentar</button>
          </p>
        } @else if (grades(); as g) {
          @if (!g.periods.length) {
            <section class="card empty-state sp-empty">
              <app-icon name="chart" />
              <h2>Aún no hay calificaciones publicadas</h2>
              <p>
                El colegio publica las notas al cerrar cada período. Mientras tanto puedes revisar
                tus tareas y comentarios.
              </p>
            </section>
          } @else {
            <nav class="sp-period-tabs" aria-label="Períodos publicados">
              @for (p of g.periods; track p.periodId) {
                <button
                  type="button"
                  [class.active]="p.periodId === period()?.periodId"
                  [attr.aria-pressed]="p.periodId === period()?.periodId"
                  (click)="selected.set(p.periodId)"
                >
                  {{ p.name }}
                  @if (p.status !== 'Available') {
                    <app-icon name="shield" />
                  }
                </button>
              }
            </nav>
            @if (period(); as p) {
              @if (p.status !== 'Available') {
                <section class="card sp-blocked" role="note">
                  <span class="sp-stat-icon tone-gold"><app-icon name="shield" /></span>
                  <div>
                    <h2>Las calificaciones de {{ p.name }} están publicadas</h2>
                    <p>
                      {{
                        p.status === 'PendingFinancialSetup'
                          ? 'El colegio está completando la configuración de cuotas. Comunícate con caja.'
                          : 'Para consultarlas se necesitan cubiertas las colegiaturas requeridas para esta entrega.'
                      }}
                    </p>
                    @for (item of p.pending; track $index) {
                      <small>{{ item.label }} · {{ format(item.balance) }}</small>
                    }
                    <small
                      >{{ v.isSelf ? 'Consulta con tu encargado o en caja. ' : '' }}Las tareas y
                      entregas siguen disponibles.</small
                    >
                  </div>
                </section>
              } @else {
                <section class="sp-stats" aria-label="Resumen del período">
                  <div class="sp-stat static">
                    <span class="sp-stat-icon tone-gold"><app-icon name="book" /></span>
                    <span
                      ><strong>{{ scored(p).length }}</strong
                      ><small>Materias con nota</small></span
                    >
                  </div>
                  @if (best(p); as b) {
                    <div class="sp-stat static">
                      <span class="sp-stat-icon tone-green"><app-icon name="star" /></span>
                      <span
                        ><strong>{{ fmt(b.score) }}</strong
                        ><small>Mejor resultado · {{ b.subject }}</small></span
                      >
                    </div>
                  }
                  <div class="sp-stat static">
                    <span class="sp-stat-icon tone-blue"><app-icon name="clipboard" /></span>
                    <span
                      ><strong>{{ activityCount(p) }}</strong
                      ><small>Actividades calificadas</small></span
                    >
                  </div>
                  <div class="sp-stat static">
                    <span class="sp-stat-icon tone-red"><app-icon name="clock" /></span>
                    <span
                      ><strong>{{ pendingCount(p) }}</strong
                      ><small>Pendientes o exoneradas</small></span
                    >
                  </div>
                </section>
                <section class="sp-subjects" aria-label="Materias">
                  @for (grade of p.grades; track grade.subject) {
                    <article class="card sp-subject-card" [attr.data-tone]="tone(grade.subject)">
                      <header>
                        <div>
                          <span class="sp-subject" [attr.data-tone]="tone(grade.subject)">{{
                            grade.subject
                          }}</span>
                          <small>{{
                            (grade.activities?.length ?? 0)
                              ? grade.activities!.length + ' actividades'
                              : 'Nota del período'
                          }}</small>
                        </div>
                        <div
                          class="sp-ring"
                          [style.--value]="ring(grade)"
                          [attr.aria-label]="label(grade)"
                        >
                          <svg viewBox="0 0 36 36" aria-hidden="true">
                            <circle cx="18" cy="18" r="15.9" class="track" />
                            <circle cx="18" cy="18" r="15.9" class="value" pathLength="100" />
                          </svg>
                          <span>{{
                            grade.status === 'Scored'
                              ? fmt(grade.score)
                              : grade.status === 'Excused'
                                ? 'EX'
                                : 'P'
                          }}</span>
                        </div>
                      </header>
                      @if (grade.activities?.length) {
                        <ul class="sp-activities">
                          @for (a of grade.activities; track $index) {
                            <li>
                              <span class="sp-activity-name"
                                >{{ a.title }}
                                @if (a.assignmentId) {
                                  <em>Tarea</em>
                                }
                              </span>
                              <span class="sp-activity-bar" aria-hidden="true"
                                ><span [style.width.%]="bar(a.points, a.maxPoints)"></span
                              ></span>
                              <span class="sp-activity-points"
                                >{{
                                  a.status === 'Scored'
                                    ? fmt(a.points)
                                    : a.status === 'Missing'
                                      ? '0'
                                      : a.status === 'Excused'
                                        ? 'EX'
                                        : '—'
                                }}<small>/{{ fmt(a.maxPoints) }}</small></span
                              >
                            </li>
                          }
                        </ul>
                      } @else {
                        <p class="muted-copy">
                          {{
                            grade.status === 'Scored'
                              ? 'Resultado sobre 100 puntos.'
                              : grade.status === 'Excused'
                                ? 'Exonerado en este período.'
                                : 'Pendiente de entregar.'
                          }}
                        </p>
                      }
                    </article>
                  }
                </section>
              }
            }
            @if (matrix(); as m) {
              <section class="card sp-evolution" aria-labelledby="evolution-title">
                <h2 id="evolution-title">Evolución por período</h2>
                <div
                  class="table-scroll"
                  tabindex="0"
                  role="region"
                  aria-label="Calificaciones por materia y período"
                >
                  <table class="sp-matrix">
                    <thead>
                      <tr>
                        <th scope="col">Materia</th>
                        @for (p of m.periods; track p.periodId) {
                          <th scope="col">{{ p.name }}</th>
                        }
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of m.rows; track row.subject) {
                        <tr>
                          <th scope="row">
                            <span class="sp-dot" [attr.data-tone]="tone(row.subject)"></span
                            >{{ row.subject }}
                          </th>
                          @for (cell of row.cells; track $index) {
                            <td>
                              @if (cell.value !== null) {
                                <span class="sp-cell"
                                  ><span class="sp-cell-bar" [style.width.%]="cell.value"></span
                                  ><strong>{{ fmt(cell.value) }}</strong></span
                                >
                              } @else {
                                <span class="muted-copy">{{ cell.text }}</span>
                              }
                            </td>
                          }
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                <p class="muted-copy">
                  Resultados de cada período publicado sobre 100 puntos. No se calculan promedios
                  anuales.
                </p>
              </section>
            }
          }
        } @else {
          <p class="loading-state card" role="status">Cargando calificaciones…</p>
        }
      } @else if (error()) {
        <p class="feedback error-feedback" role="alert">
          {{ error() }} <button class="text-button" (click)="load()">Reintentar</button>
        </p>
      } @else {
        <p class="loading-state card" role="status">Cargando…</p>
      }
    </app-shell>
  `,
})
export class MyStudentGrades {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  private readonly school = inject(InstitutionService).current;
  readonly view = signal<StudentAccessView | null>(null);
  readonly grades = signal<MyGrades | null>(null);
  readonly notFound = signal(false);
  readonly error = signal('');
  readonly selected = signal<string | null>(null);
  private loadRequest = 0;
  readonly available = computed(() =>
    (this.grades()?.periods ?? []).filter((p) => p.status === 'Available'),
  );
  readonly period = computed(() => {
    const periods = this.grades()?.periods ?? [];
    return (
      periods.find((p) => p.periodId === this.selected()) ??
      this.available().at(-1) ??
      periods.at(-1) ??
      null
    );
  });
  readonly matrix = computed(() => {
    const periods = this.available();
    if (periods.length < 2) return null;
    const subjects = [...new Set(periods.flatMap((p) => p.grades.map((g) => g.subject)))].sort(
      (a, b) => a.localeCompare(b, 'es'),
    );
    return {
      periods,
      rows: subjects.map((subject) => ({
        subject,
        cells: periods.map((p) => {
          const g = p.grades.find((x) => x.subject === subject);
          if (!g) return { value: null, text: '—' };
          if (g.status === 'Scored') return { value: g.score, text: '' };
          return { value: null, text: g.status === 'Excused' ? 'Exonerado' : 'Pendiente' };
        }),
      })),
    };
  });

  constructor() {
    effect(() => {
      this.id();
      untracked(() => void this.load());
    });
  }
  async load(): Promise<void> {
    const request = ++this.loadRequest;
    const id = this.id();
    this.view.set(null);
    this.grades.set(null);
    this.selected.set(null);
    this.notFound.set(false);
    this.error.set('');
    try {
      const view = await this.api.get<StudentAccessView>(
        `/api/v1/me/students/${encodeURIComponent(id)}`,
      );
      if (request !== this.loadRequest) return;
      this.view.set(view);
      if (!view.isSelf && !view.faculties.includes('AcademicRecords')) return;
      const grades = await this.api.get<MyGrades>(`/api/v1/me/students/${view.student.id}/grades`);
      if (request === this.loadRequest) this.grades.set(grades);
    } catch (error) {
      if (request !== this.loadRequest) return;
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400))
        this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
  scored(p: MyGradesPeriod): MyGrade[] {
    return p.grades.filter((g) => g.status === 'Scored');
  }
  best(p: MyGradesPeriod): MyGrade | null {
    return this.scored(p).reduce<MyGrade | null>(
      (top, g) => (!top || (g.score ?? 0) > (top.score ?? 0) ? g : top),
      null,
    );
  }
  activityCount(p: MyGradesPeriod): number {
    return p.grades.reduce((n, g) => n + (g.activities ?? []).filter((a) => a.status).length, 0);
  }
  pendingCount(p: MyGradesPeriod): number {
    return p.grades.filter((g) => g.status !== 'Scored').length;
  }
  ring(grade: MyGrade): number {
    return grade.status === 'Scored' ? Math.max(0, Math.min(100, grade.score ?? 0)) : 0;
  }
  bar(value: number | null, max: number): number {
    return value === null || !max ? 0 : Math.max(0, Math.min(100, (value / max) * 100));
  }
  label(grade: MyGrade): string {
    return grade.status === 'Scored'
      ? `${points(grade.score)} de 100 puntos`
      : grade.status === 'Excused'
        ? 'Exonerado'
        : 'Pendiente';
  }
  tone(subject: string): number {
    return subjectTone(subject);
  }
  fmt(value: number | null): string {
    return points(value);
  }
  format(value: number): string {
    return money(value, this.school.locale.currency, this.school.locale.culture);
  }
}
