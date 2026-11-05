import { Component, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Shell } from '../shared/shell';
import { AccessApi } from '../core/access-api';
import { GateDevice } from '../core/gate-api';

@Component({
  imports: [Shell, FormsModule],
  template: `
    <app-shell title="Teléfonos lectores">
      <div class="page-heading"><div><span class="eyebrow">PORTERÍA</span><h1>Teléfonos lectores</h1><p>Registra cada teléfono y revoca su acceso cuando deje de usarse.</p></div></div>
      @if (error()) { <p class="feedback error-feedback" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="feedback success-feedback" role="status">{{ notice() }}</p> }
      <section class="card detail-card"><h2>Registrar teléfono</h2>
        <form class="filter-bar" (ngSubmit)="register()"><label class="form-field">Nombre del teléfono<input name="name" [(ngModel)]="name" required minlength="2" maxlength="80" autocomplete="off" /></label><button class="button" [disabled]="busy() || !!pairing()">Crear emparejamiento</button></form>
        @if (pairing(); as qr) {
          <div class="feedback" role="status"><strong>Escanea este QR desde el lector de la app</strong><p>Da acceso a registrar entradas y salidas. Se muestra una sola vez; ciérralo al terminar.</p><img class="pairing-qr" [src]="qr" alt="QR privado de emparejamiento" /><p><button class="button outline" (click)="clearPairing()">Terminé de emparejar</button></p></div>
        }
      </section>
      <section class="card detail-card"><h2>Dispositivos registrados</h2>
        <ul class="payment-list">@for (d of devices(); track d.id) {
          <li><span class="person-copy"><strong>{{ d.name }}</strong><small>{{ d.revoked ? 'Revocado' : 'Activo' }} · {{ d.lastSeenAt ? 'Última lectura: ' + api.date(d.lastSeenAt) : 'Aún no se ha conectado' }}</small></span>@if (!d.revoked) { <button class="button outline compact-button" (click)="select(d)" [disabled]="busy()">Revocar<span class="sr-only"> {{ d.name }}</span></button> }</li>
        } @empty { <li>No hay teléfonos registrados.</li> }</ul>
        @if (selected(); as d) {
          <form class="access-form" (ngSubmit)="revoke()"><h3>Revocar {{ d.name }}</h3><label class="form-field">Motivo de revocación<textarea name="reason" [(ngModel)]="reason" required minlength="10" maxlength="500"></textarea></label><div class="dialog-actions"><button type="button" class="button outline" (click)="selected.set(null)" [disabled]="busy()">Cancelar</button><button class="button" [disabled]="busy()">Confirmar revocación</button></div></form>
        }
      </section>
    </app-shell>`,
})
export class GateDevices implements OnDestroy {
  readonly api = inject(AccessApi); readonly devices = signal<GateDevice[]>([]); readonly pairing = signal('');
  readonly busy = signal(false); readonly error = signal(''); readonly notice = signal(''); readonly selected = signal<GateDevice | null>(null);
  name = ''; reason = '';
  constructor() { queueMicrotask(() => void this.load()); }
  ngOnDestroy(): void { this.clearPairing(); }
  clearPairing(): void { if (this.pairing()) URL.revokeObjectURL(this.pairing()); this.pairing.set(''); }
  async load(): Promise<void> { try { this.devices.set(await this.api.get('/api/v1/gate/devices')); } catch (e) { this.error.set(this.api.message(e)); } }
  select(d: GateDevice): void { this.selected.set(d); this.reason = ''; this.error.set(''); }
  async register(): Promise<void> {
    if (this.busy() || this.pairing()) return;
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try {
      const result = await this.api.post<{ pairingSvg: string }>('/api/v1/gate/devices', { name: this.name });
      this.pairing.set(URL.createObjectURL(new Blob([result.pairingSvg], { type: 'image/svg+xml' }))); this.name = ''; await this.load();
    } catch (e) { this.error.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
  async revoke(): Promise<void> {
    if (this.busy() || !this.selected()) return;
    this.busy.set(true); this.error.set('');
    try { await this.api.post('/api/v1/gate/devices/' + this.selected()!.id + '/revoke', { reason: this.reason }); this.clearPairing(); this.selected.set(null); this.notice.set('El teléfono ya no puede registrar marcas.'); await this.load(); }
    catch (e) { this.error.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
}
