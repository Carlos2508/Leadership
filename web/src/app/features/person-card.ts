import { Component, ElementRef, ViewChild, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';

/** Card of a student or staff member; guardians and people without a school profile get none (the API answers 404). */
interface PersonCardInfo {
  kind: 'student' | 'staff';
  token: string | null;
  issuedAt: string | null;
  years: { id: string; label: string }[];
}

@Component({
  selector: 'app-person-card', imports: [FormsModule, DatePipe],
  template: `
    <section class="card detail-card" aria-labelledby="card-title"><h2 id="card-title">{{ card() ? 'Carnet y fotografía' : 'Fotografía' }}</h2>
      @if (error()) { <p class="feedback error-feedback" role="alert">{{ error() }}</p> }
      @if (photoUrl()) { <img class="gate-photo" [src]="photoUrl()" alt="Fotografía de la persona" (error)="photoUrl.set('')" /> }
      @if (session.can('people.manage')) {
        <label class="form-field">Subir foto privada (JPG o PNG, máximo 2 MB)<input type="file" accept="image/jpeg,image/png" (change)="upload($event)" [disabled]="busy()" /></label>
        @if (photoUrl()) { <button class="text-button" (click)="remove()" [disabled]="busy()">Eliminar foto</button> }
      }
      @if (card(); as c) {
        <p class="muted-copy">{{ c.kind === 'staff' ? 'Carnet de personal' : 'Carnet de estudiante' }} ·
          {{ c.issuedAt ? 'vigente desde el ' + (c.issuedAt | date: 'dd/MM/yyyy') : 'se emite al imprimirlo por primera vez' }}</p>
        @if (session.can('people.manage')) {
          @if (c.years.length) {
            <label class="form-field">Ciclo del carnet<select name="cardYear" [(ngModel)]="yearId">
              @for (y of c.years; track y.id) { <option [value]="y.id">{{ y.label }}</option> }
            </select></label>
            <div class="dialog-actions"><a class="button outline" [href]="cardUrl('card')" target="_blank" rel="noopener">Carnet en tarjeta</a><a class="button outline" [href]="cardUrl('sheet')" target="_blank" rel="noopener">Carnet en carta</a></div>
          } @else {
            <p class="muted-copy">No hay un ciclo con inscripción abierta o en curso para imprimir el carnet.</p>
          }
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
  readonly personId = input.required<string>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts); readonly session = inject(SessionService);
  @ViewChild('replaceDialog') dialog?: ElementRef<HTMLDialogElement>;
  readonly busy = signal(false); readonly error = signal(''); readonly replaceError = signal(''); readonly photoUrl = signal('');
  readonly card = signal<PersonCardInfo | null>(null);
  reason = '';
  yearId = '';
  constructor() { queueMicrotask(() => { void this.loadPhoto(); void this.loadCard(); }); }
  private async loadPhoto(): Promise<void> {
    try { const status = await this.api.get<{ hasPhoto: boolean }>('/api/v1/people/' + this.personId() + '/photo-status'); if (status.hasPhoto) this.photoUrl.set('/api/v1/people/' + this.personId() + '/photo'); }
    catch (e) { this.error.set(this.api.message(e)); }
  }
  private async loadCard(): Promise<void> {
    try {
      const card = await this.api.get<PersonCardInfo>('/api/v1/people/' + this.personId() + '/card');
      if (!card.years.some((y) => y.id === this.yearId)) this.yearId = card.years[0]?.id ?? '';
      this.card.set(card);
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 404) this.card.set(null);
      else this.error.set(this.api.message(e));
    }
  }
  cardUrl(layout: string): string {
    return '/api/v1/people/' + this.personId() + '/card.pdf?' + new URLSearchParams({ yearId: this.yearId, layout });
  }
  open(): void { this.reason = ''; this.replaceError.set(''); this.dialog?.nativeElement.showModal(); }
  close(): void { if (!this.busy()) this.dialog?.nativeElement.close(); }
  cancel(e: Event): void { if (this.busy()) e.preventDefault(); }
  async replace(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.replaceError.set('');
    try {
      this.card.set(await this.api.post<PersonCardInfo>('/api/v1/people/' + this.personId() + '/card/reissue', { reason: this.reason }));
      this.alerts.success('Carnet reemplazado. Descarga la nueva impresión.');
      this.dialog?.nativeElement.close();
    }
    catch (e) { this.replaceError.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
  async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement; const file = input.files?.[0];
    if (!file || this.busy()) return;
    this.error.set('');
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
