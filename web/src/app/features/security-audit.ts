import { Component, inject, signal } from '@angular/core';
import { AccessApi, AuditEntry, PageResult } from '../core/access-api';
import { Shell } from '../shared/shell';

@Component({
  imports: [Shell],
  template: `
    <app-shell title="Auditoría">
      <div class="page-heading">
        <div>
          <span class="eyebrow">CAMBIOS CON TRAZABILIDAD</span>
          <h1>Auditoría de accesos</h1>
          <p>Consulta quién realizó cada cambio y su motivo.</p>
        </div>
        <button class="button outline" (click)="load()" [disabled]="loading()">Actualizar</button>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      <section class="card directory-card" [attr.aria-busy]="loading()">
        @if (loading()) {
          <p role="status" class="loading-state">Cargando auditoría…</p>
        } @else if (data(); as page) {
          <p class="audit-summary muted-copy">
            {{ page.total }} eventos · Fechas en la zona horaria del colegio.
          </p>
          @if (page.items.length) {
            <div
              class="table-scroll"
              tabindex="0"
              role="region"
              aria-label="Tabla de auditoría, desplazable horizontalmente"
            >
              <table>
                <caption class="sr-only">
                  Registro de operaciones de acceso
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Fecha</th>
                    <th scope="col">Operación</th>
                    <th scope="col">Realizada por</th>
                    <th scope="col">Cuenta afectada</th>
                    <th scope="col">Motivo</th>
                  </tr>
                </thead>
                <tbody>
                  @for (event of page.items; track event.id) {
                    <tr>
                      <td>{{ api.date(event.occurredAt) }}</td>
                      <td>
                        <strong>{{ label(event.action) }}</strong>
                        @if (event.detail) {
                          <small class="audit-detail">{{ detail(event.detail) }}</small>
                        }
                      </td>
                      <td>{{ event.actor || 'Preparación inicial o intento sin identificar' }}</td>
                      <td>{{ targetLabel(event) }}</td>
                      <td class="audit-reason">{{ event.reason || '—' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="empty-state">
              <h2>Todavía no hay eventos</h2>
              <p>Las operaciones de acceso aparecerán aquí.</p>
            </div>
          }
          <div class="pagination">
            <button
              class="button outline"
              [disabled]="page.page <= 1"
              (click)="load(page.page - 1)"
            >
              Anterior</button
            ><span>Página {{ page.page }}</span
            ><button
              class="button outline"
              [disabled]="page.page * page.pageSize >= page.total"
              (click)="load(page.page + 1)"
            >
              Siguiente
            </button>
          </div>
        }
      </section>
    </app-shell>
  `,
})
export class SecurityAudit {
  readonly api = inject(AccessApi);
  readonly data = signal<PageResult<AuditEntry> | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  async load(page = 1): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.data.set(await this.api.get<PageResult<AuditEntry>>(`/api/v1/audit?page=${page}`));
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }
  constructor() {
    void this.load();
  }
  label(action: string): string {
    return (
      (
        {
          OwnerProvisioned: 'Gestor inicial creado',
          AccountInvited: 'Cuenta invitada',
          AccessProfileChanged: 'Perfil modificado',
          AccountEnabled: 'Cuenta habilitada',
          AccountSuspended: 'Cuenta suspendida',
          SessionsRevoked: 'Sesiones cerradas',
          AccessCodeIssued: 'Código generado',
          AccessCodesRevoked: 'Códigos anulados',
          AccountActivated: 'Cuenta activada',
          PasswordRecovered: 'Contraseña recuperada',
          LoginSucceeded: 'Ingreso correcto',
          LoginFailed: 'Ingreso rechazado',
          LoginLockedOut: 'Cuenta bloqueada por intentos',
          Logout: 'Salida de la cuenta',
          PersonCreated: 'Persona registrada',
          PersonUpdated: 'Datos personales actualizados',
          StudentProfileAdded: 'Perfil de estudiante agregado',
          GuardianProfileAdded: 'Perfil de encargado agregado',
          PersonArchived: 'Persona archivada',
          PersonRestored: 'Persona restaurada',
          GuardianLinked: 'Vínculo familiar registrado',
          GuardianFacultiesChanged: 'Facultades modificadas',
          GuardianUnlinked: 'Vínculo familiar finalizado',
          AccountLinkedToPerson: 'Cuenta vinculada a persona',
          AccountUnlinkedFromPerson: 'Cuenta desvinculada de persona',
        } as Record<string, string>
      )[action] ?? 'Operación registrada'
    );
  }
  targetLabel(event: AuditEntry): string {
    // Person/relationship targets are identified by a short reference; names stay in the protected directory.
    if (event.targetType === 'Person') return 'Persona ' + (event.targetId ?? '').slice(0, 8);
    if (event.targetType === 'Relationship') return 'Vínculo ' + (event.targetId ?? '').slice(0, 8);
    return event.target || '—';
  }
  detail(value: string): string {
    return (
      (
        {
          basic: 'Cuenta básica',
          'access-manager': 'Gestión de accesos',
          auditor: 'Consulta de auditoría',
          'access-owner': 'Gestión y auditoría',
          Activation: 'Activación',
          Recovery: 'Recuperación',
          'people-reader': 'Consulta de personas',
          registrar: 'Secretaría',
          administrator: 'Administración institucional',
        } as Record<string, string>
      )[value] ?? ''
    );
  }
}
