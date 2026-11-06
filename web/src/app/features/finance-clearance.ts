import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { distinctUntilChanged, map } from 'rxjs';
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
        <button class="button outline" (click)="load()">Reintentar solvencia</button>
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
            ><select name="cutoff" [(ngModel)]="cutoffId" (ngModelChange)="loadReport()" [disabled]="!setup() || loading()">
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
        @if (loading()) {
          <p class="loading-state" role="status">Calculando solvencia…</p>
        } @else if (!error() && setup() && !setup()!.cutoffs.length) {
          <div class="empty-state">
            <app-icon name="info" />
            <h2>Este ciclo no tiene cortes de solvencia</h2>
            <p>Defínelos en Configuración financiera a partir del calendario de cuotas.</p>
            <a class="button" routerLink="/workspace/finance/setup" [queryParams]="{ year: yearId }">Ir a configuración</a>
          </div>
        } @else if (report(); as r) {
          <div class="preview-summary" aria-live="polite">
            <span class="summary-clear"><strong>{{ r.clear }}</strong> {{ r.clear === 1 ? "solvente" : "solventes" }}</span>
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
        }
      </section>
    </app-shell>
  `,
})
export class FinanceClearance {
  readonly api = inject(AccessApi);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private loadRequest = 0;
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
    this.route.queryParamMap.pipe(
      map(params => JSON.stringify(['year', 'cutoff', 'grade', 'status'].map(key => params.get(key)))),
      distinctUntilChanged(),
      takeUntilDestroyed(),
    ).subscribe(() => void this.load());
  }
  format(value: number): string {
    return money(value, this.setup()?.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  async load(): Promise<void> {
    const request = ++this.loadRequest;
    const query = this.route.snapshot.queryParamMap;
    const requestedYear = query.get('year');
    const requestedCutoff = query.get('cutoff');
    this.loading.set(true);
    this.error.set('');
    this.setup.set(null);
    this.report.set(null);
    this.cutoffId = requestedCutoff ?? '';
    this.gradeId = query.get('grade') ?? '';
    this.status = query.get('status') ?? 'all';
    try {
      if (!this.years().length) {
        const [years, grades] = await Promise.all([
          this.api.get<YearSummary[]>('/api/v1/academic/years'),
          this.api.get<GradeView[]>('/api/v1/academic/grades'),
        ]);
        if (request !== this.loadRequest) return;
        this.years.set(years);
        this.grades.set(grades);
      }
      const selected = requestedYear ? this.years().find(x => x.id === requestedYear)
        : this.years().find(x => x.isDefault) ?? this.years()[0];
      this.yearId = selected?.id ?? requestedYear ?? '';
      if (!selected) {
        if (requestedYear) this.error.set('No encontramos el ciclo solicitado. Selecciona otro ciclo.');
        return;
      }
      const setup = await this.api.get<FinanceSetup>(`/api/v1/finance/years/${selected.id}/setup`);
      if (request !== this.loadRequest) return;
      this.setup.set(setup);
      const cutoff = requestedCutoff ? setup.cutoffs.find(x => x.id === requestedCutoff) : setup.cutoffs.at(-1);
      if (requestedCutoff && !cutoff) {
        this.error.set('El corte solicitado no pertenece a este ciclo. Selecciona otro corte.');
        return;
      }
      this.yearId = selected.id;
      this.cutoffId = cutoff?.id ?? '';
      if (!requestedYear || (cutoff && !requestedCutoff)) {
        await this.router.navigate([], { queryParams: { year: selected.id, cutoff: cutoff?.id ?? null,
          grade: this.gradeId || null, status: this.status === 'all' ? null : this.status }, replaceUrl: true });
        return;
      }
      if (!cutoff) return;
      const filters = new URLSearchParams({ status: this.status });
      if (this.gradeId) filters.set('gradeId', this.gradeId);
      const report = await this.api.get<ClearanceReport>(`/api/v1/finance/cutoffs/${cutoff.id}/report?${filters}`);
      if (request === this.loadRequest) this.report.set(report);
    } catch (error) {
      if (request === this.loadRequest) this.error.set(this.api.message(error));
    } finally {
      if (request === this.loadRequest) this.loading.set(false);
    }
  }
  async changeYear(): Promise<void> {
    await this.router.navigate([], { queryParams: { year: this.yearId } });
  }
  async loadReport(): Promise<void> {
    const navigated = await this.router.navigate([], { queryParams: { year: this.yearId, cutoff: this.cutoffId || null,
      grade: this.gradeId || null, status: this.status === 'all' ? null : this.status } });
    if (!navigated) await this.load();
  }
}
