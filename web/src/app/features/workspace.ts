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

interface Attention {
  key: string;
  icon: string;
  title: string;
  detail: string;
  route: string;
  action: string;
  tone: string;
}
@Component({
  imports: [Shell, Icon, RouterLink, CyclePipe, FormsModule],
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
  readonly greeting = (() => {
    const hour = Number(
      new Intl.DateTimeFormat('en-US', {
        hour: 'numeric',
        hourCycle: 'h23',
        timeZone: this.school.locale.timeZone,
      }).format(new Date()),
    );
    return hour < 12 ? 'Buenos días' : hour < 19 ? 'Buenas tardes' : 'Buenas noches';
  })();
  readonly today = new Intl.DateTimeFormat(this.school.locale.culture, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: this.school.locale.timeZone,
  }).format(new Date());
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
