import { Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { StudentAccessView } from '../core/student-api';
import { relationshipLabels } from '../core/people-api';
import { Icon } from './icon';

/** Title and section tabs of the student/family pages (summary, tasks, grades, attendance). */
@Component({
  selector: 'app-student-header',
  imports: [RouterLink, RouterLinkActive, Icon],
  template: `
    <header class="student-hero">
      <div class="student-hero-copy">
        <span class="eyebrow">{{
          view().isSelf ? 'MI ESPACIO ESCOLAR' : 'ESTUDIANTE A MI CARGO'
        }}</span>
        <h1>{{ heading() }}</h1>
        @if (title() && title() !== view().student.displayName) {
          <p class="student-hero-name">{{ view().student.displayName }}</p>
        }
        <p>
          <span class="pill blue">{{ view().student.studentCode }}</span>
          @if (view().placement; as p) {
            <span class="student-hero-place"
              >{{ p.grade }}{{ p.section ? ' · Sección ' + p.section : '' }}</span
            >
          }
          @if (!view().isSelf) {
            <span class="pill purple">{{ relationships[view().student.relationship] }}</span>
          }
        </p>
      </div>
      <ng-content />
    </header>
    <nav class="student-tabs" aria-label="Secciones del estudiante">
      <a
        [routerLink]="['/workspace/my/students', view().student.id]"
        routerLinkActive="active"
        [routerLinkActiveOptions]="{ exact: true }"
        ariaCurrentWhenActive="page"
        ><app-icon name="home" />Resumen</a
      >
      <a
        [routerLink]="['/workspace/my/students', view().student.id, 'tasks']"
        routerLinkActive="active"
        ariaCurrentWhenActive="page"
        ><app-icon name="clipboard" />Tareas</a
      >
      <a
        [routerLink]="['/workspace/my/students', view().student.id, 'grades']"
        routerLinkActive="active"
        ariaCurrentWhenActive="page"
        ><app-icon name="chart" />Calificaciones</a
      >
      <a
        [routerLink]="['/workspace/my/students', view().student.id, 'attendance']"
        routerLinkActive="active"
        ariaCurrentWhenActive="page"
        ><app-icon name="calendar" />Asistencia</a
      >
    </nav>
  `,
})
export class StudentHeader {
  readonly view = input.required<StudentAccessView>();
  readonly title = input('');
  readonly relationships = relationshipLabels;
  heading(): string {
    return this.title() || this.view().student.displayName;
  }
}
