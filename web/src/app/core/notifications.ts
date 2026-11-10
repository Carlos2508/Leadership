import { Injectable, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { AccessApi } from './access-api';
import { Dashboard } from './dashboard';
import { SessionService } from './session';

interface FeedItem {
  id: string;
  title: string;
  audience: string;
  publishedAt: string;
  author: string;
  read: boolean;
}

export type NotificationKind = 'notice' | 'task' | 'money' | 'teaching' | 'admin';

export interface AppNotification {
  /** Stable key; for pending items it includes the figure so a change shows again as new. */
  key: string;
  kind: NotificationKind;
  title: string;
  detail: string;
  link: string[];
  queryParams?: Record<string, string>;
  fragment?: string;
  /** Announcement id for unread notices (opening marks it as read on the server). */
  noticeId?: string;
  isNew: boolean;
}

const POLL_MS = 120_000;
const MIN_REFRESH_MS = 30_000;

/**
 * Header bell: unread announcements plus pending work derived from the dashboard (already authorized per resource
 * and permission on the server). Nothing here grants access; links still go through route guards and API checks.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly api = inject(AccessApi);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  private readonly feed = signal<FeedItem[]>([]);
  private readonly dashboard = signal<Dashboard | null>(null);
  private readonly seen = signal<Set<string>>(new Set());
  readonly loading = signal(false);
  readonly error = signal('');
  private lastLoad = 0;
  private started = false;
  private userId = '';

  readonly items = computed<AppNotification[]>(() => {
    const seen = this.seen();
    const notices: AppNotification[] = this.feed()
      .filter((x) => !x.read)
      .slice(0, 8)
      .map((x) => ({
        key: 'notice:' + x.id,
        kind: 'notice',
        title: x.title,
        detail: `${x.author} · ${new Date(x.publishedAt).toLocaleDateString('es-GT', { day: 'numeric', month: 'short' })}`,
        link: ['/workspace/announcements'],
        queryParams: { open: x.id },
        noticeId: x.id,
        isNew: true,
      }));
    return [...notices, ...this.pending().map((x) => ({ ...x, isNew: !seen.has(x.key) }))];
  });

  /** Badge: unread notices and pending items the user has not looked at yet. */
  readonly count = computed(() => this.items().filter((x) => x.isNew).length);

  start(): void {
    if (this.started) return;
    this.started = true;
    void this.refresh(true);
    setInterval(() => {
      if (document.visibilityState === 'visible') void this.refresh();
    }, POLL_MS);
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') void this.refresh();
    });
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => void this.refresh());
  }

  async refresh(force = false): Promise<void> {
    const userId = this.session.userId();
    if (!userId || this.loading() || (!force && Date.now() - this.lastLoad < MIN_REFRESH_MS))
      return;
    if (userId !== this.userId) {
      this.userId = userId;
      this.seen.set(this.readSeen(userId));
    }
    this.loading.set(true);
    this.lastLoad = Date.now();
    try {
      const [feed, dashboard] = await Promise.all([
        this.api.get<FeedItem[]>('/api/v1/me/announcements'),
        this.api.get<Dashboard>('/api/v1/dashboard').catch(() => null),
      ]);
      this.feed.set(feed);
      this.dashboard.set(dashboard);
      this.error.set('');
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }

  /** Called when the panel is opened: pending items stop counting until their figures change. */
  markSeen(): void {
    const keys = new Set([...this.seen(), ...this.pending().map((x) => x.key)]);
    this.seen.set(keys);
    try {
      localStorage.setItem(this.storageKey(this.userId), JSON.stringify([...keys].slice(-200)));
    } catch {
      /* Private mode: the badge simply resets on reload. */
    }
  }

  async markNoticeRead(id: string): Promise<void> {
    this.feed.update((list) => list.map((x) => (x.id === id ? { ...x, read: true } : x)));
    try {
      await this.api.post(`/api/v1/me/announcements/${id}/read`, {});
    } catch {
      /* The announcements page retries when it is opened. */
    }
  }

  async markAllNoticesRead(): Promise<void> {
    const unread = this.feed()
      .filter((x) => !x.read)
      .map((x) => x.id);
    for (const id of unread) await this.markNoticeRead(id);
  }

  /** Keeps the bell in sync when the announcements page marks something as read. */
  noticeRead(id: string): void {
    this.feed.update((list) => list.map((x) => (x.id === id ? { ...x, read: true } : x)));
  }

  private pending(): Omit<AppNotification, 'isNew'>[] {
    const d = this.dashboard();
    if (!d) return [];
    const money = (n: number) =>
      'Q ' + n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const result: Omit<AppNotification, 'isNew'>[] = [];
    for (const s of d.students) {
      const overdue = s.tasks.filter((t) => t.overdue).length;
      if (s.canAcademic && (s.pendingTasks ?? 0) > 0)
        result.push({
          key: `task:${s.id}:${s.pendingTasks}:${overdue}`,
          kind: 'task',
          title: s.isSelf
            ? `${s.pendingTasks} ${s.pendingTasks === 1 ? 'tarea pendiente' : 'tareas pendientes'}`
            : `${s.name}: ${s.pendingTasks} ${s.pendingTasks === 1 ? 'tarea pendiente' : 'tareas pendientes'}`,
          detail: overdue
            ? `${overdue} vencida${overdue === 1 ? '' : 's'}`
            : 'Revisa las fechas de entrega',
          link: ['/workspace/my/students', s.id, 'tasks'],
        });
      if (s.canFinance && (s.balance ?? 0) > 0)
        result.push({
          key: `money:${s.id}:${s.balance}`,
          kind: 'money',
          title: `Saldo pendiente de ${s.name}`,
          detail: money(s.balance ?? 0),
          link: ['/workspace/my/students', s.id],
          fragment: 'account-title',
        });
    }
    const weekday = new Date().getDay() % 6 !== 0;
    for (const c of d.teaching) {
      const group = c.section ? `${c.grade} · ${c.section}` : c.grade;
      if (c.pendingReviews > 0)
        result.push({
          key: `review:${c.id}:${c.pendingReviews}`,
          kind: 'teaching',
          title: `${c.pendingReviews} ${c.pendingReviews === 1 ? 'entrega por revisar' : 'entregas por revisar'}`,
          detail: `${c.subject} · ${group}`,
          link: ['/workspace/courses', c.id, 'assignments'],
        });
      if (weekday && c.students > 0 && c.attendanceRecorded === 0)
        result.push({
          key: `attendance:${c.id}:${d.on}`,
          kind: 'teaching',
          title: 'Asistencia de hoy sin registrar',
          detail: `${c.subject} · ${group}`,
          link: ['/workspace/courses', c.id, 'attendance'],
        });
    }
    if (d.access && d.access.pendingActivation > 0)
      result.push({
        key: `access:${d.access.pendingActivation}`,
        kind: 'admin',
        title: `${d.access.pendingActivation} ${d.access.pendingActivation === 1 ? 'cuenta sin activar' : 'cuentas sin activar'}`,
        detail: 'Entrega o renueva el código de acceso',
        link: ['/workspace/access'],
      });
    if (d.finance && d.finance.overdue > 0)
      result.push({
        key: `overdue:${d.finance.overdue}:${d.finance.studentsWithBalance}`,
        kind: 'money',
        title: `${money(d.finance.overdue)} en saldo vencido`,
        detail: `${d.finance.studentsWithBalance} estudiante${d.finance.studentsWithBalance === 1 ? '' : 's'} con saldo`,
        link: ['/workspace/finance'],
      });
    if (d.finance && d.finance.enrollmentsWithoutCharges > 0)
      result.push({
        key: `nocharges:${d.finance.enrollmentsWithoutCharges}`,
        kind: 'admin',
        title: `${d.finance.enrollmentsWithoutCharges} inscripción${d.finance.enrollmentsWithoutCharges === 1 ? '' : 'es'} sin cargos`,
        detail: 'Revisa la configuración de cuotas del ciclo',
        link: ['/workspace/finance/setup'],
      });
    // Same rules as «Requiere atención» on the dashboard, so both always agree.
    if (d.academic)
      for (const p of d.academic.periods.filter(
        (x) => !x.published && x.expected > 0 && x.endsOn < d.on,
      ))
        result.push({
          key: `period:${p.periodId}:${p.recorded}:${p.expected}`,
          kind: 'teaching',
          title: `${p.name}: ${p.recorded < p.expected ? `${p.expected - p.recorded} registros por completar` : 'listo para revisar'}`,
          detail: 'El período terminó y aún no está publicado',
          link: ['/workspace/grades'],
        });
    if (d.academic && d.academic.unassignedCourses > 0)
      result.push({
        key: `unassigned:${d.academic.unassignedCourses}`,
        kind: 'admin',
        title: `${d.academic.unassignedCourses} curso${d.academic.unassignedCourses === 1 ? '' : 's'} sin docente`,
        detail: 'Asigna docentes para habilitar libreta y asistencia',
        link: ['/workspace/courses'],
      });
    return result;
  }

  private storageKey(userId: string): string {
    return 'sp.notifications.seen.' + userId;
  }

  private readSeen(userId: string): Set<string> {
    try {
      return new Set(JSON.parse(localStorage.getItem(this.storageKey(userId)) ?? '[]') as string[]);
    } catch {
      return new Set();
    }
  }
}
