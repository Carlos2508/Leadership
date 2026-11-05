import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { InstitutionService } from '../core/institution';
import { PreviewService } from './preview-data';
@Component({
  imports: [Shell, Icon, FormsModule],
  template: `
    <app-shell [preview]="true" [title]="school.terms.studentPlural">
      <div class="page-heading">
        <div>
          <span class="eyebrow">NUESTRA COMUNIDAD</span>
          <h1>{{ school.terms.studentPlural }}</h1>
          <p>Una vista clara para acompañar a cada estudiante.</p>
        </div>
        <span class="pill mint">Directorio de muestra</span>
      </div>
      <section class="card directory-card">
        <div class="filters">
          <label class="search-field"
            ><span>Buscar estudiante</span>
            <div>
              <app-icon name="search" /><input
                type="search"
                placeholder="Nombre o código"
                [ngModel]="query()"
                (ngModelChange)="query.set($event)"
              /></div></label
          ><label class="select-field"
            ><span>Grado</span
            ><select [ngModel]="grade()" (ngModelChange)="grade.set($event)">
              <option value="">Todos los grados</option>
              @for (grade of grades(); track grade) {
                <option [value]="grade">{{ grade }}</option>
              }
            </select></label
          ><label class="select-field"
            ><span>Estado</span
            ><select [ngModel]="status()" (ngModelChange)="status.set($event)">
              <option value="">Todos los estados</option>
              <option>Activo</option>
              <option>Pendiente</option>
            </select></label
          ><button class="text-button reset-filters" (click)="clear()">Limpiar filtros</button>
        </div>
        <div class="directory-summary" aria-live="polite">
          {{ filtered().length }} de {{ data.data()?.students?.length ?? 0 }} estudiantes de muestra
        </div>
        @if (data.error()) {
          <div class="empty-state" role="alert">
            <h2>No pudimos cargar la muestra</h2>
            <button class="button" (click)="data.load()">Reintentar</button>
          </div>
        } @else if (!data.data()) {
          <p role="status" class="loading-state">Cargando directorio…</p>
        } @else if (filtered().length) {
          <div
            class="table-scroll"
            tabindex="0"
            role="region"
            aria-label="Tabla de estudiantes de muestra"
          >
            <table>
              <caption class="sr-only">
                Estudiantes ficticios para revisar el diseño
              </caption>
              <thead>
                <tr>
                  <th scope="col">Estudiante</th>
                  <th scope="col">Código</th>
                  <th scope="col">Grado</th>
                  <th scope="col">Estado</th>
                </tr>
              </thead>
              <tbody>
                @for (student of filtered(); track student.code) {
                  <tr>
                    <td>
                      <div class="student-name">
                        <span class="student-avatar">{{ student.initials }}</span
                        ><strong>{{ student.name }}</strong>
                      </div>
                    </td>
                    <td class="code-cell">{{ student.code }}</td>
                    <td>{{ student.grade }}</td>
                    <td>
                      <span
                        class="pill"
                        [class.mint]="student.status === 'Activo'"
                        [class.gold]="student.status !== 'Activo'"
                        ><span class="status-dot"></span>{{ student.status }}</span
                      >
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <div class="empty-state">
            <app-icon name="search" />
            <h2>No encontramos coincidencias</h2>
            <p>Prueba con otro nombre o ajusta los filtros.</p>
            <button class="button outline" (click)="clear()">Ver todos</button>
          </div>
        }
      </section>
    </app-shell>
  `,
})
export class PreviewStudents {
  readonly school = inject(InstitutionService).current;
  readonly data = inject(PreviewService);
  readonly query = signal('');
  readonly grade = signal('');
  readonly status = signal('');
  readonly grades = computed(() =>
    [...new Set(this.data.data()?.students.map((x) => x.grade) ?? [])].sort(),
  );
  readonly filtered = computed(() => {
    const normalize = (value: string) =>
      value
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .toLowerCase();
    const query = normalize(this.query().trim());
    return (this.data.data()?.students ?? []).filter(
      (x) =>
        normalize(`${x.name} ${x.code}`).includes(query) &&
        (!this.grade() || x.grade === this.grade()) &&
        (!this.status() || x.status === this.status()),
    );
  });
  constructor() {
    void this.data.load();
  }
  clear(): void {
    this.query.set('');
    this.grade.set('');
    this.status.set('');
  }
}
