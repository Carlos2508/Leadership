import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { InstitutionService } from '../core/institution';
import { PreviewService } from './preview-data';
@Component({
  imports: [Shell, Icon, RouterLink],
  template: `
    <app-shell [preview]="true" title="Administración">
      <div class="page-heading">
        <div>
          <span class="eyebrow">PANEL DE DIRECCIÓN</span>
          <h1>Resumen del colegio</h1>
          <p>Una mirada a tu comunidad, para acompañar cada paso.</p>
        </div>
        <span class="date-label"
          ><app-icon name="calendar" />Ciclo de muestra {{ data.data()?.academicYear }}</span
        >
      </div>
      @if (data.error()) {
        <section class="card empty-state" role="alert">
          <h2>No pudimos cargar la muestra</h2>
          <button class="button" (click)="data.load()">Reintentar</button>
        </section>
      } @else if (data.data(); as sample) {
        <div class="stats-grid">
          <article class="card stat-card">
            <div class="stat-top">
              <span class="icon-tile mint"><app-icon name="school" /></span
              ><span class="stat-note">Datos de muestra</span>
            </div>
            <strong class="stat-number">{{ sample.students.length }}</strong
            ><span>{{ school.terms.studentPlural }} de muestra</span>
          </article>
          <article class="card stat-card">
            <div class="stat-top">
              <span class="icon-tile blue"><app-icon name="check" /></span
              ><span class="stat-note">Último mes</span>
            </div>
            <strong class="stat-number">95<span>%</span></strong
            ><span>Asistencia de muestra</span>
          </article>
          <article class="card stat-card">
            <div class="stat-top">
              <span class="icon-tile gold"><app-icon name="star" /></span
              ><span class="stat-note">Ejemplo visual</span>
            </div>
            <strong class="stat-number">86.4</strong><span>Promedio de muestra</span>
          </article>
          <article class="card stat-card">
            <div class="stat-top">
              <span class="icon-tile purple"><app-icon name="book" /></span
              ><span class="stat-note">Ejemplo visual</span>
            </div>
            <strong class="stat-number">3</strong><span>Grados de muestra</span>
          </article>
        </div>
        <div class="dashboard-grid">
          <section class="card chart-card">
            <div class="card-heading">
              <div>
                <h2>Evolución de asistencia</h2>
                <p>Cada día en clase abre nuevas oportunidades.</p>
              </div>
              <span class="pill mint">Datos ficticios</span>
            </div>
            <div class="chart-legend">
              <span><i></i>Asistencia</span><span><i class="reference"></i>Referencia: 100 %</span>
            </div>
            <div class="chart" role="img" [attr.aria-label]="chartDescription()">
              <div class="chart-axis"><span>100 %</span><span>50 %</span><span>0 %</span></div>
              <div class="chart-bars">
                @for (month of sample.attendance; track month.month) {
                  <div class="chart-column">
                    <div class="bar-track">
                      <div class="bar-value" [style.height.%]="month.value">
                        <span>{{ month.value }} %</span>
                      </div>
                    </div>
                    <span class="bar-label">{{ month.month }}</span>
                  </div>
                }
              </div>
            </div>
          </section>
          <section class="card agenda-card">
            <div class="card-heading">
              <div>
                <h2>Lo importante hoy</h2>
                <p>Una agenda de ejemplo.</p>
              </div>
              <app-icon name="calendar" />
            </div>
            <article class="agenda-item">
              <span class="icon-tile gold"><app-icon name="book" /></span>
              <div>
                <strong>Entrega de reportes</strong>
                <p>Coordinación académica</p>
                <span class="pill gold">Hoy · Muestra</span>
              </div>
            </article>
            <article class="agenda-item">
              <span class="icon-tile blue"><app-icon name="people" /></span>
              <div>
                <strong>Encuentro con familias</strong>
                <p>Salón de reuniones</p>
                <span class="pill blue">Mañana · Muestra</span>
              </div>
            </article>
            <article class="agenda-item">
              <span class="icon-tile purple"><app-icon name="star" /></span>
              <div>
                <strong>Semana cultural</strong>
                <p>Aprender también es crear</p>
                <span class="pill purple">Próximamente · Muestra</span>
              </div>
            </article>
          </section>
        </div>
        <section class="community-banner">
          <div class="icon-tile mint"><app-icon name="people" /></div>
          <div>
            <h2>Detrás de cada número, una historia.</h2>
            <p>Conoce la vista del directorio y prueba sus filtros.</p>
          </div>
          <a class="button outline" routerLink="/preview/students"
            >Ver {{ school.terms.studentPlural.toLowerCase() }} <app-icon name="arrow"
          /></a>
        </section>
      } @else {
        <p role="status" class="loading-state">Cargando muestra…</p>
      }
    </app-shell>
  `,
})
export class PreviewDashboard {
  readonly school = inject(InstitutionService).current;
  readonly data = inject(PreviewService);
  constructor() {
    void this.data.load();
  }
  chartDescription(): string {
    return (
      'Asistencia ficticia: ' +
      (this.data
        .data()
        ?.attendance.map((x) => `${x.month}: ${x.value} %`)
        .join(', ') ?? '')
    );
  }
}
