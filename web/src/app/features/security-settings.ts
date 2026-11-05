import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AccessApi } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

interface MfaStatus {
  enabled: boolean;
  recoveryCodesLeft: number;
  requiredBy: string[];
}

const permissionLabels: Record<string, string> = {
  'accounts.manage': 'Cuentas y accesos',
  'audit.read': 'Auditoría',
  'people.manage': 'Gestión de personas',
  'finance.reverse': 'Reversión de pagos',
  'finance.collect': 'Caja',
  'finance.charges': 'Cargos',
  'academic.manage': 'Gestión académica',
};

/** Two-step verification (authenticator app) for the signed-in account. */
@Component({
  imports: [Shell, Icon, FormsModule],
  template: `
    <app-shell title="Mi seguridad">
      <div class="page-heading">
        <div>
          <span class="eyebrow">MI CUENTA</span>
          <h1>Seguridad de mi cuenta</h1>
          <p>Protege tu acceso con una aplicación de autenticación en tu teléfono.</p>
        </div>
      </div>
      @if (notice()) {
        <p class="feedback success-feedback" role="status" tabindex="-1" #noticeRef>{{ notice() }}</p>
      }
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      @if (status(); as s) {
        @if (s.requiredBy.length && !s.enabled) {
          <p class="action-explanation" role="note">
            Tu perfil requiere verificación en dos pasos para usar: {{ labels(s.requiredBy) }}. Actívala para continuar.
          </p>
        }
        <div class="detail-grid academic-grid">
          <div class="detail-main">
            <section class="card detail-card" aria-labelledby="mfa-title">
              <div class="section-heading">
                <h2 id="mfa-title">Verificación en dos pasos</h2>
                <span class="pill" [class.mint]="s.enabled" [class.gold]="!s.enabled">{{ s.enabled ? 'Activa' : 'Inactiva' }}</span>
              </div>
              @if (!s.enabled && !setup()) {
                <p class="muted-copy">
                  Además de tu contraseña, se pedirá un código de 6 dígitos que cambia cada 30 segundos. Funciona con aplicaciones
                  como Google Authenticator, Microsoft Authenticator u otras compatibles con TOTP, incluso sin conexión.
                </p>
                <button class="button" (click)="start()" [disabled]="busy()"><app-icon name="shield" />Activar verificación</button>
              } @else if (!s.enabled && setup(); as setupData) {
                <ol class="setup-steps">
                  <li>
                    <strong>Escanea este código con tu aplicación</strong>
                    <div class="qr-box" [innerHTML]="qr()" role="img" aria-label="Código QR para la aplicación de autenticación"></div>
                    <small>¿No puedes escanear? Escribe esta clave en la aplicación:</small>
                    <code class="shared-key">{{ setupData.sharedKey }}</code>
                  </li>
                  <li>
                    <form class="inline-code" (ngSubmit)="enable()">
                      <label class="form-field"
                        ><span>Escribe el código de 6 dígitos que muestra la aplicación</span
                        ><input name="code" [(ngModel)]="code" required inputmode="numeric" autocomplete="one-time-code" maxlength="7" pattern="[0-9 ]{6,7}"
                      /></label>
                      <button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Verificando…' : 'Confirmar y activar' }}</button>
                    </form>
                  </li>
                </ol>
              } @else {
                <p class="muted-copy">
                  Tu cuenta pide un código de la aplicación al ingresar. Te quedan
                  <strong>{{ s.recoveryCodesLeft }}</strong> códigos de respaldo para usar si no tienes tu teléfono.
                </p>
                <form class="inline-code" (ngSubmit)="regenerate()">
                  <label class="form-field"
                    ><span>Código actual de la aplicación</span
                    ><input name="code" [(ngModel)]="code" required inputmode="numeric" autocomplete="one-time-code" maxlength="7"
                  /></label>
                  <div class="inline-actions">
                    <button class="button outline" type="submit" [disabled]="busy()">Generar nuevos códigos de respaldo</button>
                    @if (!s.requiredBy.length) {
                      <button class="text-button danger-text" type="button" (click)="disable()" [disabled]="busy()">Desactivar verificación</button>
                    }
                  </div>
                </form>
              }
            </section>
            @if (codes().length) {
              <section class="card detail-card recovery-card" aria-labelledby="codes-title">
                <h2 id="codes-title">Guarda tus códigos de respaldo</h2>
                <p class="muted-copy">
                  Cada código sirve una sola vez si pierdes tu teléfono. No volverán a mostrarse; guárdalos en un lugar seguro.
                </p>
                <ul class="recovery-codes">
                  @for (item of codes(); track item) {
                    <li><code>{{ item }}</code></li>
                  }
                </ul>
                <div class="inline-actions">
                  <button class="button outline" (click)="copy()">Copiar códigos</button>
                  <button class="button" (click)="codes.set([])">Ya los guardé</button>
                </div>
              </section>
            }
          </div>
          <aside class="detail-side">
            <section class="card detail-card" aria-labelledby="account-info-title">
              <h2 id="account-info-title">Tu cuenta</h2>
              <ul class="profile-list">
                <li><span>Usuario</span><strong>{{ session.username() }}</strong></li>
                <li><span>Contraseña</span><a class="text-button" href="/Identity/Account/Activate">¿La olvidaste?</a></li>
              </ul>
              <p class="muted-copy">
                Si pierdes tu teléfono y tus códigos, {{ school.displayName }} puede restablecer la verificación después de confirmar
                tu identidad.
              </p>
            </section>
          </aside>
        </div>
      } @else {
        <p class="loading-state card" role="status">Cargando…</p>
      }
    </app-shell>
  `,
})
export class SecuritySettings {
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly sanitizer = inject(DomSanitizer);
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;
  readonly status = signal<MfaStatus | null>(null);
  readonly setup = signal<{ sharedKey: string; authenticatorUri: string; qrSvg: string } | null>(null);
  readonly qr = signal<SafeHtml>('');
  readonly codes = signal<string[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  code = '';

  constructor() {
    queueMicrotask(() => void this.load());
  }
  labels(permissions: string[]): string {
    return permissions.map((x) => permissionLabels[x] ?? x).join(', ');
  }
  async load(): Promise<void> {
    try {
      this.status.set(await this.api.get<MfaStatus>('/api/v1/me/mfa'));
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }
  private async run(action: () => Promise<void>, message: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
      this.code = '';
      this.notice.set(message);
      await this.load();
      await this.session.load(window.location.pathname, true);
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  start(): Promise<void> {
    return this.run(async () => {
      const setup = await this.api.post<{ sharedKey: string; authenticatorUri: string; qrSvg: string }>('/api/v1/me/mfa/setup', {});
      // The SVG is generated by this installation's server from the enrollment key; it contains no scripts.
      this.qr.set(this.sanitizer.bypassSecurityTrustHtml(setup.qrSvg));
      this.setup.set(setup);
    }, 'Escanea el código y confirma con el código de 6 dígitos.');
  }
  enable(): Promise<void> {
    return this.run(async () => {
      const result = await this.api.post<{ codes: string[] }>('/api/v1/me/mfa/enable', { code: this.code });
      this.setup.set(null);
      this.codes.set(result.codes);
    }, 'La verificación en dos pasos quedó activa.');
  }
  regenerate(): Promise<void> {
    return this.run(async () => {
      this.codes.set((await this.api.post<{ codes: string[] }>('/api/v1/me/mfa/recovery-codes', { code: this.code })).codes);
    }, 'Se generaron nuevos códigos de respaldo; los anteriores ya no sirven.');
  }
  disable(): Promise<void> {
    return this.run(async () => {
      await this.api.post('/api/v1/me/mfa/disable', { code: this.code });
    }, 'La verificación en dos pasos se desactivó.');
  }
  async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.codes().join('\n'));
      this.notice.set('Códigos copiados. Guárdalos fuera de este equipo.');
    } catch {
      this.notice.set('Selecciona los códigos para copiarlos manualmente.');
    }
  }
}
