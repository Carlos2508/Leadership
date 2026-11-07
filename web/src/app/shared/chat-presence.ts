import { Component, DestroyRef, OnChanges, computed, inject, input, signal } from '@angular/core';
import { AccessApi } from '../core/access-api';

/** A lease expires on screen even if the next network request never completes. Server time corrects device clock skew. */
@Component({
  selector: 'app-chat-presence',
  template: `<span class="chat-presence" [class.online]="online()"
    ><i aria-hidden="true"></i>{{ label() }}</span
  >`,
})
export class ChatPresence implements OnChanges {
  readonly until = input<string | null>(null);
  readonly lastActive = input<string | null>(null);
  readonly serverNow = input<string | null>(null);
  private readonly api = inject(AccessApi);
  private readonly clock = signal(Date.now());
  private readonly offset = signal(0);
  readonly online = computed(
    () => !!this.until() && Date.parse(this.until()!) > this.clock() + this.offset(),
  );
  readonly label = computed(() =>
    this.online()
      ? 'En línea'
      : this.lastActive()
        ? 'Últ. vez: ' + this.api.date(this.lastActive())
        : 'Sin actividad reciente',
  );
  constructor() {
    const timer = setInterval(() => this.clock.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }
  ngOnChanges(): void {
    const server = Date.parse(this.serverNow() ?? '');
    if (Number.isFinite(server)) this.offset.set(server - Date.now());
    this.clock.set(Date.now());
  }
}
