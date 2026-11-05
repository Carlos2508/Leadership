import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { InstitutionService } from '../core/institution';
import { GradeView, YearSummary } from '../core/academic-api';
import { ClearanceReport, FinanceSetup, clearanceLabels, money } from '../core/finance-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  template: `
    <app-shell title="Solvencia">
      <a class="back-link" routerLink="/workspace/finance"><app-icon name="arrow" />Volver a caja</a>
      <div class="page-heading">
        <div>
          <span class="eyebrow">FINANZAS</span>
          <h1>Solvencia por corte</h1>
          <p>
            Calculada en este momento con pagos confirmados. Un saldo vacío sin cargos emitidos no se
            considera solvencia.
          </p>
        </div>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      <section class="card directory-card" [attr.aria-busy]="loading()">
        <form class="filters" (ngSubmit)="loadReport()">
          <label class="select-field"
            ><span>Ciclo</span
            ><select name="year" [(ngModel)]="yearId" (ngModelChange)="changeYear()">
              @for (item of years(); track item.id) {
                <option [value]="item.id">{{ item.label }}</option>
              }
            </select></label
          ><label class="select-field"
            ><span>Corte</span
            ><select name="cutoff" [(ngModel)]="cutoffId" (ngModelChange)="loadReport()">
              <option value="" disabled>Selecciona</option>
              @for (cutoff of setup()?.cutoffs ?? []; track cutoff.id) {
                <option [value]="cutoff.id">{{ cutoff.name }}</option>
              }
            </select></label
          ><label class="select-field"
            ><span>Grado</span
            ><select name="grade" [(ngModel)]="gradeId" (ngModelChange)="loadReport()">
              <option value="">Todos</option>
              @for (grade of grades(); track grade.id) {
                <option [value]="grade.id">{{ grade.name }}</option>
              }
            </select></label
          ><label class="select-field"
            ><span>Estado</span
            ><select name="status" [(ngModel)]="status" (ngModelChange)="loadReport()">
              <option value="all">Todos</option>
              <option value="Clear">Solventes</option>
              <option value="Blocked">Con saldo</option>
              <option value="PendingFinancialSetup">Configuración incompleta</option>
            </select></label
          >
        </form>
        @if (!(setup()?.cutoffs?.length)) {
          <div class="empty-state">
            <app-icon name="info" />
            <h2>Este ciclo no tiene cortes de solvencia</h2>
            <p>Defínelos en Configuración financiera a partir del calendario de cuotas.</p>
            <a class="button" routerLink="/workspace/finance/setup" [queryParams]="{ year: yearId }">Ir a configuración</a>
          </div>
        } @else if (report(); as r) {
          <div class="preview-summary" aria-live="polite">
            <span class="summary-clear"><strong>{{ r.clear }}</strong> solventes</span>
            <span class="summary-blocked"><strong>{{ r.blocked }}</strong> con saldo pendiente</span>
            <span><strong>{{ r.pendingSetup }}</strong> con configuración incompleta</span>
          </div>
          @if (r.rows.length) {
            <div class="table-scroll" tabindex="0" role="region" aria-label="Solvencia por estudiante">
              <table class="stack-table">
                <caption class="sr-only">Solvencia de cada estudiante para {{ r.cutoff }}</caption>
                <thead>
                  <tr>
                    <th scope="col">Estudiante</th>
                    <th scope="col">Grado</th>
                    <th scope="col">Estado</th>
                    <th scope="col">Pendiente</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of r.rows; track row.studentId) {
                    <tr>
                      <td>
                        <a class="person-link" routerLink="/workspace/finance" [queryParams]="{ student: row.studentId }"
                          ><span class="person-copy"><strong>{{ row.studentName }}</strong><small>{{ row.studentCode }}</small></span></a
                        >
                      </td>
                      <td data-label="Grado">{{ row.grade ?? '—' }}{{ row.section ? ' · ' + row.section : '' }}</td>
                      <td data-label="Estado">
                        <span
                          class="pill"
                          [class.mint]="row.status === 'Clear'"
                          [class.suspended]="row.status === 'Blocked'"
                          [class.gold]="row.status === 'PendingFinancialSetup'"
                          >{{ labels[row.status] }}</span
                        >
                      </td>
                      <td data-label="Pendiente">
                        {{
                          row.status === 'PendingFinancialSetup'
                            ? row.missingPeriods + ' cuotas sin emitir'
                            : row.pendingBalance
                              ? format(row.pendingBalance)
                              : '—'
                        }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="empty-inline padded">No hay estudiantes con estos filtros.</p>
          }
        } @else if (loading()) {
          <p class="loading-state" role="status">Calculando solvencia…</p>
        }
      </section>
    </app-shell>
  `,
})
export class FinanceClearance {
  readonly year = input<string | undefined>();
  readonly cutoff = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly setup = signal<FinanceSetup | null>(null);
  readonly report = signal<ClearanceReport | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly labels = clearanceLabels;
  yearId = '';
  cutoffId = '';
  gradeId = '';
  status = 'all';

  constructor() {
    queueMicrotask(() => void this.init());
  }
  format(value: number): string {
    return money(value, this.setup()?.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  async init(): Promise<void> {
    try {
      const [years, grades] = await Promise.all([
        this.api.get<YearSummary[]>('/api/v1/academic/years'),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
      ]);
      this.years.set(years);
      this.grades.set(grades);
      const selected = years.find((x) => x.id === this.year()) ?? years.find((x) => x.isDefault) ?? years[0];
      if (!selected) return;
      this.yearId = selected.id;
      await this.loadSetup(this.cutoff());
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }
  async changeYear(): Promise<void> {
    this.report.set(null);
    await this.loadSetup();
  }
  private async loadSetup(preferred?: string): Promise<void> {
    const setup = await this.api.get<FinanceSetup>(`/api/v1/finance/years/${this.yearId}/setup`);
    this.setup.set(setup);
    this.cutoffId = setup.cutoffs.find((x) => x.id === preferred)?.id ?? setup.cutoffs.at(-1)?.id ?? '';
    await this.loadReport();
  }
  async loadReport(): Promise<void> {
    if (!this.cutoffId) {
      this.report.set(null);
      return;
    }
    this.loading.set(true);
    this.error.set('');
    try {
      await this.router.navigate([], { queryParams: { year: this.yearId, cutoff: this.cutoffId } });
      const query = new URLSearchParams({ status: this.status });
      if (this.gradeId) query.set('gradeId', this.gradeId);
      this.report.set(await this.api.get<ClearanceReport>(`/api/v1/finance/cutoffs/${this.cutoffId}/report?${query}`));
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }
}
