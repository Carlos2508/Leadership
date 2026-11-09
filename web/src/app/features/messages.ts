import {
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccessApi, PageResult } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { ChatContact, ChatInbox, ChatMessage, ChatPage, ChatThread, ChatYear } from '../core/chat';
import { PendingChanges } from '../core/pending-changes';
import { Icon } from '../shared/icon';
import { Shell } from '../shared/shell';
import { ChatPresence } from '../shared/chat-presence';

@Component({
  selector: 'app-messages',
  imports: [Shell, Icon, FormsModule, DatePipe, ChatPresence],
  templateUrl: './messages.html',
})
export class Messages implements PendingChanges {
  private readonly api = inject(AccessApi);
  private readonly alerts = inject(Alerts);
  readonly inbox = inject(ChatInbox);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  @ViewChild('history') private history?: ElementRef<HTMLElement>;
  @ViewChild('contactsDialog') private dialog?: ElementRef<HTMLDialogElement>;
  readonly years = signal<ChatYear[]>([]);
  readonly threads = signal<ChatThread[]>([]);
  readonly current = signal<ChatThread | null>(null);
  readonly messages = signal<ChatMessage[]>([]);
  readonly error = signal('');
  readonly threadError = signal('');
  readonly loading = signal(true);
  readonly threadLoading = signal(false);
  readonly sending = signal(false);
  readonly older = signal(false);
  readonly more = signal(false);
  readonly newMessages = signal(false);
  readonly contacts = signal<ChatContact[]>([]);
  readonly contactTotal = signal(0);
  readonly contactBusy = signal(false);
  readonly contactError = signal('');
  readonly listBusy = signal(false);
  readonly threadTotal = signal(0);
  year = '';
  draft = '';
  search = '';
  contactSearch = '';
  contactPage = 1;
  private selectedId = '';
  private generation = 0;
  private polling = false;
  private pending?: { clientMessageId: string; body: string };
  private readThrough = 0;
  private threadPage = 1;
  private listRevision = 0;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private reading = false;
  readonly retrying = signal(false);
  readonly filtered = computed(() =>
    this.threads().filter((t) =>
      `${t.name} ${t.student}`.toLocaleLowerCase().includes(this.filter().toLocaleLowerCase()),
    ),
  );
  readonly filter = signal('');
  constructor() {
    const destroy = inject(DestroyRef);
    this.route.queryParamMap
      .pipe(takeUntilDestroyed())
      .subscribe((params) => void this.load(params.get('year'), params.get('conversation')));
    const timer = setInterval(() => {
      if (!document.hidden) void this.poll();
    }, 3000);
    destroy.onDestroy(() => {
      clearInterval(timer);
      clearTimeout(this.searchTimer);
    });
  }
  async canLeave(): Promise<boolean> {
    if (this.sending()) return false;
    return (
      (!this.draft.trim() && !this.pending) ||
      (await this.alerts.confirm({
        title: 'Hay un mensaje sin enviar',
        text: 'Puedes quedarte para enviarlo o salir y descartarlo.',
        confirmText: 'Salir y descartar',
        cancelText: 'Seguir escribiendo',
      }))
    );
  }
  async load(requestedYear: string | null, id: string | null): Promise<void> {
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    this.current.set(null);
    this.messages.set([]);
    this.selectedId = id ?? '';
    this.threadError.set('');
    try {
      const years = await this.api.get<ChatYear[]>('/api/v1/chat/years');
      if (generation !== this.generation) return;
      this.years.set(years);
      this.year =
        requestedYear ?? years.find((y) => y.status === 'Active')?.id ?? years[0]?.id ?? '';
      if (requestedYear && !years.some((y) => y.id === requestedYear)) {
        this.error.set('Ese ciclo no está disponible para tus conversaciones.');
        this.threads.set([]);
        return;
      }
      if (!this.year) {
        this.threads.set([]);
        return;
      }
      const threads = await this.api.get<PageResult<ChatThread>>(this.listUrl());
      if (generation !== this.generation) return;
      this.threads.set(threads.items);
      this.threadTotal.set(threads.total);
      this.threadPage = 1;
      if (id) await this.open(id, generation);
    } catch (e) {
      if (generation === this.generation) {
        this.error.set(this.api.message(e));
        this.threads.set([]);
      }
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }
  async chooseYear(value: string): Promise<void> {
    if (!(await this.canLeave())) return;
    this.draft = '';
    this.pending = undefined;
    this.retrying.set(false);
    void this.router.navigate([], { queryParams: { year: value } });
  }
  async choose(id: string | null): Promise<void> {
    if (!(await this.canLeave())) return;
    this.draft = '';
    this.pending = undefined;
    this.retrying.set(false);
    void this.router.navigate([], { queryParams: { year: this.year, conversation: id } });
  }
  private async open(id: string, generation: number): Promise<void> {
    this.threadLoading.set(true);
    this.readThrough = 0;
    try {
      const page = await this.api.get<ChatPage>(this.base() + `/conversations/${id}/messages`);
      if (generation !== this.generation) return;
      this.current.set(page.conversation);
      this.messages.set(page.items);
      this.more.set(page.hasMore);
      this.jumpToEnd();
    } catch (e) {
      if (generation === this.generation) this.threadError.set(this.api.message(e));
    } finally {
      if (generation === this.generation) this.threadLoading.set(false);
    }
  }
  async poll(): Promise<void> {
    if (!this.year || this.loading() || this.polling) return;
    this.polling = true;
    const generation = this.generation;
    const revision = this.listRevision;
    const atEnd = this.atEnd();
    const thread = this.current();
    try {
      const threads = await this.api.get<PageResult<ChatThread>>(this.listUrl());
      if (generation !== this.generation) return;
      if (revision === this.listRevision) {
        const ids = new Set(threads.items.map((t) => t.id));
        this.threads.set(
          this.threadPage === 1
            ? threads.items
            : [...threads.items, ...this.threads().filter((t) => !ids.has(t.id))],
        );
        this.threadTotal.set(threads.total);
      }
      if (thread) {
        const after = this.messages().at(-1)?.sequence ?? 0;
        const page = await this.api.get<ChatPage>(
          this.base() +
            `/conversations/${thread.id}/messages?after=${after}&knownRead=${thread.peerReadSequence}&knownDelivered=${thread.peerDeliveredSequence ?? 0}&receiptFrom=${this.messages()[0]?.sequence ?? 1}`,
        );
        if (generation !== this.generation) return;
        this.current.set(page.conversation);
        const existing = new Set(this.messages().map((m) => m.sequence));
        const added = page.items.filter((m) => !existing.has(m.sequence));
        const receipts = new Map((page.receipts ?? []).map((r) => [r.sequence, r]));
        this.messages.update((rows) => [
          ...rows.map((m) =>
            receipts.has(m.sequence) ? { ...m, ...receipts.get(m.sequence)! } : m,
          ),
          ...added,
        ]);
        if (
          this.pending &&
          this.messages().some((m) => m.mine && m.clientMessageId === this.pending!.clientMessageId)
        )
          this.sent();
        if (added.length) {
          if (atEnd) this.jumpToEnd();
          else this.newMessages.set(true);
        } else if (atEnd) void this.markRead();
      }
      if (!this.retrying()) this.threadError.set('');
    } catch (e) {
      if (generation === this.generation) {
        this.threadError.set(this.api.message(e));
        const status = (e as { status?: number })?.status;
        if (status === 401 || status === 403 || status === 404) {
          this.current.set(null);
          this.messages.set([]);
          this.threads.set([]);
        }
      }
    } finally {
      this.polling = false;
    }
  }
  async send(): Promise<void> {
    const thread = this.current();
    if (!thread || !thread.canSend || this.sending() || !this.draft.trim()) return;
    this.pending ??= { clientMessageId: crypto.randomUUID(), body: this.draft.trim() };
    const attempt = this.pending;
    this.sending.set(true);
    this.threadError.set('');
    const generation = this.generation;
    try {
      await this.api.post<ChatMessage>(
        this.base() + `/conversations/${thread.id}/messages`,
        attempt,
      );
      if (generation !== this.generation || this.pending !== attempt) return;
      // Fetch from the last displayed sequence: appending the acknowledgment directly could skip incoming
      // messages committed between the previous poll and this send.
      this.sent();
      this.jumpToEnd();
      void this.poll();
    } catch (e) {
      if (generation === this.generation && this.pending === attempt) {
        this.threadError.set(this.api.message(e));
        this.retrying.set(true);
      }
    } finally {
      if (this.pending === attempt) this.sending.set(false);
    }
  }
  private sent(): void {
    this.threadError.set('');
    this.sending.set(false);
    this.pending = undefined;
    this.retrying.set(false);
    this.draft = '';
    void this.inbox.refresh();
  }
  async loadOlder(): Promise<void> {
    const thread = this.current();
    if (!thread || this.older()) return;
    const generation = this.generation;
    const box = this.history?.nativeElement;
    const height = box?.scrollHeight ?? 0;
    this.older.set(true);
    try {
      const page = await this.api.get<ChatPage>(
        this.base() + `/conversations/${thread.id}/messages?before=${this.messages()[0]?.sequence}`,
      );
      if (generation !== this.generation) return;
      const existing = new Set(this.messages().map((m) => m.sequence));
      this.messages.update((rows) => [
        ...page.items.filter((m) => !existing.has(m.sequence)),
        ...rows,
      ]);
      this.more.set(page.hasMore);
      requestAnimationFrame(() => {
        if (box) box.scrollTop += box.scrollHeight - height;
      });
    } catch (e) {
      if (generation === this.generation) this.threadError.set(this.api.message(e));
    } finally {
      this.older.set(false);
    }
  }
  atEnd(): boolean {
    const box = this.history?.nativeElement;
    return !box || box.scrollHeight - box.scrollTop - box.clientHeight < 70;
  }
  jumpToEnd(): void {
    requestAnimationFrame(() => {
      const box = this.history?.nativeElement;
      if (box) box.scrollTop = box.scrollHeight;
      this.newMessages.set(false);
      void this.markRead();
    });
  }
  async markRead(): Promise<void> {
    const thread = this.current();
    const last = this.messages().at(-1)?.sequence ?? 0;
    if (!thread || document.hidden || !this.atEnd() || this.reading || last <= this.readThrough)
      return;
    this.reading = true;
    try {
      await this.api.post(this.base() + `/conversations/${thread.id}/read`, {
        throughSequence: last,
      });
      if (this.current()?.id !== thread.id) return;
      this.readThrough = Math.max(this.readThrough, last);
      this.threads.update((rows) =>
        rows.map((t) =>
          t.id === thread.id ? { ...t, unread: Math.max(0, t.lastSequence - last) } : t,
        ),
      );
      void this.inbox.refresh();
    } catch {
      /* Retried while the displayed messages remain visible; never mark newer, unseen messages. */
    } finally {
      this.reading = false;
    }
  }
  async newChat(): Promise<void> {
    this.contactSearch = '';
    this.contactPage = 1;
    this.contacts.set([]);
    this.dialog?.nativeElement.showModal();
    await this.findContacts();
  }
  async findContacts(append = false): Promise<void> {
    const year = this.year;
    if (!year || this.contactBusy()) return;
    this.contactBusy.set(true);
    this.contactError.set('');
    if (!append) this.contactPage = 1;
    try {
      const result = await this.api.get<PageResult<ChatContact>>(
        this.base() +
          `/contacts?page=${this.contactPage}&search=${encodeURIComponent(this.contactSearch)}`,
      );
      if (year !== this.year) return;
      this.contacts.update((rows) => (append ? [...rows, ...result.items] : result.items));
      this.contactTotal.set(result.total);
    } catch (e) {
      this.contactError.set(this.api.message(e));
    } finally {
      this.contactBusy.set(false);
    }
  }
  async moreContacts(): Promise<void> {
    this.contactPage++;
    await this.findContacts(true);
  }
  searchThreads(value: string): void {
    this.filter.set(value);
    this.listRevision++;
    this.threadPage = 1;
    this.threads.set([]);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => void this.browseThreads(false), 300);
  }
  async browseThreads(append: boolean): Promise<void> {
    if (this.listBusy() || !this.year) return;
    const generation = this.generation,
      revision = this.listRevision;
    this.listBusy.set(true);
    try {
      const page = append ? this.threadPage + 1 : 1;
      const result = await this.api.get<PageResult<ChatThread>>(this.listUrl(page));
      if (generation !== this.generation || revision !== this.listRevision) return;
      const ids = new Set(this.threads().map((t) => t.id));
      this.threads.set(
        append ? [...this.threads(), ...result.items.filter((t) => !ids.has(t.id))] : result.items,
      );
      this.threadPage = page;
      this.threadTotal.set(result.total);
    } catch (e) {
      if (generation === this.generation) this.threadError.set(this.api.message(e));
    } finally {
      this.listBusy.set(false);
      if (generation === this.generation && revision !== this.listRevision)
        void this.browseThreads(false);
    }
  }
  async start(contact: ChatContact): Promise<void> {
    if (this.contactBusy()) return;
    this.contactBusy.set(true);
    this.contactError.set('');
    try {
      const result = await this.api.post<{ id: string }>(this.base() + '/conversations', {
        studentId: contact.studentId,
        teacherId: contact.teacherId,
        memberId: contact.memberId,
      });
      this.dialog?.nativeElement.close();
      await this.choose(result.id);
    } catch (e) {
      this.contactError.set(this.api.message(e));
    } finally {
      this.contactBusy.set(false);
    }
  }
  key(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      void this.send();
    }
  }
  initials(name: string): string {
    return name
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0])
      .join('')
      .toUpperCase();
  }
  isRead(m: ChatMessage, thread: ChatThread): boolean {
    return !!m.readAt || m.sequence <= thread.peerReadSequence;
  }
  isDelivered(m: ChatMessage, thread: ChatThread): boolean {
    return this.isRead(m, thread) || !!m.deliveredAt || m.sequence <= thread.peerDeliveredSequence;
  }
  receiptText(m: ChatMessage, thread: ChatThread): string {
    if (this.isRead(m, thread))
      return m.readAt ? 'Visto a las ' + this.api.time(m.readAt) : 'Visto';
    return this.isDelivered(m, thread) ? 'Entregado' : 'Enviado';
  }
  receiptDetails(m: ChatMessage, thread: ChatThread): string {
    return (
      'Enviado: ' +
      this.api.date(m.sentAt) +
      (m.deliveredAt ? '\nEntregado: ' + this.api.date(m.deliveredAt) : '') +
      (m.readAt
        ? '\nVisto: ' + this.api.date(m.readAt)
        : this.isRead(m, thread)
          ? '\nVisto · hora no registrada para este mensaje anterior'
          : '')
    );
  }
  private base(): string {
    return `/api/v1/years/${this.year}/chat`;
  }
  private listUrl(page = 1): string {
    return this.base() + `/conversations?page=${page}&search=${encodeURIComponent(this.filter())}`;
  }
}
