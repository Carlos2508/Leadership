import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccessApi, PageResult } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { GradeView, YearSummary } from '../core/academic-api';
import { Dashboard } from '../core/dashboard';
import { Icon } from '../shared/icon';

interface Step {
  key: string;
  title: string;
  detail: string;
  done: boolean;
  link: string;
  query?: Record<string, string>;
  action: string;
}

/**
 * First steps of a new school on Inicio: what to prepare, in order, with a direct link to each screen. It only reads
 * lists the account can already see; nothing is changed from here. Disappears when the school is running.
 */
@Component({
  selector: 'app-setup-guide',
  imports: [RouterLink, Icon],
  template: `
    @if (visible()) {
      <section class="card detail-card setup-guide first-steps" aria-labelledby="first-steps-title">
        <div class="section-heading">
          <div>
            <span class="eyebrow">PRIMEROS PASOS</span>
            <h2 id="first-steps-title">Prepara el colegio en {{ steps().length }} pasos</h2>
            <p class="muted-copy">Sigue el orden; cada paso abre la pantalla donde se hace.</p>
          </div>
          <span class="pill" [class.mint]="done() === steps().length"
            >{{ done() }} de {{ steps().length }}</span
          >
        </div>
        <ol class="setup-steps">
          @for (step of steps(); track step.key; let i = $index) {
            <li [class.done]="step.done" [class.next]="step.key === next()?.key">
              <span class="setup-mark" aria-hidden="true">
                @if (step.done) {
                  <app-icon name="check" />
                } @else {
                  {{ i + 1 }}
                }
              </span>
              <span class="period-copy"
                ><strong>{{ step.title }}</strong
                ><small>{{ step.detail }}</small
                ><span class="sr-only">{{ step.done ? 'Listo' : 'Pendiente' }}</span></span
              >
              @if (!step.done) {
                <a
                  class="button compact-button"
                  [class.outline]="step.key !== next()?.key"
                  [routerLink]="step.link"
                  [queryParams]="step.query"
                  >{{ step.action }}</a
                >
              }
            </li>
          }
        </ol>
        <button class="text-button first-steps-hide" type="button" (click)="hide()">
          Ocultar esta guía
        </button>
      </section>
    }
  `,
})
export class SetupGuide {
  readonly stats = input<Dashboard | null>(null);
  private readonly api = inject(AccessApi);
  private readonly session = inject(SessionService);
  private readonly school = inject(InstitutionService).current;
  private readonly hidden = signal(false);
  private readonly data = signal<{
    accounts: number | null;
    grades: GradeView[] | null;
    years: YearSummary[] | null;
    subjects: number | null;
    courses: { teachers: unknown[] }[] | null;
    students: number | null;
    periods: number | null;
  } | null>(null);

  readonly steps = computed<Step[]>(() => {
    const d = this.data();
    if (!d) return [];
    const can = (p: string) => this.session.can(p);
    const term = this.school.terms.period.toLowerCase();
    const year = d.years?.find((y) => y.isDefault) ?? d.years?.[0];
    const steps: Step[] = [];
    if (can('accounts.manage') && !can('academic.manage') && !can('people.manage'))
      steps.push({
        key: 'role',
        title: 'Tu acceso para preparar el colegio',
        detail:
          'Esta cuenta solo administra accesos. Date el rol de administración o invita a quien dirige el colegio.',
        done: false,
        link: '/workspace/access',
        query: { action: 'my-role' },
        action: 'Cambiar mi rol',
      });
    if (can('accounts.manage') && d.accounts !== null)
      steps.push({
        key: 'team',
        title: 'Cuentas del equipo',
        detail:
          d.accounts > 1 ? `${d.accounts} cuentas creadas` : 'Invita a secretaría, caja y docentes',
        done: d.accounts > 1,
        link: '/workspace/access',
        query: { action: 'invite' },
        action: 'Invitar',
      });
    if (d.grades !== null)
      steps.push({
        key: 'grades',
        title: 'Grados del colegio',
        detail: d.grades.length
          ? `${d.grades.length} grados`
          : 'Ej. Primero primaria, Segundo primaria…',
        done: d.grades.length > 0,
        link: '/workspace/academic',
        action: 'Registrar grados',
      });
    if (d.years !== null)
      steps.push({
        key: 'year',
        title: `Ciclo escolar y ${term}s`,
        detail: year
          ? `${year.label}${d.periods ? ` · ${d.periods} ${term}s` : ` · sin ${term}s`}`
          : `Fechas del año y sus ${term}s`,
        done: !!year && !!d.periods,
        link: '/workspace/academic',
        action: year ? `Crear ${term}s` : 'Crear ciclo',
      });
    if (d.subjects !== null && d.courses !== null) {
      const missing = d.courses.filter((c) => !c.teachers.length).length;
      steps.push({
        key: 'courses',
        title: 'Materias, cursos y docentes',
        detail: d.courses.length
          ? missing
            ? `${d.courses.length} cursos · ${missing} sin docente`
            : `${d.courses.length} cursos con docente`
          : d.subjects
            ? `${d.subjects} materias · falta crear los cursos`
            : 'Registra las materias y crea los cursos de cada grado',
        done: d.courses.length > 0 && missing === 0,
        link: '/workspace/courses',
        action: d.courses.length ? 'Asignar docentes' : 'Crear cursos',
      });
    }
    if (d.students !== null)
      steps.push({
        key: 'students',
        title: 'Estudiantes y familias',
        detail: d.students
          ? `${d.students} estudiantes registrados`
          : 'Registra a cada estudiante y a sus padres o encargados',
        done: d.students > 0,
        link: '/workspace/students',
        action: 'Registrar',
      });
    if (d.years !== null && can('enrollment.manage'))
      steps.push({
        key: 'enrollment',
        title: 'Inscripciones',
        detail: year?.enrollments
          ? `${year.enrollments} inscritos`
          : 'Abre inscripciones en el ciclo e inscribe a cada estudiante',
        done: !!year?.enrollments,
        link: '/workspace/enrollment',
        action: 'Inscribir',
      });
    const finance = this.stats()?.finance;
    if (can('finance.charges') && finance && year)
      steps.push({
        key: 'fees',
        title: 'Cuotas y cargos',
        detail:
          finance.enrollmentsWithoutCharges > 0 || !year.enrollments
            ? 'Define la colegiatura y emite los cargos del ciclo'
            : 'Cargos emitidos a todas las inscripciones',
        done: !!year.enrollments && finance.enrollmentsWithoutCharges === 0,
        link: '/workspace/finance/setup',
        action: 'Configurar',
      });
    return steps;
  });
  readonly done = computed(() => this.steps().filter((x) => x.done).length);
  readonly next = computed(() => this.steps().find((x) => !x.done));
  /** Only while the school is still being prepared (no enrollments yet or a pending step before enrollment). */
  readonly visible = computed(() => {
    if (this.hidden() || !this.steps().length || !this.next()) return false;
    const d = this.data();
    const year = d?.years?.find((y) => y.isDefault) ?? d?.years?.[0];
    return !year || !year.enrollments || this.steps().some((s) => s.key === 'role');
  });

  constructor() {
    queueMicrotask(() => {
      this.hidden.set(localStorage.getItem(this.key()) === '1');
      if (!this.hidden()) void this.load();
    });
  }

  hide(): void {
    this.hidden.set(true);
    try {
      localStorage.setItem(this.key(), '1');
    } catch {
      /* Private mode: shown again on the next visit. */
    }
  }

  private key(): string {
    return 'sp.setup-guide.hidden.' + this.session.userId();
  }

  private async load(): Promise<void> {
    const can = (p: string) => this.session.can(p);
    const get = <T>(url: string, allowed: boolean): Promise<T | null> =>
      allowed ? this.api.get<T>(url).catch(() => null) : Promise.resolve(null);
    const [accounts, grades, years, subjects, students] = await Promise.all([
      get<PageResult<unknown>>('/api/v1/access/accounts?page=1', can('accounts.manage')),
      get<GradeView[]>('/api/v1/academic/grades', can('academic.read')),
      get<YearSummary[]>('/api/v1/academic/years', can('academic.read')),
      get<{ id: string }[]>('/api/v1/academic/subjects', can('academic.read')),
      get<PageResult<unknown>>(
        '/api/v1/people?role=student&status=Active&page=1',
        can('people.read'),
      ),
    ]);
    const year = years?.find((y) => y.isDefault) ?? years?.[0];
    const [courses, detail] = year
      ? await Promise.all([
          get<{ teachers: unknown[] }[]>(`/api/v1/years/${year.id}/courses`, true),
          get<{ periods: unknown[] }>(`/api/v1/academic/years/${year.id}`, true),
        ])
      : [years ? [] : null, null];
    this.data.set({
      accounts: accounts?.total ?? null,
      grades,
      years,
      subjects: subjects?.length ?? null,
      courses: courses ?? (years ? [] : null),
      students: students?.total ?? null,
      periods: detail?.periods.length ?? (years ? 0 : null),
    });
  }
}
