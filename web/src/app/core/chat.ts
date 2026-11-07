import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { AccessApi } from './access-api';
import { Alerts } from './alerts';
import { SessionService } from './session';

export interface ChatYear {
  id: string;
  label: string;
  status: string;
}
export interface ChatSummary {
  available: boolean;
  unread: number;
  conversations: number;
  revision: string;
  pendingDeliveries: ChatDelivery[];
}
export interface ChatDelivery {
  academicYearId: string;
  conversationId: string;
  throughSequence: number;
}
export interface ChatThread {
  id: string;
  academicYearId: string;
  year: string;
  name: string;
  role: string;
  student: string;
  preview: string;
  updatedAt: string;
  unread: number;
  lastSequence: number;
  peerReadSequence: number;
  peerDeliveredSequence: number;
  peerActiveUntil: string | null;
  peerLastActiveAt: string | null;
  serverNow: string;
  canSend: boolean;
}
export interface ChatMessage {
  sequence: number;
  clientMessageId: string;
  body: string;
  sentAt: string;
  mine: boolean;
  deliveredAt: string | null;
  readAt: string | null;
}
export interface ChatPage {
  conversation: ChatThread;
  items: ChatMessage[];
  hasMore: boolean;
  receipts: { sequence: number; deliveredAt: string | null; readAt: string | null }[];
}
export interface ChatContact {
  studentId: string;
  teacherId: string;
  memberId: string;
  name: string;
  role: string;
  student: string;
}

/** One authenticated badge per browser session. Invalidation polls never expose messages on the public/login page. */
@Injectable({ providedIn: 'root' })
export class ChatInbox {
  private readonly api = inject(AccessApi);
  private readonly session = inject(SessionService);
  private readonly alerts = inject(Alerts);
  private readonly router = inject(Router);
  readonly summary = signal<ChatSummary>({
    available: false,
    unread: 0,
    conversations: 0,
    revision: '',
    pendingDeliveries: [],
  });
  readonly failed = signal(false);
  private owner = '';
  private busy = false;
  private timer?: ReturnType<typeof setInterval>;
  private readonly clientId = crypto.randomUUID();
  private version = 0;
  private active = false;
  constructor() {
    effect(() => {
      const owner = this.session.state() === 'ready' ? this.session.userId() : '';
      if (owner === this.owner) return;
      this.stop();
      this.owner = owner;
      clearInterval(this.timer);
      this.summary.set({
        available: false,
        unread: 0,
        conversations: 0,
        revision: '',
        pendingDeliveries: [],
      });
      if (owner) {
        void this.refresh();
        this.timer = setInterval(() => {
          if (!document.hidden) void this.refresh();
        }, 10000);
      }
    });
    const visibility = () => {
      if (this.foreground()) void this.refresh();
      else this.stop();
    };
    const hide = () => this.stop();
    document.addEventListener('visibilitychange', visibility);
    window.addEventListener('pagehide', hide);
    const navigation = this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) visibility();
    });
    inject(DestroyRef).onDestroy(() => {
      this.stop();
      clearInterval(this.timer);
      navigation.unsubscribe();
      document.removeEventListener('visibilitychange', visibility);
      window.removeEventListener('pagehide', hide);
    });
  }
  private foreground(): boolean {
    return !!this.owner && !document.hidden && this.router.url.startsWith('/workspace');
  }
  private stop(): void {
    if (!this.active) return;
    this.active = false;
    const token = this.session.token();
    if (!token) return;
    void fetch('/api/v1/chat/activity', {
      method: 'POST',
      credentials: 'same-origin',
      keepalive: true,
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
      body: JSON.stringify({
        clientId: this.clientId,
        version: ++this.version,
        active: false,
        deliveries: [],
      }),
    }).catch(() => {});
  }
  private async activity(deliveries: ChatDelivery[]): Promise<void> {
    if (!this.foreground()) return;
    this.active = true;
    await this.api.post('/api/v1/chat/activity', {
      clientId: this.clientId,
      version: ++this.version,
      active: true,
      deliveries,
    });
  }
  async refresh(): Promise<void> {
    if (!this.foreground() || this.busy) return;
    const owner = this.owner;
    this.busy = true;
    try {
      const next = await this.api.get<ChatSummary>('/api/v1/chat/summary');
      if (owner !== this.owner) return;
      const previous = this.summary();
      this.summary.set(next);
      this.failed.set(false);
      if (next.available) await this.activity(next.pendingDeliveries ?? []);
      if (
        next.unread > 0 &&
        previous.revision !== next.revision &&
        !this.router.url.startsWith('/workspace/messages')
      )
        this.alerts.info(
          next.unread === 1
            ? 'Tienes un mensaje sin leer'
            : `Tienes ${next.unread} mensajes sin leer`,
          'Abre el icono de mensajes para conversar.',
        );
    } catch {
      if (owner === this.owner) this.failed.set(true);
    } finally {
      this.busy = false;
    }
  }
}
