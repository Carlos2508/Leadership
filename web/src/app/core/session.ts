import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  readonly username = signal('');
  readonly state = signal<'loading' | 'ready' | 'denied' | 'error'>('loading');
  readonly token = signal('');
  readonly permissions = signal<string[]>([]);
  readonly userId = signal('');
  can(permission: string): boolean {
    return this.permissions().includes(permission);
  }
  async load(returnUrl = '/workspace'): Promise<boolean> {
    try {
      const session = await firstValueFrom(
        this.http.get<{ userId: string; username: string; permissions: string[] }>(
          '/api/v1/session',
        ),
      );
      this.username.set(session.username);
      this.userId.set(session.userId);
      this.permissions.set(session.permissions);
      this.state.set('ready');
    } catch (error) {
      this.permissions.set([]);
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
    try {
      this.token.set(
        (await firstValueFrom(this.http.get<{ token: string }>('/api/v1/antiforgery'))).token,
      );
    } catch {
      /* Logout remains disabled until an antiforgery token can be loaded. */
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
