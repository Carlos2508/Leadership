import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AccessApi,
  AccessCode,
  AccessProfile,
  AccountSummary,
  PageResult,
} from '../core/access-api';
import { SessionService } from '../core/session';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

@Component({
  imports: [Shell, Icon, FormsModule],
  templateUrl: './access-management.html',
})
export class AccessManagement {
  readonly api = inject(AccessApi);
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
  profile = 'basic';
  reason = '';
  verified = false;
  operation = 'profile';
  constructor() {
    void this.load();
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
    return this.profiles().find((x) => x.key === key)?.label ?? 'Cuenta básica';
  }
  openInvite(): void {
    this.resetDialog();
    this.username = '';
    this.profile = 'basic';
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
          profile: 'Guardar perfil',
          sessions: 'Cerrar todas las sesiones',
          issue: 'Generar código de acceso',
          cancel: 'Anular códigos vigentes',
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
        this.notice.set('El cambio se guardó y quedó registrado.');
        if (account.id === this.session.userId() && this.operation !== 'cancel') {
          await this.session.load('/workspace/access');
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
