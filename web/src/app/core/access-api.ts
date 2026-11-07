import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SessionService } from './session';
import { InstitutionService } from './institution';

export interface AccessProfile {
  key: string;
  label: string;
  permissions: string[];
}
export interface AccountSummary {
  id: string;
  username: string;
  status: 'Pending' | 'Active' | 'Suspended';
  profile: string;
  version: string;
  codeExpiresAt: string | null;
  codePurpose: string | null;
  twoFactorEnabled: boolean;
  /** Every role of the account (an account may combine several). */
  profiles?: string[];
}
export interface AccessCode {
  userId: string;
  code: string;
  purpose: string;
  expiresAt: string;
}
export interface PageResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
export interface AuditEntry {
  id: string;
  occurredAt: string;
  action: string;
  reason: string;
  detail: string;
  actor: string | null;
  target: string | null;
  targetId: string | null;
  targetType: string | null;
  traceId: string;
}

@Injectable({ providedIn: 'root' })
export class AccessApi {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly school = inject(InstitutionService).current;
  get<T>(path: string): Promise<T> {
    return firstValueFrom(this.http.get<T>(path));
  }
  async post<T>(path: string, body: object): Promise<T> {
    if (!this.session.token()) {
      const result = await this.get<{ token: string }>('/api/v1/antiforgery');
      this.session.token.set(result.token);
    }
    return firstValueFrom(
      this.http.post<T>(path, body, { headers: { 'X-XSRF-TOKEN': this.session.token() } }),
    );
  }
  /** Multipart upload with the antiforgery header; the browser sets the multipart boundary. */
  async upload<T>(path: string, data: FormData): Promise<T> {
    if (!this.session.token()) {
      const result = await this.get<{ token: string }>('/api/v1/antiforgery');
      this.session.token.set(result.token);
    }
    return firstValueFrom(this.http.post<T>(path, data, { headers: { 'X-XSRF-TOKEN': this.session.token() } }));
  }
  size(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }
  message(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        window.location.assign(
          '/Identity/Account/Login?ReturnUrl=' + encodeURIComponent(
            window.location.pathname + window.location.search + window.location.hash,
          ),
        );
        return 'Tu sesión terminó. Ingresa nuevamente.';
      }
      if (error.status === 403)
        return 'Ya no tienes permiso para realizar esta operación. Recarga la página.';
      if (typeof error.error?.title === 'string') return error.error.title;
    }
    return 'No pudimos completar la operación. Revisa tu conexión e intenta nuevamente.';
  }
  date(value: string | null): string {
    if (!value) return '—';
    return new Intl.DateTimeFormat(this.school.locale.culture, {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: this.school.locale.timeZone,
    }).format(new Date(value));
  }
  /** Time of day only, in the school's time zone (for lists already grouped by date). */
  time(value: string | null): string {
    if (!value) return '—';
    return new Intl.DateTimeFormat(this.school.locale.culture, { timeStyle: 'short', timeZone: this.school.locale.timeZone }).format(
      new Date(value),
    );
  }
}
