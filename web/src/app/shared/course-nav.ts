import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { SessionService } from '../core/session';
import { Icon } from './icon';

interface MyCourse {
  id: string;
  subject: string;
  grade: string;
  section: string | null;
}

export type CourseTab = 'roster' | 'attendance' | 'assignments' | 'grades';

/**
 * Same navigation on every course page: the four teaching tools of the course, and a switch to the teacher's other
 * courses keeping the same tool. Only navigation; every page still checks access on the server.
 */
@Component({
  selector: 'app-course-nav',
  imports: [RouterLink, Icon],
  template: `
    <div class="course-nav">
      <a class="back-link" [routerLink]="back()"><app-icon name="arrow" />{{ backLabel() }}</a>
      <nav class="course-tabs" aria-label="Herramientas del curso">
        @for (tab of tabs; track tab.key) {
          <a
            [routerLink]="link(courseId(), tab.key)"
            [class.active]="tab.key === active()"
            [attr.aria-current]="tab.key === active() ? 'page' : null"
            ><app-icon [name]="tab.icon" />{{ tab.label }}</a
          >
        }
      </nav>
      @if (courses().length > 1) {
        <label class="course-switch"
          ><span>Cambiar de curso</span
          ><select [value]="courseId()" (change)="go($any($event.target).value)">
            @for (c of courses(); track c.id) {
              <option [value]="c.id" [selected]="c.id === courseId()">
                {{ c.subject }} · {{ c.grade }}{{ c.section ? ' ' + c.section : '' }}
              </option>
            }
          </select></label
        >
      }
    </div>
  `,
})
export class CourseNav {
  readonly courseId = input.required<string>();
  readonly active = input.required<CourseTab>();
  private readonly api = inject(AccessApi);
  private readonly router = inject(Router);
  private readonly session = inject(SessionService);
  readonly courses = signal<MyCourse[]>([]);
  readonly staff = computed(() => this.session.can('academic.read'));
  readonly tabs: { key: CourseTab; label: string; icon: string }[] = [
    { key: 'roster', label: 'Lista', icon: 'people' },
    { key: 'attendance', label: 'Asistencia', icon: 'check' },
    { key: 'assignments', label: 'Actividades', icon: 'clipboard' },
    { key: 'grades', label: 'Libreta', icon: 'star' },
  ];

  constructor() {
    queueMicrotask(() =>
      this.api
        .get<MyCourse[]>('/api/v1/me/courses')
        .then((list) => this.courses.set(list))
        .catch(() => this.courses.set([])),
    );
  }

  back(): string {
    return this.staff() && !this.courses().some((c) => c.id === this.courseId())
      ? '/workspace/courses'
      : '/workspace';
  }
  backLabel(): string {
    return this.back() === '/workspace' ? 'Mi inicio' : 'Cursos';
  }

  link(id: string, tab: CourseTab): string[] {
    return tab === 'roster' ? ['/workspace/courses', id] : ['/workspace/courses', id, tab];
  }

  go(id: string): void {
    if (id && id !== this.courseId()) void this.router.navigate(this.link(id, this.active()));
  }
}
