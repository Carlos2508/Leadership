import { Component, ElementRef, ViewChild, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';

@Component({
  selector: 'app-person-card', imports: [FormsModule],
  template: `
    <section class="card detail-card" aria-labelledby="card-title"><h2 id="card-title">{{ student() ? 'Carnet y fotografía' : 'Fotografía' }}</h2>
      @if (error()) { <p class="feedback error-feedback" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="feedback success-feedback" role="status">{{ notice() }}</p> }
      @if (photoUrl()) { <img class="gate-photo" [src]="photoUrl()" alt="Fotografía de la persona" (error)="photoUrl.set('')" /> }
      @if (session.can('people.manage')) {
        <label class="form-field">Subir foto privada (JPG o PNG, máximo 2 MB)<input type="file" accept="image/jpeg,image/png" (change)="upload($event)" [disabled]="busy()" /></label>
        @if (photoUrl()) { <button class="text-button" (click)="remove()" [disabled]="busy()">Eliminar foto</button> }
        @if (student()) {
          <div class="dialog-actions"><a class="button outline" [href]="cardUrl('card')" target="_blank" rel="noopener">Carnet en tarjeta</a><a class="button outline" [href]="cardUrl('sheet')" target="_blank" rel="noopener">Carnet en carta</a></div>
          <p class="muted-copy">La impresión conserva el carnet vigente. Si se extravió, reemplázalo para invalidar el anterior.</p>
          <button class="text-button" (click)="open()" [disabled]="busy()">Reemplazar carnet</button>
        }
      }
    </section>
    <dialog #replaceDialog class="access-dialog" aria-labelledby="replace-title" (cancel)="cancel($event)">
      <form class="access-form" (ngSubmit)="replace()"><h2 id="replace-title">Reemplazar carnet</h2><p>El carnet anterior dejará de funcionar al confirmar.</p>
        @if (replaceError()) { <p class="feedback error-feedback" role="alert">{{ replaceError() }}</p> }
        <label class="form-field">Motivo del reemplazo<textarea name="cardReason" [(ngModel)]="reason" required minlength="10" maxlength="500"></textarea></label>
        <div class="dialog-actions"><button type="button" class="button outline" (click)="close()" [disabled]="busy()">Cancelar</button><button class="button" [disabled]="busy()">Confirmar reemplazo</button></div>
      </form>
    </dialog>`,
})
export class PersonCard {
  readonly personId = input.required<string>(); readonly student = input(false);
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts); readonly session = inject(SessionService);
  @ViewChild('replaceDialog') dialog?: ElementRef<HTMLDialogElement>;
  readonly busy = signal(false); readonly error = signal(''); readonly replaceError = signal(''); readonly notice = signal(''); readonly photoUrl = signal('');
  reason = '';
  constructor() { queueMicrotask(() => void this.loadPhoto()); }
  private async loadPhoto(): Promise<void> {
    try { const status = await this.api.get<{ hasPhoto: boolean }>('/api/v1/people/' + this.personId() + '/photo-status'); if (status.hasPhoto) this.photoUrl.set('/api/v1/people/' + this.personId() + '/photo'); }
    catch (e) { this.error.set(this.api.message(e)); }
  }
  cardUrl(layout: string): string { return '/api/v1/students/' + this.personId() + '/card.pdf?layout=' + layout; }
  open(): void { this.reason = ''; this.replaceError.set(''); this.dialog?.nativeElement.showModal(); }
  close(): void { if (!this.busy()) this.dialog?.nativeElement.close(); }
  cancel(e: Event): void { if (this.busy()) e.preventDefault(); }
  async replace(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.replaceError.set('');
    try { await this.api.post('/api/v1/students/' + this.personId() + '/card/reissue', { reason: this.reason }); this.alerts.success('Carnet reemplazado. Descarga la nueva impresión.'); this.dialog?.nativeElement.close(); }
    catch (e) { this.replaceError.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
  async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement; const file = input.files?.[0];
    if (!file || this.busy()) return;
    this.error.set(''); this.notice.set('');
    if (file.size > 2 * 1024 * 1024) { this.error.set('La foto debe pesar como máximo 2 MB.'); input.value = ''; return; }
    this.busy.set(true);
    try { const data = new FormData(); data.append('file', file); await this.api.upload('/api/v1/people/' + this.personId() + '/photo', data); this.photoUrl.set('/api/v1/people/' + this.personId() + '/photo?v=' + Date.now()); this.alerts.success('La foto privada se guardó.'); }
    catch (e) { void this.alerts.error('No se pudo completar la acción', this.api.message(e)); }
    finally { this.busy.set(false); input.value = ''; }
  }
  async remove(): Promise<void> {
    if (this.busy()) return;
    if (!(await this.alerts.confirm({ title: '¿Eliminar la fotografía?', text: 'El carnet volverá a mostrar las iniciales.', confirmText: 'Eliminar foto', danger: true }))) return;
    this.busy.set(true); this.error.set('');
    try { await this.api.post('/api/v1/people/' + this.personId() + '/photo/remove', {}); this.photoUrl.set(''); this.alerts.success('La foto se eliminó.'); }
    catch (e) { void this.alerts.error('No se pudo completar la acción', this.api.message(e)); }
    finally { this.busy.set(false); }
  }
}
