import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { CourseView } from './courses';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

interface Roster {
  course: CourseView;
  on: string;
  students: { studentId: string; name: string; code: string; section: string | null; from: string; until: string | null }[];
}

/** Course roster on a chosen date, for academic staff and for the teacher with an active assignment. */
@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  template: `
    <app-shell [title]="roster()?.course?.subject ?? 'Curso'">
      <a class="back-link" [routerLink]="session.can('academic.read') ? '/workspace/courses' : '/workspace'"
        ><app-icon name="arrow" />Volver</a
      >
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a este curso</h2>
          <p>Solo el personal académico y los docentes asignados pueden ver su lista.</p>
        </section>
      } @else if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      } @else if (roster(); as r) {
        <div class="page-heading">
          <div>
            <span class="eyebrow">CICLO {{ r.course.year }}</span>
            <h1>{{ r.course.subject }}</h1>
            <p>
              {{ r.course.grade }} · {{ r.course.section ? 'Sección ' + r.course.section : 'Todo el grado' }}
              @for (teacher of r.course.teachers; track teacher.assignmentId) {
                · {{ teacher.name }}
              }
            </p>
          </div>
          <div class="inline-actions">
            <a class="button outline" [routerLink]="['/workspace/courses', r.course.id, 'attendance']"><app-icon name="check" />Asistencia</a>
            <a class="button outline" [routerLink]="['/workspace/courses', r.course.id, 'assignments']"><app-icon name="book" />Actividades</a>
            <a class="button" [routerLink]="['/workspace/courses', r.course.id, 'grades']"><app-icon name="star" />Libreta</a>
          </div>
        </div>
        <section class="card directory-card">
          <form class="filters" (ngSubmit)="load()">
            <label class="select-field date-field"
              ><span>Lista vigente al</span><input type="date" name="on" [(ngModel)]="on" (change)="load()"
            /></label>
          </form>
          <p class="directory-summary" aria-live="polite">
            {{ r.students.length }} {{ r.students.length === 1 ? 'estudiante' : 'estudiantes' }} al {{ date(r.on) }}
          </p>
          @if (r.students.length) {
            <div class="table-scroll" tabindex="0" role="region" aria-label="Lista del curso">
              <table class="stack-table">
                <caption class="sr-only">Estudiantes del curso en la fecha elegida</caption>
                <thead>
                  <tr>
                    <th scope="col">#</th>
                    <th scope="col">Estudiante</th>
                    <th scope="col">Sección</th>
                    <th scope="col">Desde</th>
                  </tr>
                </thead>
                <tbody>
                  @for (student of r.students; track student.studentId; let i = $index) {
                    <tr>
                      <td data-label="#">{{ i + 1 }}</td>
                      <td>
                        <span class="person-copy"><strong>{{ student.name }}</strong><small>{{ student.code }}</small></span>
                      </td>
                      <td data-label="Sección">{{ student.section ?? 'Sin sección' }}</td>
                      <td data-label="Desde">{{ date(student.from) }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="empty-inline padded">No hay estudiantes en el curso en esa fecha.</p>
          }
        </section>
      } @else {
        <p class="loading-state card" role="status">Cargando lista…</p>
      }
    </app-shell>
  `,
})
export class CourseRoster {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  readonly roster = signal<Roster | null>(null);
  readonly notFound = signal(false);
  readonly error = signal('');
  on = '';

  constructor() {
    queueMicrotask(() => void this.load());
  }
  date(value: string | null): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  async load(): Promise<void> {
    try {
      const query = this.on ? `?on=${this.on}` : '';
      const roster = await this.api.get<Roster>(`/api/v1/courses/${encodeURIComponent(this.id())}/roster${query}`);
      this.roster.set(roster);
      this.on = roster.on;
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
}
