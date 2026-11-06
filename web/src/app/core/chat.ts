import { Injectable, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
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
  canSend: boolean;
}
export interface ChatMessage {
  sequence: number;
  clientMessageId: string;
  body: string;
  sentAt: string;
  mine: boolean;
}
export interface ChatPage {
  conversation: ChatThread;
  items: ChatMessage[];
  hasMore: boolean;
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
  });
  readonly failed = signal(false);
  private owner = '';
  private busy = false;
  private timer?: ReturnType<typeof setInterval>;
  constructor() {
    effect(() => {
      const owner = this.session.state() === 'ready' ? this.session.userId() : '';
      if (owner === this.owner) return;
      this.owner = owner;
      clearInterval(this.timer);
      this.summary.set({ available: false, unread: 0, conversations: 0, revision: '' });
      if (owner) {
        void this.refresh();
        this.timer = setInterval(() => {
          if (!document.hidden) void this.refresh();
        }, 10000);
      }
    });
    document.addEventListener('visibilitychange', () => {
      if (!document.hidden) void this.refresh();
    });
  }
  async refresh(): Promise<void> {
    if (!this.owner || this.busy) return;
    const owner = this.owner;
    this.busy = true;
    try {
      const next = await this.api.get<ChatSummary>('/api/v1/chat/summary');
      if (owner !== this.owner) return;
      const previous = this.summary();
      this.summary.set(next);
      this.failed.set(false);
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
