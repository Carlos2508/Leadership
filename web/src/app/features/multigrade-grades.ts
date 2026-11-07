import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { CourseView } from './courses';
import { parseScore } from './course-grades';
import { Shell } from '../shared/shell';

type ScoreState = 'Scored' | 'Missing' | 'Excused';
interface Period {
  id: string;
  sequence: number;
  name: string;
  published: boolean;
}
interface Sheet {
  course: CourseView;
  period: Period;
  periods: Period[];
  canEdit: boolean;
  requiresReason: boolean;
  rows: { studentId: string; name: string; code: string; score: number | null; status: ScoreState | null; version: string | null }[];
  scale: { name: string; levels: { code: string; label: string; value: number }[] } | null;
}
interface Row {
  courseId: string;
  group: string;
  studentId: string;
  name: string;
  code: string;
  score: number | null;
  status: ScoreState | null;
  version: string | null;
  draftText: string;
  draftStatus: ScoreState | '';
}

/**
 * Multigrade room: the same subject taught to several grades/sections, captured in one table. Each student keeps the
 * grade of their own course (results stay separate); every course is saved with its own row versions.
 */
@Component({
  imports: [Shell, FormsModule],
  template: `
    <app-shell title="Notas por aula">
      <div class="page-heading">
        <div>
          <span class="eyebrow">AULA MULTIGRADO</span>
          <h1>Notas de varios grados juntos</h1>
          <p>Elige los cursos que das en la misma aula. Cada estudiante conserva la nota de su propio grado.</p>
        </div>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      <section class="card directory-card">
        <div class="filters">
          <label class="select-field"
            ><span>Materia</span
            ><select name="subject" [(ngModel)]="subject" (ngModelChange)="pickSubject()">
              @for (s of subjects(); track s) {
                <option [value]="s">{{ s }}</option>
              }
            </select></label
          >
          <fieldset class="group-options">
            <legend>Cursos del aula</legend>
            @for (c of coursesOfSubject(); track c.id) {
              <label class="check-option"
                ><input type="checkbox" [name]="'c-' + c.id" [(ngModel)]="chosen[c.id]" (ngModelChange)="load()" /><span
                  >{{ c.grade }}{{ c.section ? ' · ' + c.section : '' }}</span
                ></label
              >
            }
          </fieldset>
          @if (periods().length) {
            <label class="select-field"
              ><span>Período</span
              ><select name="period" [(ngModel)]="sequence" (ngModelChange)="load()">
                @for (p of periods(); track p.sequence) {
                  <option [ngValue]="p.sequence">{{ p.name }}{{ p.published ? ' (publicado)' : '' }}</option>
                }
              </select></label
            >
          }
        </div>
      </section>
      @if (loading()) {
        <p class="loading-state card" role="status">Cargando libretas…</p>
      } @else if (rows().length) {
        <section class="card directory-card">
          <form (ngSubmit)="save()">
            <div class="table-scroll" tabindex="0" role="region" aria-label="Notas del aula">
              <table class="stack-table grade-table">
                <thead>
                  <tr>
                    <th scope="col">Estudiante</th>
                    <th scope="col">Grado</th>
                    <th scope="col">Situación</th>
                    <th scope="col">Nota o logro</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows(); track row.courseId + row.studentId) {
                    <tr [class.row-invalid]="invalid(row)">
                      <td><span class="person-copy"><strong>{{ row.name }}</strong><small>{{ row.code }}</small></span></td>
                      <td data-label="Grado">{{ row.group }}</td>
                      <td data-label="Situación">
                        <select class="inline-select" [name]="'st-' + row.courseId + row.studentId" [(ngModel)]="row.draftStatus"
                          [disabled]="!editable(row)" [attr.aria-label]="'Situación de ' + row.name">
                          <option value="">Sin registrar</option>
                          <option value="Scored">Con nota</option>
                          <option value="Missing">Pendiente</option>
                          <option value="Excused">Exonerado</option>
                        </select>
                      </td>
                      <td data-label="Nota">
                        @if (sheets[row.courseId]?.scale; as scale) {
                          <select class="inline-select" [name]="'lv-' + row.courseId + row.studentId" [(ngModel)]="row.draftText"
                            (ngModelChange)="scored(row)" [disabled]="!editable(row) || (row.draftStatus !== 'Scored' && row.draftStatus !== '')"
                            [attr.aria-label]="'Logro de ' + row.name">
                            <option value="">—</option>
                            @for (level of scale.levels; track level.value) {
                              <option [value]="'' + level.value">{{ level.label }}</option>
                            }
                          </select>
                        } @else {
                          <input class="score-input" type="text" inputmode="decimal" autocomplete="off"
                            [name]="'sc-' + row.courseId + row.studentId" [(ngModel)]="row.draftText" (ngModelChange)="scored(row)"
                            [disabled]="!editable(row) || (row.draftStatus !== 'Scored' && row.draftStatus !== '')"
                            [attr.aria-label]="'Nota de ' + row.name" />
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="sheet-actions">
              <p class="muted-copy">Los períodos publicados se corrigen desde la libreta de cada curso, con motivo.</p>
              <button class="button" type="submit" [disabled]="busy() || !changedCount()">
                {{ busy() ? 'Guardando…' : 'Guardar ' + changedCount() + ' cambios' }}
              </button>
            </div>
          </form>
        </section>
      } @else if (chosenIds().length) {
        <p class="empty-inline card padded">No hay estudiantes en esos cursos durante el período.</p>
      }
    </app-shell>
  `,
})
export class MultigradeGrades implements OnInit {
  private readonly api = inject(AccessApi);
  private readonly alerts = inject(Alerts);
  readonly courses = signal<CourseView[]>([]);
  readonly rows = signal<Row[]>([]);
  readonly periods = signal<Period[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly subjects = computed(() => [...new Set(this.courses().map((c) => c.subject))].sort((a, b) => a.localeCompare(b, 'es')));
  sheets: Record<string, Sheet> = {};
  chosen: Record<string, boolean> = {};
  subject = '';
  sequence: number | null = null;
  private request = 0;

  coursesOfSubject(): CourseView[] {
    return this.courses().filter((c) => c.subject === this.subject);
  }
  chosenIds(): string[] {
    return this.coursesOfSubject().filter((c) => this.chosen[c.id]).map((c) => c.id);
  }
  editable(row: Row): boolean {
    const sheet = this.sheets[row.courseId];
    return !!sheet && sheet.canEdit && !sheet.period.published;
  }
  invalid(row: Row): boolean {
    return row.draftStatus === 'Scored' && row.draftText.trim() !== '' && parseScore(row.draftText) === undefined;
  }
  changed(row: Row): boolean {
    const status = row.draftStatus || (row.draftText.trim() ? 'Scored' : '');
    const score = status === 'Scored' ? parseScore(row.draftText) : null;
    if (status === '' || (status === 'Scored' && (score === null || score === undefined))) return false;
    return status !== row.status || score !== row.score;
  }
  changedCount(): number {
    return this.rows().filter((r) => this.editable(r) && this.changed(r) && !this.invalid(r)).length;
  }
  scored(row: Row): void {
    if (row.draftText.trim() !== '' && row.draftStatus === '') row.draftStatus = 'Scored';
    this.rows.set([...this.rows()]);
  }

  async ngOnInit(): Promise<void> {
    try {
      const courses = await this.api.get<CourseView[]>('/api/v1/me/courses');
      this.courses.set(courses);
      // Start with the subject the teacher gives to the most groups.
      const counts = new Map<string, number>();
      for (const c of courses) counts.set(c.subject, (counts.get(c.subject) ?? 0) + 1);
      this.subject = [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? '';
      this.pickSubject();
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }

  pickSubject(): void {
    this.chosen = Object.fromEntries(this.coursesOfSubject().map((c) => [c.id, true]));
    this.sequence = null;
    void this.load();
  }

  async load(): Promise<void> {
    const request = ++this.request;
    const ids = this.chosenIds();
    this.rows.set([]);
    if (!ids.length) return;
    this.loading.set(true);
    this.error.set('');
    try {
      const first = await this.api.get<Sheet>(`/api/v1/courses/${ids[0]}/grades`);
      this.periods.set(first.periods);
      if (this.sequence === null) this.sequence = first.period.sequence;
      const sheets: Record<string, Sheet> = {};
      for (const id of ids) {
        const probe = id === ids[0] ? first : await this.api.get<Sheet>(`/api/v1/courses/${id}/grades`);
        const period = probe.periods.find((p) => p.sequence === this.sequence);
        if (!period) continue;
        sheets[id] = probe.period.id === period.id ? probe : await this.api.get<Sheet>(`/api/v1/courses/${id}/grades?periodId=${period.id}`);
      }
      if (request !== this.request) return;
      this.sheets = sheets;
      this.rows.set(
        Object.values(sheets).flatMap((s) =>
          s.rows.map((r) => ({
            ...r,
            courseId: s.course.id,
            group: s.course.grade + (s.course.section ? ' · ' + s.course.section : ''),
            draftText: r.score === null ? '' : String(r.score),
            draftStatus: (r.status ?? '') as ScoreState | '',
          })),
        ),
      );
    } catch (error) {
      if (request === this.request) this.error.set(this.api.message(error));
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.busy()) return;
    const pending = this.rows().filter((r) => this.editable(r) && this.changed(r) && !this.invalid(r));
    if (!pending.length) return;
    this.busy.set(true);
    this.error.set('');
    let saved = 0;
    try {
      for (const courseId of [...new Set(pending.map((r) => r.courseId))]) {
        const entries = pending
          .filter((r) => r.courseId === courseId)
          .map((r) => {
            const status = (r.draftStatus || 'Scored') as ScoreState;
            return { studentId: r.studentId, score: status === 'Scored' ? parseScore(r.draftText) ?? null : null, status, version: r.version };
          });
        const result = await this.api.post<{ changed: number }>(`/api/v1/courses/${courseId}/grades`, {
          periodId: this.sheets[courseId].period.id,
          entries,
          reason: null,
        });
        saved += result.changed;
      }
      this.alerts.success(`Se guardaron ${saved} notas.`);
      await this.load();
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
