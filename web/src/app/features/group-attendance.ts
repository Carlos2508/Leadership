import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { PluralPipe, plural } from '../shared/plural.pipe';

type Status = 'Present' | 'Absent' | 'Late' | 'Excused';
interface Group {
  academicYearId: string;
  year: string;
  gradeLevelId: string;
  grade: string;
  sectionId: string | null;
  section: string | null;
  courses: number;
}
interface Row {
  studentId: string;
  name: string;
  code: string;
  group: string;
  status: Status | null;
  note: string;
  draft: Status;
  draftNote: string;
}
interface Sheet {
  academicYearId: string;
  date: string;
  canEdit: boolean;
  courses: number;
  rows: Omit<Row, 'draft' | 'draftNote'>[];
}

const STORAGE_KEY = 'group-attendance-selection';

/**
 * Daily attendance per grade and section: one mark per student per day, written to every course of the group the
 * teacher has. Several groups can be taken together (multigrade rooms); each student keeps their own grade.
 */
@Component({
  imports: [Shell, Icon, FormsModule, PluralPipe],
  template: `
    <app-shell title="Asistencia diaria">
      <div class="page-heading">
        <div>
          <span class="eyebrow">ASISTENCIA DIARIA</span>
          <h1>Asistencia por grado y sección</h1>
          <p>Marca una sola vez al día. Se registra en todos tus cursos del grupo.</p>
        </div>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      @if (groupsLoading()) {
        <p class="loading-state card" role="status">Cargando grupos…</p>
      } @else if (!groups().length) {
        <section class="card empty-state">
          <app-icon name="calendar" />
          <h2>No tienes grupos asignados</h2>
          <p>Cuando coordinación te asigne cursos, aparecerán aquí sus grados y secciones.</p>
        </section>
      } @else {
        <section class="card directory-card">
          <div class="filters group-picker">
            <fieldset class="group-options" [disabled]="busy()">
              <legend>Grupo{{ groups().length > 1 ? 's (elige varios para un aula multigrado)' : '' }}</legend>
              @for (g of groups(); track key(g)) {
                <label class="check-option"
                  ><input type="checkbox" [checked]="selected().has(key(g))" (change)="toggle(g)" />
                  <span>{{ g.grade }}{{ g.section ? ' · ' + g.section : '' }} <small>{{ g.year }}</small></span></label
                >
              }
            </fieldset>
            <label class="select-field date-field"
              ><span>Fecha</span><input type="date" name="date" [(ngModel)]="date" (change)="load()" [disabled]="busy()"
            /></label>
          </div>
        </section>
        @if (loading()) {
          <p class="loading-state card" role="status">Cargando estudiantes…</p>
        } @else if (sheet(); as s) {
          <section class="card directory-card">
            <div class="filters">
              <p class="directory-summary attendance-counts" aria-live="polite">
                @if (dirty()) {
                  <span class="pill gold">Cambios sin guardar</span>
                } @else if (recorded() === rows().length && rows().length) {
                  <span class="pill mint">Registrada</span>
                } @else {
                  <span class="pill gold">{{ recorded() ? 'Registro parcial' : 'Sin registrar' }}</span>
                }
                {{ counts().absent | plural: 'ausencia' : 'ausencias' }} ·
                {{ counts().late | plural: 'tardanza' : 'tardanzas' }} ·
                {{ counts().excused | plural: 'justificada' : 'justificadas' }} ·
                se guarda en {{ s.courses | plural: 'curso' : 'cursos' }}
              </p>
            </div>
            @if (rows().length) {
              <form (ngSubmit)="save()">
                <ul class="attendance-list">
                  @for (row of rows(); track row.studentId) {
                    <li>
                      <span class="person-copy"
                        ><strong>{{ row.name }}</strong><small>{{ row.code }} · {{ row.group }}</small></span
                      >
                      <fieldset class="attendance-options" [disabled]="!s.canEdit || busy()">
                        <legend class="sr-only">Asistencia de {{ row.name }}</legend>
                        @for (option of options; track option.value) {
                          <label class="attendance-option" [class.selected]="row.draft === option.value" [attr.data-status]="option.value"
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
                    <button class="button outline" type="button" (click)="allPresent()" [disabled]="busy()">Todos presentes</button>
                    <button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Guardando…' : 'Guardar asistencia del día' }}</button>
                  </div>
                } @else {
                  <p class="muted-copy padded">Las fechas futuras son solo de consulta.</p>
                }
              </form>
            } @else {
              <p class="empty-inline padded">No hay estudiantes en ese grupo en esa fecha.</p>
            }
          </section>
        } @else if (!selected().size) {
          <p class="empty-inline card padded">Elige al menos un grupo.</p>
        }
      }
    </app-shell>
  `,
})
export class GroupAttendance implements OnInit {
  private readonly api = inject(AccessApi);
  private readonly alerts = inject(Alerts);
  private readonly route = inject(ActivatedRoute);
  readonly groups = signal<Group[]>([]);
  readonly selected = signal(new Set<string>());
  readonly sheet = signal<Sheet | null>(null);
  readonly rows = signal<Row[]>([]);
  readonly groupsLoading = signal(true);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  date = '';
  private request = 0;
  readonly options: { value: Status; label: string }[] = [
    { value: 'Present', label: 'Presente' },
    { value: 'Absent', label: 'Ausente' },
    { value: 'Late', label: 'Tarde' },
    { value: 'Excused', label: 'Justificada' },
  ];
  readonly recorded = computed(() => this.rows().filter((x) => x.status !== null).length);
  readonly dirty = computed(() =>
    this.rows().some((x) => x.draft !== (x.status ?? 'Present') || (x.draft === 'Present' ? '' : x.draftNote.trim()) !== (x.status && x.status !== 'Present' ? x.note : '')),
  );
  readonly counts = computed(() => ({
    absent: this.rows().filter((x) => x.draft === 'Absent').length,
    late: this.rows().filter((x) => x.draft === 'Late').length,
    excused: this.rows().filter((x) => x.draft === 'Excused').length,
  }));

  key(g: Pick<Group, 'academicYearId' | 'gradeLevelId' | 'sectionId'>): string {
    return `${g.academicYearId}|${g.gradeLevelId}:${g.sectionId ?? ''}`;
  }

  async ngOnInit(): Promise<void> {
    try {
      const groups = await this.api.get<Group[]>('/api/v1/attendance/groups');
      this.groups.set(groups);
      const wanted = this.route.snapshot.queryParamMap.get('groups');
      let initial: string[] = [];
      if (wanted) initial = wanted.split(',');
      else {
        try {
          initial = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
        } catch {
          initial = [];
        }
      }
      const valid = new Set(groups.map((g) => this.key(g)));
      let chosen = initial.filter((k) => valid.has(k));
      if (!chosen.length && groups.length) chosen = [this.key(groups[0])];
      // A selection always belongs to a single year.
      const year = chosen[0]?.split('|')[0];
      this.selected.set(new Set(chosen.filter((k) => k.startsWith(year + '|'))));
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.groupsLoading.set(false);
    }
    await this.load();
  }

  toggle(g: Group): void {
    const next = new Set(this.selected());
    const k = this.key(g);
    if (next.has(k)) next.delete(k);
    else {
      // Groups of another year cannot be mixed: start a new selection.
      for (const other of [...next]) if (!other.startsWith(g.academicYearId + '|')) next.delete(other);
      next.add(k);
    }
    this.selected.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify([...next]));
    } catch {
      /* storage unavailable: selection lasts this visit */
    }
    void this.load();
  }

  touch(): void {
    this.rows.set([...this.rows()]);
  }
  allPresent(): void {
    for (const row of this.rows()) row.draft = 'Present';
    this.touch();
  }

  async load(): Promise<void> {
    const request = ++this.request;
    const keys = [...this.selected()];
    this.sheet.set(null);
    this.rows.set([]);
    if (!keys.length) return;
    const yearId = keys[0].split('|')[0];
    const groups = keys.map((k) => k.split('|')[1]).join(',');
    this.loading.set(true);
    this.error.set('');
    try {
      const date = this.date ? `&date=${encodeURIComponent(this.date)}` : '';
      const sheet = await this.api.get<Sheet>(
        `/api/v1/attendance/group?yearId=${encodeURIComponent(yearId)}&groups=${encodeURIComponent(groups)}${date}`,
      );
      if (request !== this.request) return;
      this.date = sheet.date;
      this.sheet.set(sheet);
      this.rows.set(sheet.rows.map((x) => ({ ...x, draft: x.status ?? 'Present', draftNote: x.note })));
    } catch (error) {
      if (request !== this.request) return;
      this.error.set(error instanceof HttpErrorResponse && error.status === 404 ? 'No tienes cursos en ese grupo.' : this.api.message(error));
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    const sheet = this.sheet();
    if (!sheet || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const groups = [...this.selected()].map((k) => {
        const [grade, section] = k.split('|')[1].split(':');
        return { gradeLevelId: grade, sectionId: section || null };
      });
      const result = await this.api.post<{ students: number; courses: number; changed: number }>('/api/v1/attendance/group', {
        academicYearId: sheet.academicYearId,
        groups,
        date: sheet.date,
        entries: this.rows().map((x) => ({ studentId: x.studentId, status: x.draft, note: x.draft === 'Present' ? null : x.draftNote })),
      });
      if (result.changed)
        this.alerts.success(`Asistencia guardada para ${plural(result.students, 'estudiante', 'estudiantes')} en ${plural(result.courses, 'curso', 'cursos')}.`);
      else this.alerts.info('No había cambios.');
      await this.load();
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
