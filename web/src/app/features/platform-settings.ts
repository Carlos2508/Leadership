import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { ChatInbox } from '../core/chat';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

interface SettingsView {
  chatAttachmentMaxMb: number;
  chatAttachmentLimitMb: number;
  chatAttachmentTypes: string[];
  version: string | null;
}

/** Operational settings the administrator changes from the platform (not branding), each change with a reason. */
@Component({
  imports: [Shell, Icon, FormsModule],
  template: `
    <app-shell title="Ajustes">
      <div class="page-heading">
        <div>
          <span class="eyebrow">ADMINISTRACIÓN</span>
          <h1>Ajustes de la plataforma</h1>
          <p>Opciones de funcionamiento de esta instalación. Cada cambio queda en la auditoría con su motivo.</p>
        </div>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      @if (settings(); as s) {
        <section class="card detail-card" aria-labelledby="chat-settings">
          <h2 id="chat-settings"><app-icon name="message" /> Archivos en mensajes</h2>
          <p class="muted-copy">
            En el chat se pueden enviar documentos e imágenes: {{ s.chatAttachmentTypes.join(', ') }}. Los programas y scripts
            (por ejemplo .exe o .bat) nunca se aceptan, tampoco dentro de un ZIP.
          </p>
          <form class="access-form" (ngSubmit)="save()">
            <label class="form-field"
              ><span>Tamaño máximo por archivo (MB)</span
              ><input name="maxMb" type="number" min="1" [max]="s.chatAttachmentLimitMb" step="1" [(ngModel)]="maxMb" required
            /><small>Entre 1 y {{ s.chatAttachmentLimitMb }} MB. Valor actual: {{ s.chatAttachmentMaxMb }} MB.</small></label>
            <label class="form-field"
              ><span>Motivo del cambio</span><input name="reason" [(ngModel)]="reason" required minlength="8" maxlength="500" autocomplete="off"
            /></label>
            <div class="dialog-actions">
              <button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Guardando…' : 'Guardar ajustes' }}</button>
            </div>
          </form>
        </section>
      } @else if (!error()) {
        <p class="loading-state card" role="status">Cargando ajustes…</p>
      }
    </app-shell>
  `,
})
export class PlatformSettings {
  private readonly api = inject(AccessApi);
  private readonly alerts = inject(Alerts);
  private readonly inbox = inject(ChatInbox);
  readonly settings = signal<SettingsView | null>(null);
  readonly error = signal('');
  readonly busy = signal(false);
  maxMb = 5;
  reason = '';

  constructor() {
    void this.load();
  }
  async load(): Promise<void> {
    try {
      const settings = await this.api.get<SettingsView>('/api/v1/settings');
      this.settings.set(settings);
      this.maxMb = settings.chatAttachmentMaxMb;
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }
  async save(): Promise<void> {
    const settings = this.settings();
    if (!settings || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post('/api/v1/settings', { chatAttachmentMaxMb: Number(this.maxMb), version: settings.version, reason: this.reason });
      this.reason = '';
      this.alerts.success('Los ajustes se guardaron.');
      await this.load();
      void this.inbox.refresh();
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
