import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { CourseView } from './courses';
import { Shell } from '../shared/shell';
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
  rows: Omit<Row, 'draft' | 'draftNote'>[];
}

/** Daily attendance of a course for the assigned teacher; everyone starts as present until changed. */
@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, PluralPipe],
  template: `
    <app-shell title="Asistencia">
      <a class="back-link" [routerLink]="['/workspace/courses', id()]"><app-icon name="arrow" />Volver a la lista</a>
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" /><h2>No tienes acceso a este curso</h2>
          <p>Solo el personal académico y los docentes asignados pueden registrar asistencia.</p>
        </section>
      } @else if (sheet(); as s) {
        <div class="page-heading">
          <div>
            <span class="eyebrow">ASISTENCIA</span>
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
        <section class="card directory-card">
          <div class="filters">
            <div class="day-picker">
              <button type="button" class="icon-button day-step" (click)="shift(-1)" aria-label="Día anterior"><app-icon name="arrow" /></button>
              <label class="select-field date-field"
                ><span>Fecha</span><input type="date" name="date" [(ngModel)]="date" (change)="load()"
              /></label>
              <button type="button" class="icon-button day-step next" (click)="shift(1)" aria-label="Día siguiente"><app-icon name="arrow" /></button>
            </div>
            <p class="directory-summary attendance-counts" aria-live="polite">
              @if (recorded()) {
                <span class="pill mint">Registrada</span>
                {{ counts().present | plural: 'presente' : 'presentes' }} · {{ counts().absent | plural: 'ausencia' : 'ausencias' }} ·
                {{ counts().late | plural: 'tardanza' : 'tardanzas' }} · {{ counts().excused | plural: 'justificada' : 'justificadas' }}
              } @else {
                <span class="pill gold">Sin registrar</span> Todos aparecen como presentes; marca solo las excepciones y guarda.
              }
            </p>
          </div>
          @if (rows().length) {
            <form (ngSubmit)="save()">
              <ul class="attendance-list">
                @for (row of rows(); track row.studentId) {
                  <li>
                    <span class="person-copy"><strong>{{ row.name }}</strong><small>{{ row.code }}</small></span>
                    <fieldset class="attendance-options" [disabled]="!s.canEdit">
                      <legend class="sr-only">Asistencia de {{ row.name }}</legend>
                      @for (option of options; track option.value) {
                        <label class="attendance-option" [class.selected]="row.draft === option.value" [attr.data-status]="option.value"
                          ><input type="radio" [name]="'status-' + row.studentId" [value]="option.value" [(ngModel)]="row.draft" /><span>{{
                            option.label
                          }}</span></label
                        >
                      }
                    </fieldset>
                    @if (row.draft !== 'Present') {
                      <label class="note-field"
                        ><span class="sr-only">Observación de {{ row.name }}</span
                        ><input [name]="'note-' + row.studentId" [(ngModel)]="row.draftNote" maxlength="200" placeholder="Observación (opcional)" [disabled]="!s.canEdit"
                      /></label>
                    }
                  </li>
                }
              </ul>
              @if (s.canEdit) {
                <div class="sheet-actions">
                  <button class="button outline" type="button" (click)="allPresent()" [disabled]="busy()">Todos presentes</button>
                  <button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Guardando…' : 'Guardar asistencia' }}</button>
                </div>
              }
            </form>
          } @else {
            <p class="empty-inline padded">No hay estudiantes en el curso en esa fecha.</p>
          }
        </section>
      } @else {
        <p class="loading-state card" role="status">Cargando asistencia…</p>
      }
    </app-shell>
  `,
})
export class CourseAttendance {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly school = inject(InstitutionService).current;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;
  readonly sheet = signal<Sheet | null>(null);
  readonly rows = signal<Row[]>([]);
  readonly notFound = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly recorded = computed(() => this.rows().some((x) => x.status));
  readonly counts = computed(() => {
    const rows = this.rows();
    return {
      present: rows.filter((x) => x.status === 'Present').length,
      absent: rows.filter((x) => x.status === 'Absent').length,
      late: rows.filter((x) => x.status === 'Late').length,
      excused: rows.filter((x) => x.status === 'Excused').length,
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
    queueMicrotask(() => void this.load());
  }
  formatted(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  allPresent(): void {
    for (const row of this.rows()) row.draft = 'Present';
    this.rows.set([...this.rows()]);
  }
  /** Previous/next school day (skips weekends). */
  async shift(days: number): Promise<void> {
    if (!this.date) return;
    const value = new Date(this.date + 'T12:00:00');
    do value.setDate(value.getDate() + days);
    while (value.getDay() === 0 || value.getDay() === 6);
    this.date = value.toISOString().slice(0, 10);
    this.notice.set('');
    await this.load();
  }
  async load(): Promise<void> {
    this.error.set('');
    try {
      const query = this.date ? `?date=${this.date}` : '';
      const sheet = await this.api.get<Sheet>(`/api/v1/courses/${encodeURIComponent(this.id())}/attendance${query}`);
      this.sheet.set(sheet);
      this.date = sheet.date;
      this.rows.set(sheet.rows.map((x) => ({ ...x, draft: x.status ?? 'Present', draftNote: x.note })));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
  async save(): Promise<void> {
    const sheet = this.sheet();
    if (!sheet || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.post<{ changed: number }>(`/api/v1/courses/${sheet.course.id}/attendance`, {
        date: sheet.date,
        entries: this.rows().map((x) => ({
          studentId: x.studentId,
          status: x.draft,
          note: x.draft === 'Present' ? null : x.draftNote,
          version: x.version,
        })),
      });
      this.notice.set(result.changed ? `Asistencia guardada (${plural(result.changed, 'cambio', 'cambios')}).` : 'No había cambios.');
      await this.load();
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
