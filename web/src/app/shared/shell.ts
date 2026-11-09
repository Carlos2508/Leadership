import {
  Component,
  ElementRef,
  HostListener,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { InstitutionService } from '../core/institution';
import { Icon } from './icon';
import { SessionService } from '../core/session';
import { AppNotification, NotificationKind, NotificationService } from '../core/notifications';
import { ChatInbox } from '../core/chat';
@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, Icon],
  template: `
    <a class="skip-link" href="#content">Saltar al contenido</a>
    <div class="workspace-layout">
      <aside
        class="sidebar"
        [class.mobile-open]="menuOpen()"
        id="navigation"
        (keydown)="onMenuKeydown($event)"
      >
        <a class="brand" routerLink="/"
          ><img [src]="school.assets.mainLogo" alt="" /><span
            ><strong>{{ school.public.heroTitle || school.displayName }}</strong
            ><small>{{ school.campusLabel }}</small></span
          ></a
        >
        <button class="mobile-close icon-button" (click)="closeMenu()" aria-label="Cerrar menú">
          <app-icon name="close" />
        </button>
        <div class="nav-label">{{ preview() ? 'ESPACIO DE ADMINISTRACIÓN' : 'MI ESPACIO' }}</div>
        <nav aria-label="Navegación principal" (click)="onNavigationClick($event)">
          <a
            [routerLink]="preview() ? '/preview' : '/workspace'"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{
              paths: 'exact',
              queryParams: 'ignored',
              matrixParams: 'ignored',
              fragment: 'exact',
            }"
            ariaCurrentWhenActive="page"
            ><app-icon name="home" />Inicio</a
          >
          @if (!preview() && session.studentId(); as studentId) {
            <a
              routerLink="/workspace"
              fragment="personal-courses"
              queryParamsHandling="preserve"
              [class.active]="router.url.endsWith('#personal-courses')"
              ><app-icon name="school" />Académico</a
            >
            <a
              [routerLink]="['/workspace/my/students', studentId]"
              fragment="tasks-title"
              queryParamsHandling="preserve"
              [class.active]="router.url.endsWith('#tasks-title')"
              ><app-icon name="clipboard" />Tareas</a
            >
            <a
              [routerLink]="['/workspace/my/students', studentId]"
              fragment="grades-title"
              queryParamsHandling="preserve"
              [class.active]="router.url.endsWith('#grades-title')"
              ><app-icon name="chart" />Calificaciones</a
            >
            <a
              [routerLink]="['/workspace/my/students', studentId]"
              fragment="attendance-title"
              queryParamsHandling="preserve"
              [class.active]="router.url.endsWith('#attendance-title')"
              ><app-icon name="calendar" />Asistencia</a
            >
          }
          @if (!preview() && !session.studentId() && session.myStudents().length) {
            <span class="nav-group" aria-hidden="true">MIS ESTUDIANTES</span>
            @for (student of session.myStudents(); track student.id) {
              <a
                [routerLink]="['/workspace/my/students', student.id]"
                routerLinkActive="active"
                ariaCurrentWhenActive="page"
                ><app-icon name="people" /><span class="nav-text">{{
                  student.displayName
                }}</span></a
              >
            }
          }
          @if (!preview() && session.myCourses().length) {
            <span class="nav-group" aria-hidden="true">DOCENCIA</span>
            @if (session.myCourses().length <= 6) {
              @for (course of session.myCourses(); track course.id) {
                <a
                  [routerLink]="['/workspace/courses', course.id]"
                  routerLinkActive="active"
                  ariaCurrentWhenActive="page"
                  ><app-icon name="book" /><span class="nav-text"
                    >{{ course.subject
                    }}<small
                      >{{ course.grade }}{{ course.section ? ' · ' + course.section : '' }}</small
                    ></span
                  ></a
                >
              }
            } @else {
              <!-- Many courses (homeroom teachers): grouped by grade and section, every course reachable. -->
              @for (group of teachingGroups(); track group.label) {
                <span class="nav-subgroup" aria-hidden="true">{{ group.label }}</span>
                @for (course of group.courses; track course.id) {
                  <a
                    class="nav-compact"
                    [routerLink]="['/workspace/courses', course.id]"
                    routerLinkActive="active"
                    ariaCurrentWhenActive="page"
                    [attr.aria-label]="course.subject + ', ' + group.label"
                    ><app-icon name="book" /><span class="nav-text">{{ course.subject }}</span></a
                  >
                }
              }
            }
          }
          @if (!preview()) {
            <a
              routerLink="/workspace/announcements"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="megaphone" />{{ session.studentId() ? 'Comunicados' : 'Avisos' }}</a
            >
          }
          @if (preview()) {
            <a routerLink="/preview/students" routerLinkActive="active" ariaCurrentWhenActive="page"
              ><app-icon name="people" />{{ school.terms.studentPlural }}</a
            >
          }
          @if (!preview() && (session.can('people.read') || session.can('academic.read'))) {
            <span class="nav-group" aria-hidden="true">COLEGIO</span>
          }
          @if (!preview() && session.can('people.read')) {
            <a
              routerLink="/workspace/students"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="people" />Estudiantes</a
            >
            <a
              routerLink="/workspace/parents"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="users" />Padres</a
            >
            <a
              routerLink="/workspace/teachers"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="school" />Docentes</a
            >
          }
          @if (!preview() && session.can('academic.read')) {
            <a
              routerLink="/workspace/enrollment"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="school" />Inscripciones</a
            >
            <a
              routerLink="/workspace/courses"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="book" />Cursos</a
            >
            <a routerLink="/workspace/grades" routerLinkActive="active" ariaCurrentWhenActive="page"
              ><app-icon name="star" />Calificaciones</a
            >
            <a
              routerLink="/workspace/academic"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="calendar" />Ciclo escolar</a
            >
          }
          @if (
            !preview() &&
            (session.can('attendance.gate') ||
              session.can('attendance.read') ||
              session.can('attendance.manage'))
          ) {
            <details class="nav-section" [open]="router.url.startsWith('/workspace/gate')">
              <summary>
                <app-icon name="scan" />Portería<span class="nav-chevron" aria-hidden="true"
                  >⌄</span
                >
              </summary>
              @if (session.can('attendance.gate')) {
                <a
                  routerLink="/workspace/gate"
                  [routerLinkActiveOptions]="{ exact: true }"
                  routerLinkActive="active"
                  ariaCurrentWhenActive="page"
                  ><app-icon name="scan" />Lector de entrada</a
                >
              }
              @if (session.can('attendance.read')) {
                <a
                  routerLink="/workspace/gate/report"
                  routerLinkActive="active"
                  ariaCurrentWhenActive="page"
                  ><app-icon name="clock" />Entradas y salidas</a
                >
              }
              @if (session.can('attendance.manage')) {
                <a
                  routerLink="/workspace/gate/devices"
                  routerLinkActive="active"
                  ariaCurrentWhenActive="page"
                  ><app-icon name="phone" />Teléfonos lectores</a
                >
              }
            </details>
          }
          @if (!preview() && session.can('finance.read')) {
            <span class="nav-group" aria-hidden="true">Finanzas</span>
            <a
              routerLink="/workspace/finance"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="wallet" />Caja y cobros</a
            >
          }
          @if (!preview() && session.can('inventory.read')) {
            <span class="nav-group" aria-hidden="true">Bodega</span>
            <a
              routerLink="/workspace/inventory"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="book" />Inventario</a
            >
          }
          @if (!preview() && (session.can('accounts.manage') || session.can('audit.read'))) {
            <details
              class="nav-section"
              [open]="
                router.url.startsWith('/workspace/access') ||
                router.url.startsWith('/workspace/audit') ||
                router.url.startsWith('/workspace/roles')
              "
            >
              <summary>
                <app-icon name="key" />Administración<span class="nav-chevron" aria-hidden="true"
                  >⌄</span
                >
              </summary>
              @if (session.can('accounts.manage')) {
                <a
                  routerLink="/workspace/roles"
                  routerLinkActive="active"
                  ariaCurrentWhenActive="page"
                  ><app-icon name="shield" />Roles y permisos</a
                >
                <a
                  routerLink="/workspace/access"
                  routerLinkActive="active"
                  ariaCurrentWhenActive="page"
                  ><app-icon name="key" />Cuentas y accesos</a
                >
              }
              @if (session.can('audit.read')) {
                <a
                  routerLink="/workspace/audit"
                  routerLinkActive="active"
                  ariaCurrentWhenActive="page"
                  ><app-icon name="chart" />Auditoría</a
                >
              }
            </details>
          }
        </nav>
        @if (preview()) {
          <div class="nav-label">MUESTRA DEL DISEÑO</div>
          <p class="sidebar-note">Explora la navegación con información ficticia.</p>
          <a class="sidebar-home" routerLink="/"><app-icon name="arrow" />Volver al inicio</a>
        }
        @if (!preview()) {
          <div class="sidebar-footer">
            <a
              class="sidebar-account"
              routerLink="/workspace/security"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              ><app-icon name="user" />Mi cuenta</a
            >
            @if (session.token()) {
              <form class="sidebar-logout" action="/Identity/Account/Logout" method="post">
                <input type="hidden" name="__RequestVerificationToken" [value]="session.token()" />
                <button type="submit"><app-icon name="logout" />Cerrar sesión</button>
              </form>
            }
          </div>
        }
        <div class="sidebar-art" aria-hidden="true">
          <app-icon name="school" /><svg viewBox="0 0 260 130" preserveAspectRatio="none">
            <path d="M0 0C80 12 140 58 260 110V130H0Z" />
          </svg>
        </div>
      </aside>
      @if (menuOpen()) {
        <button
          class="menu-backdrop"
          (click)="closeMenu()"
          tabindex="-1"
          aria-hidden="true"
          aria-label="Cerrar menú"
        ></button>
      }
      <div class="workspace-main" [attr.inert]="menuOpen() ? '' : null">
        <header class="topbar" [class.home-topbar]="title() === 'Inicio'">
          <div class="breadcrumb">
            <button
              class="icon-button mobile-menu"
              (click)="openMenu()"
              [attr.aria-expanded]="menuOpen()"
              aria-controls="navigation"
              aria-label="Abrir menú"
            >
              <app-icon name="menu" /></button
            ><span class="breadcrumb-text">{{ title() }}</span>
          </div>
          <div class="topbar-actions">
            @if (!preview() && chat.summary().available) {
              <a
                class="topbar-chat"
                routerLink="/workspace/messages"
                [attr.aria-label]="
                  'Mensajes' +
                  (chat.summary().unread ? ': ' + chat.summary().unread + ' sin leer' : '')
                "
                [title]="
                  chat.failed()
                    ? 'No se pudieron actualizar los mensajes. Abre para reintentar.'
                    : 'Mensajes'
                "
                ><app-icon name="message" />
                @if (chat.summary().unread) {
                  <span class="notify-badge" aria-hidden="true">{{
                    chat.summary().unread > 99 ? '99+' : chat.summary().unread
                  }}</span>
                }
              </a>
            }
            @if (!preview()) {
              <div class="notify-wrap">
                <button
                  type="button"
                  class="topbar-notification"
                  (click)="toggleNotifications($event)"
                  [attr.aria-expanded]="notifyOpen()"
                  aria-controls="notification-panel"
                  [attr.aria-label]="
                    'Notificaciones' +
                    (notifications.count() ? ': ' + notifications.count() + ' nuevas' : '')
                  "
                >
                  <app-icon name="bell" />
                  @if (notifications.count()) {
                    <span class="notify-badge" aria-hidden="true">{{
                      notifications.count() > 9 ? '9+' : notifications.count()
                    }}</span>
                  }
                </button>
                @if (notifyOpen()) {
                  <section
                    id="notification-panel"
                    class="notify-panel"
                    role="dialog"
                    aria-label="Notificaciones"
                    (click)="$event.stopPropagation()"
                  >
                    <header class="notify-head">
                      <strong>Notificaciones</strong>
                      @if (hasUnreadNotices()) {
                        <button type="button" class="text-button" (click)="markAllRead()">
                          Marcar avisos como leídos
                        </button>
                      }
                    </header>
                    @if (notifications.items().length) {
                      <ul class="notify-list">
                        @for (item of notifications.items(); track item.key) {
                          <li>
                            <a
                              [routerLink]="item.link"
                              [queryParams]="item.queryParams"
                              [fragment]="item.fragment"
                              (click)="openNotification(item)"
                              [class.is-new]="item.isNew"
                              ><span class="notify-icon" [attr.data-kind]="item.kind"
                                ><app-icon [name]="iconFor(item.kind)" /></span
                              ><span class="notify-copy"
                                ><strong>{{ item.title }}</strong
                                ><small>{{ item.detail }}</small></span
                              >
                              @if (item.isNew) {
                                <span class="notify-dot" aria-label="Nueva"></span>
                              }
                            </a>
                          </li>
                        }
                      </ul>
                    } @else if (notifications.error()) {
                      <p class="notify-empty">{{ notifications.error() }}</p>
                    } @else {
                      <p class="notify-empty">
                        <app-icon name="check" /><span
                          ><strong>Estás al día</strong>No hay avisos nuevos ni pendientes.</span
                        >
                      </p>
                    }
                    <a
                      class="notify-all"
                      routerLink="/workspace/announcements"
                      (click)="closeNotifications()"
                      >Ver todos los avisos <app-icon name="arrow"
                    /></a>
                  </section>
                }
              </div>
            }
            <details class="topbar-profile-menu">
              <summary
                [attr.aria-label]="
                  'Mi cuenta: ' + (session.displayName() || session.username() || 'Demostración')
                "
              >
                <span class="avatar">{{
                  preview() ? 'D' : session.initials() || school.shortName.slice(0, 1)
                }}</span
                ><span class="topbar-profile-copy"
                  ><strong>{{
                    preview() ? 'Demostración' : session.displayName() || session.username()
                  }}</strong
                  ><small>{{
                    preview() ? 'Vista de muestra' : session.schoolProfile() || 'Mi espacio'
                  }}</small></span
                ><app-icon name="chevron" />
              </summary>
              <div class="topbar-profile-dropdown">
                <a routerLink="/workspace/security"><app-icon name="user" />Mi cuenta y seguridad</a
                ><a routerLink="/"><app-icon name="home" />Ir al sitio público</a>
              </div>
            </details>
          </div>
        </header>
        @if (preview()) {
          <div class="sample-notice">
            <app-icon name="info" /><span
              ><strong>Muestra interactiva.</strong> Todos los datos son ficticios. Los cambios de
              filtros no modifican registros.</span
            >
          </div>
        }
        <main id="content" class="page-content"><ng-content /></main>
        <footer class="workspace-footer">
          {{ school.displayName }}
          <span>{{ preview() ? 'Vista de demostración' : 'Plataforma escolar' }}</span>
        </footer>
      </div>
    </div>
  `,
})
export class Shell {
  readonly router = inject(Router);
  readonly session = inject(SessionService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly school = inject(InstitutionService).current;
  readonly preview = input(false);
  readonly title = input('Vista general');
  /** Kept for compatibility with pages that still pass it; the bell uses NotificationService. */
  readonly unread = input(0);
  /** Teacher courses grouped by grade and section, in grade order then subject. */
  readonly teachingGroups = computed(() => {
    const groups = new Map<string, { label: string; courses: { id: string; subject: string }[] }>();
    for (const c of this.session.myCourses()) {
      const label = c.section ? `${c.grade} · ${c.section}` : c.grade;
      const group = groups.get(label) ?? { label, courses: [] };
      group.courses.push({ id: c.id, subject: c.subject });
      groups.set(label, group);
    }
    return [...groups.values()].map((g) => ({
      ...g,
      courses: g.courses.sort((a, b) => a.subject.localeCompare(b.subject, 'es')),
    }));
  });
  readonly menuOpen = signal(false);
  readonly notifications = inject(NotificationService);
  readonly chat = inject(ChatInbox);
  readonly notifyOpen = signal(false);
  readonly hasUnreadNotices = computed(() =>
    this.notifications.items().some((x) => x.kind === 'notice'),
  );

  constructor() {
    // Inputs are bound after construction; read preview() once they are.
    queueMicrotask(() => {
      if (!this.preview()) this.notifications.start();
    });
    // Long menus scroll on their own: keep the current page's entry visible (only the menu scrolls, never the page).
    afterNextRender(() => {
      setTimeout(() => {
        const nav = this.host.nativeElement.querySelector('.sidebar nav') as HTMLElement | null;
        const active = nav?.querySelector('a.active') as HTMLElement | null;
        if (!nav || !active) return;
        const top =
          active.getBoundingClientRect().top - nav.getBoundingClientRect().top + nav.scrollTop;
        if (
          top + active.offsetHeight > nav.scrollTop + nav.clientHeight - 24 ||
          top < nav.scrollTop
        )
          nav.scrollTop = Math.max(0, top - nav.clientHeight / 2);
      }, 0);
    });
  }
  iconFor(kind: NotificationKind): string {
    return {
      notice: 'megaphone',
      task: 'clipboard',
      money: 'wallet',
      teaching: 'book',
      admin: 'key',
    }[kind];
  }
  toggleNotifications(event: Event): void {
    event.stopPropagation();
    const open = !this.notifyOpen();
    this.notifyOpen.set(open);
    if (open) {
      void this.notifications.refresh(true);
      // Pending items stop counting once seen; unread notices keep counting until opened.
      setTimeout(() => this.notifications.markSeen(), 1200);
    }
  }
  closeNotifications(): void {
    this.notifyOpen.set(false);
  }
  openNotification(item: AppNotification): void {
    this.notifyOpen.set(false);
    if (item.noticeId) void this.notifications.markNoticeRead(item.noticeId);
  }
  async markAllRead(): Promise<void> {
    await this.notifications.markAllNoticesRead();
  }
  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.notifyOpen()) this.notifyOpen.set(false);
  }
  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.notifyOpen()) return;
    this.notifyOpen.set(false);
    this.host.nativeElement.querySelector<HTMLElement>('.topbar-notification')?.focus();
  }
  onNavigationClick(event: MouseEvent): void {
    if ((event.target as Element).closest('a')) this.closeMenu(false);
  }
  openMenu(): void {
    this.menuOpen.set(true);
    requestAnimationFrame(() =>
      this.host.nativeElement.querySelector<HTMLElement>('aside nav a')?.focus(),
    );
  }
  closeMenu(restoreFocus = true): void {
    this.menuOpen.set(false);
    if (restoreFocus)
      requestAnimationFrame(() =>
        this.host.nativeElement.querySelector<HTMLElement>('.mobile-menu')?.focus(),
      );
  }
  onMenuKeydown(event: KeyboardEvent): void {
    if (!this.menuOpen()) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeMenu();
      return;
    }
    if (event.key !== 'Tab') return;
    const controls = [
      ...this.host.nativeElement.querySelectorAll<HTMLElement>(
        'aside a, aside button, aside summary',
      ),
    ].filter((element) => element.getClientRects().length > 0);
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (
      (event.shiftKey && document.activeElement === first) ||
      (!event.shiftKey && document.activeElement === last)
    ) {
      event.preventDefault();
      (event.shiftKey ? last : first)?.focus();
    }
  }
}
