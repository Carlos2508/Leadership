import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { GradeView, YearDetail, YearSummary } from '../core/academic-api';
import { Shell } from '../shared/shell';

interface Candidate {
  id: string;
  name: string;
  code: string;
  previous: string | null;
}
interface ImportRow {
  row: number;
  givenNames: string;
  familyNames: string;
  studentCode: string | null;
  birthDate: string | null;
  grade: string | null;
  section: string | null;
  action: 'Create' | 'CreateAndEnroll' | 'Existing' | 'EnrollExisting' | 'Error';
  message: string | null;
}
interface ImportResult {
  applied: boolean;
  create: number;
  existing: number;
  enroll: number;
  errors: number;
  rows: ImportRow[];
}

/**
 * Mass enrollment: promote a whole group from the previous cycle (or any search) into a grade/section in one step,
 * and import students from Excel (.xlsx/.csv) with a preview before anything is saved.
 */
@Component({
  imports: [Shell, FormsModule, RouterLink],
  template: `
    <app-shell title="Inscripción masiva">
      <div class="page-heading">
        <div>
          <span class="eyebrow">MATRÍCULA ANUAL</span>
          <h1>Inscripción masiva</h1>
          <p>Inscribe grupos completos o importa una lista desde Excel.</p>
        </div>
        <a class="button outline" routerLink="/workspace/enrollment">Volver a inscripciones</a>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      <section class="card directory-card">
        <div class="filters">
          <label class="select-field"
            ><span>Ciclo de destino</span
            ><select name="year" [(ngModel)]="yearId" (ngModelChange)="changeYear()" [disabled]="busy()">
              @for (y of openYears(); track y.id) {
                <option [value]="y.id">{{ y.label }}</option>
              }
            </select></label
          >
          <nav class="period-tabs" aria-label="Forma de inscribir">
            <a [class.active]="tab() === 'group'" (click)="$event.preventDefault(); tab.set('group')" href="#">Inscribir varios</a>
            <a [class.active]="tab() === 'import'" (click)="$event.preventDefault(); tab.set('import')" href="#">Importar desde Excel</a>
          </nav>
        </div>
      </section>

      @if (tab() === 'group') {
        <section class="card detail-card">
          <h2>1. ¿A quiénes?</h2>
          <div class="form-grid">
            <label class="form-field"
              ><span>Del ciclo</span
              ><select name="fromYear" [(ngModel)]="fromYearId" (ngModelChange)="fromChanged()">
                <option value="">Buscar por nombre o código</option>
                @for (y of years(); track y.id) {
                  @if (y.id !== yearId) {
                    <option [value]="y.id">{{ y.label }}</option>
                  }
                }
              </select></label
            >
            @if (fromYearId) {
              <label class="form-field"
                ><span>Grado anterior</span
                ><select name="fromGrade" [(ngModel)]="fromGradeId" (ngModelChange)="fromSectionId = ''; search()">
                  <option value="">Elige un grado</option>
                  @for (g of grades(); track g.id) {
                    <option [value]="g.id">{{ g.name }}</option>
                  }
                </select></label
              >
              <label class="form-field"
                ><span>Sección anterior</span
                ><select name="fromSection" [(ngModel)]="fromSectionId" (ngModelChange)="search()">
                  <option value="">Todas</option>
                  @for (s of fromSections(); track s.id) {
                    @if (s.gradeLevelId === fromGradeId) {
                      <option [value]="s.id">{{ s.name }}</option>
                    }
                  }
                </select></label
              >
            } @else {
              <label class="form-field"
                ><span>Buscar</span
                ><input name="q" type="search" [(ngModel)]="term" (keydown.enter)="$event.preventDefault(); search()" maxlength="100"
              /></label>
              <button class="button outline" type="button" (click)="search()">Buscar</button>
            }
          </div>
          @if (candidates().length) {
            <div class="inline-actions">
              <button type="button" class="text-button" (click)="selectAll(true)">Marcar todos ({{ candidates().length }})</button>
              <button type="button" class="text-button" (click)="selectAll(false)">Ninguno</button>
            </div>
            <div class="check-list">
              @for (c of candidates(); track c.id) {
                <label class="check-option"
                  ><input type="checkbox" [name]="'s-' + c.id" [(ngModel)]="chosen[c.id]" /><span
                    >{{ c.name }} <small>{{ c.code }}{{ c.previous ? ' · ' + c.previous : '' }}</small></span
                  ></label
                >
              }
            </div>
          } @else if (searched()) {
            <p class="muted-copy">No hay estudiantes sin inscripción con ese criterio.</p>
          }
          <h2>2. ¿A dónde?</h2>
          <div class="form-grid">
            <label class="form-field"
              ><span>Grado</span
              ><select name="grade" [(ngModel)]="gradeId" (ngModelChange)="sectionId = ''">
                <option value="">Elige un grado</option>
                @for (g of grades(); track g.id) {
                  @if (g.isActive) {
                    <option [value]="g.id">{{ g.name }}</option>
                  }
                }
              </select></label
            >
            <label class="form-field"
              ><span>Sección <small>(opcional)</small></span
              ><select name="section" [(ngModel)]="sectionId">
                <option value="">Sin sección</option>
                @for (s of detail()?.sections ?? []; track s.id) {
                  @if (s.gradeLevelId === gradeId) {
                    <option [value]="s.id">{{ s.name }}{{ s.capacity ? ' (' + s.current + '/' + s.capacity + ')' : '' }}</option>
                  }
                }
              </select></label
            >
          </div>
          <div class="sheet-actions">
            <button class="button" type="button" (click)="enroll()" [disabled]="busy() || !count() || !gradeId">
              {{ busy() ? 'Inscribiendo…' : 'Inscribir ' + count() + ' estudiante' + (count() === 1 ? '' : 's') }}
            </button>
          </div>
        </section>
      } @else {
        <section class="card detail-card">
          <h2>Importar desde Excel</h2>
          <p class="muted-copy">
            Columnas: <strong>Nombres, Apellidos</strong>, Código, Fecha de nacimiento, Grado, Sección. Si el código ya existe se usa esa
            ficha; si la fila trae grado, se inscribe en el ciclo elegido. No crea cuentas, encargados, notas ni cobros.
          </p>
          <div class="inline-actions">
            <a class="button outline" href="/api/v1/enrollments/import/template" download>Descargar plantilla</a>
            <label class="file-picker button"
              ><input type="file" accept=".xlsx,.csv" (change)="pick($event)" [disabled]="busy()" /><span>{{
                file ? file.name : 'Elegir archivo'
              }}</span></label
            >
          </div>
          @if (preview(); as p) {
            <p class="directory-summary">
              {{ p.create }} nuevas fichas · {{ p.existing }} ya registradas · {{ p.enroll }} inscripciones ·
              <strong [class.error]="p.errors">{{ p.errors }} con error</strong>
            </p>
            <div class="table-scroll" tabindex="0" role="region" aria-label="Vista previa de la importación">
              <table class="stack-table import-table">
                <thead>
                  <tr>
                    <th scope="col">Fila</th>
                    <th scope="col">Estudiante</th>
                    <th scope="col">Código</th>
                    <th scope="col">Grado</th>
                    <th scope="col">Resultado</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of p.rows; track r.row) {
                    <tr>
                      <td>{{ r.row }}</td>
                      <td>{{ r.familyNames }}, {{ r.givenNames }}</td>
                      <td>{{ r.studentCode ?? 'Se asigna' }}</td>
                      <td>{{ r.grade ?? '—' }}{{ r.section ? ' · ' + r.section : '' }}</td>
                      <td [class.error]="r.action === 'Error'">{{ actionLabel(r) }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="sheet-actions">
              <button class="button" type="button" (click)="upload(true)" [disabled]="busy() || p.errors > 0 || p.applied">
                {{ p.applied ? 'Importación terminada' : busy() ? 'Importando…' : 'Importar ' + p.rows.length + ' filas' }}
              </button>
            </div>
          }
        </section>
      }
    </app-shell>
  `,
})
export class EnrollmentBulk implements OnInit {
  private readonly api = inject(AccessApi);
  private readonly alerts = inject(Alerts);
  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly detail = signal<YearDetail | null>(null);
  readonly fromSections = signal<YearDetail['sections']>([]);
  readonly candidates = signal<Candidate[]>([]);
  readonly searched = signal(false);
  readonly preview = signal<ImportResult | null>(null);
  readonly tab = signal<'group' | 'import'>('group');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly openYears = computed(() => this.years().filter((y) => y.status === 'EnrollmentOpen' || y.status === 'Active'));
  yearId = '';
  fromYearId = '';
  fromGradeId = '';
  fromSectionId = '';
  term = '';
  gradeId = '';
  sectionId = '';
  chosen: Record<string, boolean> = {};
  file: File | null = null;

  count(): number {
    return this.candidates().filter((c) => this.chosen[c.id]).length;
  }

  async ngOnInit(): Promise<void> {
    try {
      const [years, grades] = await Promise.all([
        this.api.get<YearSummary[]>('/api/v1/academic/years'),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
      ]);
      this.years.set(years);
      this.grades.set(grades);
      const target = this.openYears().find((y) => y.isDefault) ?? this.openYears()[0];
      if (!target) {
        this.error.set('No hay un ciclo con inscripciones abiertas.');
        return;
      }
      this.yearId = target.id;
      const before = years.filter((y) => y.startsOn < target.startsOn).sort((a, b) => b.startsOn.localeCompare(a.startsOn))[0];
      this.fromYearId = before?.id ?? '';
      await this.changeYear();
      await this.fromChanged();
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }

  async changeYear(): Promise<void> {
    this.detail.set(await this.api.get<YearDetail>(`/api/v1/academic/years/${this.yearId}`));
    this.preview.set(null);
    this.candidates.set([]);
    this.searched.set(false);
  }

  async fromChanged(): Promise<void> {
    this.fromGradeId = '';
    this.fromSectionId = '';
    this.candidates.set([]);
    this.searched.set(false);
    this.fromSections.set(this.fromYearId ? (await this.api.get<YearDetail>(`/api/v1/academic/years/${this.fromYearId}`)).sections : []);
  }

  async search(): Promise<void> {
    if (this.fromYearId && !this.fromGradeId) return;
    const q = new URLSearchParams();
    if (this.fromYearId) {
      q.set('fromYearId', this.fromYearId);
      q.set('fromGradeId', this.fromGradeId);
      if (this.fromSectionId) q.set('fromSectionId', this.fromSectionId);
    } else q.set('search', this.term);
    try {
      const list = await this.api.get<Candidate[]>(`/api/v1/years/${this.yearId}/enrollments/candidates?${q}`);
      this.candidates.set(list);
      this.chosen = Object.fromEntries(list.map((c) => [c.id, !!this.fromYearId]));
      this.searched.set(true);
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }

  selectAll(value: boolean): void {
    this.chosen = Object.fromEntries(this.candidates().map((c) => [c.id, value]));
  }

  async enroll(): Promise<void> {
    const ids = this.candidates().filter((c) => this.chosen[c.id]).map((c) => c.id);
    if (!ids.length || !this.gradeId || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const r = await this.api.post<{ enrolled: number; alreadyEnrolled: number; issues: { name: string; message: string }[] }>(
        '/api/v1/enrollments/bulk',
        { academicYearId: this.yearId, gradeLevelId: this.gradeId, sectionId: this.sectionId || null, studentIds: ids, enrolledOn: null, reason: null },
      );
      const issues = r.issues.map((x) => `${x.name}: ${x.message}`).join(' ');
      if (issues) await this.alerts.error(`Se inscribieron ${r.enrolled}. Algunos no:`, issues);
      else this.alerts.success(`Se inscribieron ${r.enrolled} estudiantes.`);
      await this.changeYear();
      await this.search();
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }

  async pick(event: Event): Promise<void> {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.preview.set(null);
    if (this.file) await this.upload(false);
  }

  async upload(apply: boolean): Promise<void> {
    if (!this.file || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const data = new FormData();
      data.append('file', this.file);
      const result = await this.api.upload<ImportResult>(`/api/v1/enrollments/import?yearId=${this.yearId}&apply=${apply}`, data);
      this.preview.set(result);
      if (apply) this.alerts.success(`Importación terminada: ${result.create} fichas nuevas y ${result.enroll} inscripciones.`);
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }

  actionLabel(r: ImportRow): string {
    if (r.message) return r.message;
    return {
      Create: 'Crear ficha',
      CreateAndEnroll: 'Crear ficha e inscribir',
      Existing: 'Ya registrada',
      EnrollExisting: 'Inscribir ficha existente',
      Error: 'Error',
    }[r.action];
  }
}
