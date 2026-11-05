import { Component, inject } from '@angular/core';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { RouterLink } from '@angular/router';
@Component({
  imports: [Shell, Icon, RouterLink],
  template: `
    <app-shell title="Mi espacio">
      <div class="page-heading">
        <div>
          <span class="eyebrow">MI COMUNIDAD EDUCATIVA</span>
          <h1>Bienvenido{{ session.username() ? ', ' + session.username() : '' }}.</h1>
          <p>Tu punto de encuentro con el colegio.</p>
        </div>
        <form action="/Identity/Account/Logout" method="post">
          <input type="hidden" name="__RequestVerificationToken" [value]="session.token()" /><button
            class="button outline"
            type="submit"
            [disabled]="!session.token()"
          >
            <app-icon name="logout" />Cerrar sesión
          </button>
        </form>
      </div>
      @if (session.state() === 'ready') {
        <section class="workspace-welcome card">
          <div class="icon-tile mint"><app-icon name="shield" /></div>
          <h2>Tu sesión está lista</h2>
          <p>
            Estamos preparando tu espacio. Las opciones de tu cuenta aparecerán aquí cuando el
            colegio las habilite.
          </p>
          <div class="connection-status">
            <span></span>Cuenta conectada a {{ school.displayName }}
          </div>
          @if (session.can('accounts.manage')) {
            <a class="button workspace-action" routerLink="/workspace/access"
              >Administrar cuentas <app-icon name="arrow"
            /></a>
          }
          @if (session.can('audit.read')) {
            <a class="button outline workspace-action" routerLink="/workspace/audit"
              >Consultar auditoría</a
            >
          }
        </section>
        <section class="card contact-card">
          <span class="eyebrow">ESTAMOS PARA AYUDARTE</span>
          <h2>{{ school.displayName }}</h2>
          <p>Para consultas sobre tu cuenta, comunícate con la administración.</p>
          @if (school.contact.phone) {
            <p>{{ school.contact.phone }}</p>
          }
          @if (school.contact.email) {
            <p>{{ school.contact.email }}</p>
          }
          @if (school.contact.address) {
            <p>{{ school.contact.address }}</p>
          }
        </section>
      } @else {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>
            {{
              session.state() === 'denied'
                ? 'Tu cuenta necesita autorización'
                : 'No pudimos cargar tu espacio'
            }}
          </h2>
          <p>
            {{
              session.state() === 'denied'
                ? 'Comunícate con la administración para revisar tu acceso a este colegio.'
                : 'Revisa tu conexión e inténtalo de nuevo.'
            }}
          </p>
          <button class="button" (click)="retry()">Volver a intentar</button>
        </section>
      }
    </app-shell>
  `,
})
export class Workspace {
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  retry(): void {
    window.location.reload();
  }
}
