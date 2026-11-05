import { Component, ElementRef, inject, input, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { InstitutionService } from '../core/institution';
import { Icon } from './icon';
import { SessionService } from '../core/session';
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
            >{{ school.shortName }}<small>{{ school.campusLabel }}</small></span
          ></a
        >
        <button class="mobile-close icon-button" (click)="closeMenu()" aria-label="Cerrar menú">
          <app-icon name="close" />
        </button>
        <div class="nav-label">{{ preview() ? 'ESPACIO DE ADMINISTRACIÓN' : 'MI ESPACIO' }}</div>
        <nav aria-label="Navegación principal" (click)="closeMenu(false)">
          <a
            [routerLink]="preview() ? '/preview' : '/workspace'"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: true }"
            ariaCurrentWhenActive="page"
            ><app-icon name="home" />Vista general</a
          >
          @if (preview()) {
            <a routerLink="/preview/students" routerLinkActive="active" ariaCurrentWhenActive="page"
              ><app-icon name="people" />{{ school.terms.studentPlural }}</a
            >
          }
          @if (!preview() && session.can('accounts.manage')) {
            <a routerLink="/workspace/access" routerLinkActive="active" ariaCurrentWhenActive="page"
              ><app-icon name="shield" />Cuentas y accesos</a
            >
          }
          @if (!preview() && session.can('audit.read')) {
            <a routerLink="/workspace/audit" routerLinkActive="active" ariaCurrentWhenActive="page"
              ><app-icon name="chart" />Auditoría</a
            >
          }
        </nav>
        @if (preview()) {
          <div class="nav-label">MUESTRA DEL DISEÑO</div>
          <p class="sidebar-note">Explora la navegación con información ficticia.</p>
          <a class="sidebar-home" routerLink="/"><app-icon name="arrow" />Volver al inicio</a>
        }
        <div class="sidebar-bottom">
          <span class="icon-tile gold"><app-icon name="star" /></span>
          <div>
            <strong>{{ school.tagline }}</strong
            ><small>{{ school.welcomeMessage }}</small>
          </div>
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
        <header class="topbar">
          <div class="breadcrumb">
            <button
              class="icon-button mobile-menu"
              (click)="openMenu()"
              [attr.aria-expanded]="menuOpen()"
              aria-controls="navigation"
              aria-label="Abrir menú"
            >
              <app-icon name="menu" /></button
            ><span>Inicio <span class="crumb-divider">/</span> {{ title() }}</span>
          </div>
          <div class="topbar-actions">
            @if (preview()) {
              <span class="pill gold">Ciclo de muestra 2026</span>
            }
            <span class="avatar">{{ preview() ? 'D' : school.shortName.slice(0, 1) }}</span>
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
          <span>{{ preview() ? 'Vista de demostración' : school.welcomeMessage }}</span>
        </footer>
      </div>
    </div>
  `,
})
export class Shell {
  readonly session = inject(SessionService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly school = inject(InstitutionService).current;
  readonly preview = input(false);
  readonly title = input('Vista general');
  readonly menuOpen = signal(false);
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
      ...this.host.nativeElement.querySelectorAll<HTMLElement>('aside a, aside button'),
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
