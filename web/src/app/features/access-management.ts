import { Component, ElementRef, ViewChild, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  AccessApi,
  AccessCode,
  AccessProfile,
  AccountSummary,
  PageResult,
} from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  templateUrl: './access-management.html',
})
export class AccessManagement {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  @ViewChild('accountDialog') private dialog!: ElementRef<HTMLDialogElement>;
  readonly data = signal<PageResult<AccountSummary> | null>(null);
  readonly profiles = signal<AccessProfile[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly dialogError = signal('');
  readonly notice = signal('');
  readonly copyNotice = signal('');
  readonly mode = signal<'invite' | 'manage' | 'code'>('invite');
  readonly selected = signal<AccountSummary | null>(null);
  readonly code = signal<AccessCode | null>(null);
  search = '';
  username = '';
  profile = '';
  reason = '';
  verified = false;
  operation = 'profile';
  /** Shortcut from the first-steps guide: `invite` opens the invitation, `my-role` the own account's role change. */
  readonly action = input<string | undefined>();
  constructor() {
    void this.load().then(() => queueMicrotask(() => this.applyAction()));
  }

  private async applyAction(): Promise<void> {
    if (this.action() === 'invite') this.openInvite();
    if (this.action() === 'my-role') {
      let me = this.data()?.items.find((x) => x.id === this.session.userId());
      try {
        // The current account may be beyond the first page of the directory.
        for (let page = 1; !me; page++) {
          const result = await this.api.get<PageResult<AccountSummary>>(
            `/api/v1/access/accounts?search=${encodeURIComponent(this.session.username())}&page=${page}`,
          );
          me = result.items.find((x) => x.id === this.session.userId());
          if (!result.items.length || page * result.pageSize >= result.total) break;
        }
      } catch (error) {
        this.error.set(this.api.message(error));
        return;
      }
      if (!me) {
        this.error.set(
          'No se pudo localizar tu cuenta. Búscala por tu nombre de usuario para cambiar su rol.',
        );
        return;
      }
      this.openAccount(me);
      this.profile = 'administrator';
      this.reason = 'Configuración inicial del colegio por la dirección.';
    }
  }

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [data, profiles] = await Promise.all([
        this.api.get<PageResult<AccountSummary>>(
          `/api/v1/access/accounts?search=${encodeURIComponent(this.search)}&page=${page}`,
        ),
        this.api.get<AccessProfile[]>('/api/v1/access/profiles'),
      ]);
      this.data.set(data);
      this.profiles.set(profiles);
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }
  status(account: AccountSummary): string {
    return { Pending: 'Pendiente de activación', Active: 'Activa', Suspended: 'Suspendida' }[
      account.status
    ];
  }
  profileLabel(key: string): string {
    if (key === 'custom') return 'Permisos personalizados';
    return this.profiles().find((x) => x.key === key)?.label ?? 'Rol no disponible';
  }
  openInvite(): void {
    this.resetDialog();
    this.username = '';
    this.profile = '';
    this.mode.set('invite');
    this.selected.set(null);
    this.dialog.nativeElement.showModal();
  }
  openAccount(account: AccountSummary): void {
    this.resetDialog();
    this.selected.set(account);
    this.profile = account.profile;
    this.operation = 'profile';
    this.mode.set('manage');
    this.dialog.nativeElement.showModal();
  }
  closeDialog(): void {
    if (this.busy()) return;
    this.dialog.nativeElement.close();
    this.code.set(null);
    this.selected.set(null);
    this.reason = '';
  }
  private resetDialog(): void {
    this.dialogError.set('');
    this.copyNotice.set('');
    this.code.set(null);
    this.reason = '';
    this.verified = false;
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeDialog();
  }
  async invite(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.dialogError.set('');
    try {
      const code = await this.api.post<AccessCode>('/api/v1/access/invitations', {
        username: this.username,
        profile: this.profile,
        reason: this.reason,
      });
      this.code.set(code);
      this.mode.set('code');
      await this.load();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  actionLabel(): string {
    if (this.operation === 'state')
      return this.selected()?.status === 'Suspended' ? 'Habilitar cuenta' : 'Suspender cuenta';
    return (
      (
        {
          profile: 'Guardar rol',
          sessions: 'Cerrar todas las sesiones',
          issue: 'Generar código de acceso',
          cancel: 'Anular códigos vigentes',
          'reset-mfa': 'Restablecer verificación',
        } as Record<string, string>
      )[this.operation] ?? 'Guardar'
    );
  }
  async apply(): Promise<void> {
    const account = this.selected();
    if (!account || this.busy()) return;
    this.busy.set(true);
    this.dialogError.set('');
    try {
      const root = `/api/v1/access/accounts/${account.id}`;
      if (this.operation === 'issue') {
        const code = await this.api.post<AccessCode>(root + '/codes', {
          version: account.version,
          purpose: account.status === 'Pending' ? 'Activation' : 'Recovery',
          verificationNote: this.reason,
          identityVerified: this.verified,
        });
        this.code.set(code);
        this.mode.set('code');
      } else {
        const route = (
          {
            profile: 'profile',
            state: 'state',
            sessions: 'revoke-sessions',
            cancel: 'cancel-codes',
            'reset-mfa': 'reset-mfa',
          } as Record<string, string>
        )[this.operation];
        await this.api.post<void>(root + '/' + route, {
          version: account.version,
          reason: this.reason,
          profile: this.profile,
          isActive: account.status === 'Suspended',
        });
        this.busy.set(false);
        this.closeDialog();
        this.alerts.success('El cambio se guardó y quedó registrado.');
        if (account.id === this.session.userId() && this.operation !== 'cancel') {
          await this.session.load('/workspace/access', true);
          return;
        }
      }
      await this.load(this.data()?.page ?? 1);
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  async copyCode(): Promise<void> {
    const code = this.code()?.code;
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      this.copyNotice.set('Código copiado. Entrégalo únicamente al titular verificado.');
    } catch {
      this.copyNotice.set('Selecciona el código para copiarlo manualmente.');
    }
  }
}
