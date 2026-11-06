import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  readonly username = signal('');
  /** Name of the person linked to the account, when there is one. */
  readonly displayName = signal('');
  /** Up to two initials for the avatar: person name first, otherwise the username. */
  initials(): string {
    const source = this.displayName() || this.username();
    const parts = source.split(/[\s._-]+/).filter(Boolean);
    return (
      this.displayName() && parts.length > 1 ? parts[0][0] + parts[1][0] : source.slice(0, 1)
    ).toUpperCase();
  }
  readonly state = signal<'loading' | 'ready' | 'denied' | 'error'>('loading');
  readonly token = signal('');
  readonly permissions = signal<string[]>([]);
  readonly userId = signal('');
  readonly studentId = signal<string | null>(null);
  readonly schoolProfile = signal('');
  readonly twoFactorEnabled = signal(false);
  /** Permissions the account holds but cannot use until two-step verification is enabled. */
  readonly mfaPending = signal<string[]>([]);
  can(permission: string): boolean {
    return this.permissions().includes(permission);
  }
  private loadedAt = 0;
  private pending: Promise<boolean> | null = null;
  /**
   * Validates the session against the server. Route guards reuse a validation younger than a few seconds so moving
   * between pages does not wait on it; explicit refreshes (after changing permissions or MFA) pass force. The server
   * still authorizes every API call, so this only affects what the interface shows.
   */
  load(returnUrl = '/workspace', force = false): Promise<boolean> {
    if (!force && this.state() === 'ready' && Date.now() - this.loadedAt < 15000)
      return Promise.resolve(true);
    if (!force && this.pending) return this.pending;
    const run = this.fetch(returnUrl, force).finally(() => (this.pending = null));
    this.pending = run;
    return run;
  }
  private async fetch(returnUrl: string, force: boolean): Promise<boolean> {
    // The antiforgery token is requested in parallel the first time; it stays valid for this sign-in.
    const tokenRequest =
      this.token() && !force
        ? null
        : firstValueFrom(this.http.get<{ token: string }>('/api/v1/antiforgery')).then(
            (x) => x.token,
            () => '',
          );
    try {
      const session = await firstValueFrom(
        this.http.get<{
          userId: string;
          username: string;
          permissions: string[];
          twoFactorEnabled?: boolean;
          mfaPending?: string[];
          displayName?: string | null;
          studentId?: string | null;
          schoolProfile?: string | null;
        }>('/api/v1/session'),
      );
      if (this.userId() && this.userId() !== session.userId) this.token.set('');
      this.twoFactorEnabled.set(!!session.twoFactorEnabled);
      this.mfaPending.set(session.mfaPending ?? []);
      this.username.set(session.username);
      this.displayName.set(session.displayName ?? '');
      this.userId.set(session.userId);
      this.studentId.set(session.studentId ?? null);
      this.schoolProfile.set(session.schoolProfile ?? '');
      this.permissions.set(session.permissions);
      this.state.set('ready');
      this.loadedAt = Date.now();
    } catch (error) {
      this.permissions.set([]);
      this.loadedAt = 0;
      if (error instanceof HttpErrorResponse && error.status === 401) {
        window.location.assign(
          '/Identity/Account/Login?ReturnUrl=' + encodeURIComponent(returnUrl),
        );
        return false;
      }
      this.state.set(
        error instanceof HttpErrorResponse && error.status === 403 ? 'denied' : 'error',
      );
    }
    // Logout and writes stay disabled until an antiforgery token can be loaded.
    const token = tokenRequest ? await tokenRequest : '';
    if (token) this.token.set(token);
    else if (!this.token()) {
      try {
        this.token.set(
          (await firstValueFrom(this.http.get<{ token: string }>('/api/v1/antiforgery'))).token,
        );
      } catch {
        /* retried on the next validation */
      }
    }
    return true;
  }
}
export const sessionGuard: CanActivateFn = (_, state) => inject(SessionService).load(state.url);
export const permissionGuard =
  (permission: string): CanActivateFn =>
  async (_, state) => {
    const session = inject(SessionService);
    const router = inject(Router);
    if (!(await session.load(state.url))) return false;
    return session.can(permission) || router.createUrlTree(['/workspace']);
  };
