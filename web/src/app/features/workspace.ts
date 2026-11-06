import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccessApi } from '../core/access-api';
import { Dashboard, DashboardPeriod } from '../core/dashboard';
import { FeedItem } from './announcements';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';
import { money } from '../core/finance-api';
import { yearStatusLabels } from '../core/academic-api';
import { SchoolCalendar } from '../shared/school-calendar';
import { SetupGuide } from './setup-guide';

interface Attention {
  key: string;
  icon: string;
  title: string;
  detail: string;
  route: string;
  action: string;
  tone: string;
}
interface HomeActivity {
  key: string;
  title: string;
  detail: string;
  date: string;
  route: string[];
  fragment?: string;
  overdue: boolean;
  kind: string;
}
interface HomeAction {
  label: string;
  icon: string;
  route: string[];
  fragment?: string;
}
@Component({
  imports: [Shell, Icon, RouterLink, CyclePipe, FormsModule, SchoolCalendar, SetupGuide],
  templateUrl: './workspace.html',
})
export class Workspace {
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  readonly api = inject(AccessApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly stats = signal<Dashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly feed = signal<FeedItem[]>([]);
  readonly feedError = signal('');
  readonly unread = computed(() => this.feed().filter((x) => !x.read).length);
  readonly activityDate = signal('');
  readonly self = computed(() => this.stats()?.students.find((s) => s.isSelf));
  readonly familyStudents = computed(() => this.stats()?.students.filter((s) => !s.isSelf) ?? []);
  /** Sum of the balances the family may see (finance faculty); null when none of the links includes finance. */
  readonly familyBalance = computed(() => {
    const visible = this.familyStudents().filter((s) => s.canFinance && s.balance !== null);
    return visible.length ? visible.reduce((sum, s) => sum + (s.balance ?? 0), 0) : null;
  });
  readonly personalCourseCount = computed(() =>
    this.self()
      ? (this.self()!.courses ?? '—')
      : this.stats()?.teaching.length || this.familyStudents().length,
  );
  readonly personalPending = computed(() =>
    this.stats()?.teaching.length
      ? this.reviews()
      : this.stats()?.students.some((s) => s.pendingTasks !== null)
        ? this.pendingTasks()
        : '—',
  );
  readonly activities = computed<HomeActivity[]>(() => {
    const st = this.stats();
    if (!st) return [];
    const items: HomeActivity[] = [];
    for (const student of st.students)
      for (const task of student.tasks)
        items.push({
          key: student.id + task.title + task.dueAt,
          title: task.title,
          detail: (student.isSelf ? '' : student.name + ' · ') + task.course,
          date: this.localDate(task.dueAt),
          route: ['/workspace/my/students', student.id],
          fragment: 'tasks-title',
          overdue: task.overdue,
          kind: 'Tarea',
        });
    for (const course of st.teaching)
      for (const task of course.upcoming)
        items.push({
          key: course.id + task.title + task.dueAt,
          title: task.title,
          detail: course.subject,
          date: this.localDate(task.dueAt),
          route: ['/workspace/courses', course.id, 'assignments'],
          overdue: false,
          kind: 'Entrega',
        });
    for (const period of st.academic?.periods ?? [])
      if (!period.published && period.endsOn >= st.on)
        items.push({
          key: period.periodId,
          title: 'Cierre de ' + period.name,
          detail: 'Período académico',
          date: period.endsOn,
          route: ['/workspace/grades'],
          overdue: false,
          kind: 'Período',
        });
    return items.sort((a, b) => a.date.localeCompare(b.date));
  });
  readonly eventDates = computed(() => this.activities().map((a) => a.date));
  readonly visibleActivities = computed(() =>
    this.activities()
      .filter((a) => !this.activityDate() || a.date === this.activityDate())
      .slice(0, 3),
  );
  readonly upcomingCount = computed(() => this.activities().filter((a) => !a.overdue).length);
  readonly quickActions = computed<HomeAction[]>(() => {
    const student = this.self();
    if (student)
      return [
        { label: 'Mis clases', icon: 'book', route: ['/workspace'], fragment: 'personal-courses' },
        {
          label: 'Mis tareas',
          icon: 'clipboard',
          route: ['/workspace/my/students', student.id],
          fragment: 'tasks-title',
        },
        {
          label: 'Calificaciones',
          icon: 'chart',
          route: ['/workspace/my/students', student.id],
          fragment: 'grades-title',
        },
        { label: 'Mi cuenta', icon: 'user', route: ['/workspace/security'] },
      ];
    const actions: HomeAction[] = [];
    if (this.session.can('finance.collect'))
      actions.push({ label: 'Registrar pago', icon: 'wallet', route: ['/workspace/finance'] });
    if (this.session.can('enrollment.manage'))
      actions.push({ label: 'Inscripciones', icon: 'school', route: ['/workspace/enrollment'] });
    if (this.stats()?.teaching.length || this.session.can('academic.read'))
      actions.push({ label: 'Cursos', icon: 'book', route: ['/workspace/courses'] });
    if (this.familyStudents()[0])
      actions.push({
        label: 'Seguimiento escolar',
        icon: 'school',
        route: ['/workspace/my/students', this.familyStudents()[0].id],
      });
    if (this.session.can('attendance.gate'))
      actions.push({ label: 'Portería', icon: 'scan', route: ['/workspace/gate'] });
    if (this.session.can('accounts.manage'))
      actions.push({ label: 'Cuentas y accesos', icon: 'shield', route: ['/workspace/access'] });
    actions.push(
      { label: 'Comunicados', icon: 'megaphone', route: ['/workspace/announcements'] },
      { label: 'Mi cuenta', icon: 'user', route: ['/workspace/security'] },
    );
    return actions.slice(0, 4);
  });
  readonly selectedYear = computed(() =>
    this.stats()?.years.find((y) => y.id === this.stats()?.yearId),
  );
  readonly yearStatusLabels = yearStatusLabels;
  readonly reviews = computed(
    () => this.stats()?.teaching.reduce((total, c) => total + c.pendingReviews, 0) ?? 0,
  );
  readonly pendingTasks = computed(
    () => this.stats()?.students.reduce((total, s) => total + (s.pendingTasks ?? 0), 0) ?? 0,
  );
  readonly weeklyTotal = computed(
    () => this.stats()?.finance?.lastSevenDays.reduce((sum, d) => sum + d.amount, 0) ?? 0,
  );
  readonly maxCollection = computed(() =>
    Math.max(1, ...(this.stats()?.finance?.lastSevenDays.map((d) => d.amount) ?? [])),
  );
  readonly published = computed(
    () => this.stats()?.academic?.periods.filter((p) => p.published).length ?? 0,
  );
  readonly hasSchoolSummary = computed(
    () => !!(this.stats()?.academic || this.stats()?.finance || this.stats()?.gate),
  );
  readonly attention = computed<Attention[]>(() => {
    const st = this.stats();
    if (!st) return [];
    const result: Attention[] = [];
    const f = st.finance;
    if (f?.overdue)
      result.push({
        key: 'overdue',
        icon: 'wallet',
        title: this.money(f.overdue, f.currency) + ' en saldo vencido',
        detail: 'Cargos con fecha anterior a hoy, descontando ajustes y pagos confirmados.',
        route: '/workspace/finance',
        action: 'Ver cuentas',
        tone: 'danger',
      });
    if (f?.enrollmentsWithoutCharges)
      result.push({
        key: 'charges',
        icon: 'receipt',
        title:
          f.enrollmentsWithoutCharges +
          (f.enrollmentsWithoutCharges === 1
            ? ' inscripción sin cargos registrados'
            : ' inscripciones sin cargos registrados'),
        detail: 'Revisa si falta emitir obligaciones. Esto no determina la solvencia.',
        route: '/workspace/finance/setup',
        action: 'Revisar cargos',
        tone: 'warning',
      });
    const a = st.academic;
    if (a?.unassignedCourses)
      result.push({
        key: 'teachers',
        icon: 'school',
        title:
          a.unassignedCourses +
          (a.unassignedCourses === 1
            ? ' curso sin docente asignado'
            : ' cursos sin docente asignado'),
        detail: 'No tienen una asignación docente sin finalizar.',
        route: '/workspace/courses',
        action: 'Revisar cursos',
        tone: 'warning',
      });
    const ended =
      a?.periods.filter((p) => !p.published && p.expected > 0 && p.endsOn < st.on) ?? [];
    for (const p of ended)
      result.push({
        key: p.periodId,
        icon: 'book',
        title:
          p.name +
          ': ' +
          (p.recorded < p.expected
            ? `${p.expected - p.recorded} registros por completar`
            : 'listo para revisar'),
        detail:
          'El período terminó y aún no está publicado. Coordinación revisa antes de publicar.',
        route: '/workspace/grades',
        action: 'Revisar período',
        tone: 'warning',
      });
    return result;
  });
  readonly greetingName = computed(
    () => this.session.displayName().split(' ')[0] || this.session.username(),
  );
  private request = 0;
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const year = params.get('year');
      if (year && year === this.stats()?.yearId && !this.error()) return;
      void this.load(year);
    });
    void this.loadFeed();
  }
  async load(year = this.route.snapshot.queryParamMap.get('year')): Promise<void> {
    const request = ++this.request;
    this.loading.set(true);
    this.error.set('');
    this.stats.set(null);
    try {
      const data = await this.api.get<Dashboard>(
        '/api/v1/dashboard' + (year ? '?year=' + encodeURIComponent(year) : ''),
      );
      if (request !== this.request) return;
      this.stats.set(data);
      this.activityDate.set('');
      if (!year && data.yearId)
        await this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { year: data.yearId },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
    } catch (error) {
      if (request === this.request) this.error.set(this.api.message(error));
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }
  async loadFeed(): Promise<void> {
    this.feedError.set('');
    try {
      this.feed.set(await this.api.get<FeedItem[]>('/api/v1/me/announcements'));
    } catch (error) {
      this.feedError.set(this.api.message(error));
    }
  }
  async changeYear(year: string | null): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { year },
      queryParamsHandling: 'merge',
    });
  }
  money(value: number, currency = this.school.locale.currency): string {
    return money(value, currency, this.school.locale.culture);
  }
  percent(recorded: number, expected: number): number {
    return expected ? Math.min(100, Math.round((recorded / expected) * 100)) : 0;
  }
  day(date: string, weekday = false): string {
    return new Intl.DateTimeFormat(
      this.school.locale.culture,
      weekday
        ? { weekday: 'short', timeZone: 'UTC' }
        : { day: 'numeric', month: 'short', timeZone: 'UTC' },
    ).format(new Date(date + 'T12:00:00Z'));
  }
  dayNumber(date: string): string {
    return String(Number(date.slice(8, 10)));
  }
  localDate(value: string): string {
    const parts = new Intl.DateTimeFormat('en-CA', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      timeZone: this.school.locale.timeZone,
    }).formatToParts(new Date(value));
    const part = (type: string) => parts.find((p) => p.type === type)!.value;
    return `${part('year')}-${part('month')}-${part('day')}`;
  }
  deadline(value: string): string {
    return new Intl.DateTimeFormat(this.school.locale.culture, {
      day: 'numeric',
      month: 'short',
      hour: 'numeric',
      minute: '2-digit',
      timeZone: this.school.locale.timeZone,
    }).format(new Date(value));
  }
  updated(value: string): string {
    return new Intl.DateTimeFormat(this.school.locale.culture, {
      hour: 'numeric',
      minute: '2-digit',
      timeZone: this.school.locale.timeZone,
    }).format(new Date(value));
  }
  periodState(p: DashboardPeriod): string {
    if (p.published) return 'Publicado';
    if (this.stats()!.on < p.startsOn) return 'Próximo';
    return this.stats()!.on > p.endsOn ? 'Por publicar' : 'En captura';
  }
}
