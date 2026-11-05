import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Shell } from '../shared/shell';
import { AccessApi } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { DailyReport, DailyRow, GateEvent, directionLabel, gateStates } from '../core/gate-api';

@Component({
  imports: [Shell, FormsModule],
  template: `
    <app-shell title="Entradas y salidas">
      <div class="page-heading"><div><span class="eyebrow">ASISTENCIA EN PORTERÍA</span><h1>Entradas y salidas</h1><p>Consulta las marcas del día y su historial.</p></div></div>
      @if (error()) { <p class="feedback error-feedback" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="feedback success-feedback" role="status">{{ notice() }}</p> }
      <section class="card detail-card">
        <form class="filter-bar" (ngSubmit)="load()"><label class="form-field">Fecha<input type="date" name="date" [(ngModel)]="date" required /></label><button class="button" [disabled]="busy()">Consultar</button></form>
        @if (report(); as r) {
          <p role="status">{{ r.expected }} esperados · {{ r.arrived }} con entrada · {{ r.inside }} dentro · {{ r.left }} salieron · {{ r.absent }} sin marcas</p>
          <label class="form-field">Filtrar por nombre, código o grupo<input name="filter" [(ngModel)]="filter" type="search" /></label>
          <ul class="payment-list gate-report-list">@for (row of filtered(); track row.studentId) {
            <li><span class="person-copy"><strong>{{ row.name }}</strong><small>{{ row.code }} · {{ row.placement }}</small><small>Entrada: {{ api.date(row.firstIn) }} · Salida: {{ api.date(row.lastOut) }}</small></span><span>{{ states[row.state] }}</span><button class="button outline compact-button" (click)="history(row)" [disabled]="busy()">Ver marcas {{ row.code }}</button></li>
          } @empty { <li>No hay estudiantes para esta consulta.</li> }</ul>
        } @else if (busy()) { <p role="status">Cargando reporte…</p> }
      </section>
      <dialog #historyDialog class="access-dialog" aria-labelledby="history-title" (cancel)="cancel($event)">
        <h2 id="history-title">Marcas de {{ selected()?.name }}</h2>
        @if (historyError()) { <p class="feedback error-feedback" role="alert">{{ historyError() }}</p> }
        <ul class="payment-list">@for (mark of events(); track mark.id) {
          <li><span class="person-copy"><strong>{{ direction(mark.direction) }} · {{ api.date(mark.occurredAt) }}</strong><small>{{ mark.recordedBy }} · {{ mark.note }}</small>@if (mark.voided) { <small>Anulada: {{ mark.voidReason }}</small> }</span>
            @if (!mark.voided && session.can('attendance.manage')) { <button class="text-button" (click)="voidId.set(mark.id)" [disabled]="busy()">Anular {{ direction(mark.direction).toLowerCase() }}</button> }
          </li>
        } @empty { <li>No hay marcas ese día.</li> }</ul>
        @if (voidId()) {
          <form class="access-form" (ngSubmit)="voidMark()"><label class="form-field">Motivo de anulación<textarea name="reason" [(ngModel)]="reason" required minlength="10" maxlength="500"></textarea></label><button class="button" [disabled]="busy()">Confirmar anulación</button></form>
        }
        <div class="dialog-actions"><button class="button outline" (click)="close()" [disabled]="busy()">Cerrar</button></div>
      </dialog>
    </app-shell>`,
})
export class GateReport {
  readonly api = inject(AccessApi); readonly session = inject(SessionService);
  @ViewChild('historyDialog') dialog?: ElementRef<HTMLDialogElement>;
  readonly report = signal<DailyReport | null>(null); readonly busy = signal(false); readonly error = signal(''); readonly notice = signal('');
  readonly selected = signal<DailyRow | null>(null); readonly events = signal<GateEvent[]>([]); readonly historyError = signal(''); readonly voidId = signal('');
  readonly states = gateStates; readonly direction = directionLabel;
  date = new Intl.DateTimeFormat('en-CA', { timeZone: inject(InstitutionService).current.locale.timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  filter = ''; reason = ''; private reportDate = this.date;
  constructor() { queueMicrotask(() => void this.load()); }
  filtered(): DailyRow[] { const text = this.filter.toLocaleLowerCase(); return (this.report()?.rows ?? []).filter(x => (x.name + ' ' + x.code + ' ' + x.placement).toLocaleLowerCase().includes(text)); }
  async load(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.report.set(null);
    try { this.report.set(await this.api.get('/api/v1/gate/daily?date=' + encodeURIComponent(this.date))); this.reportDate = this.date; }
    catch (e) { this.error.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
  async history(row: DailyRow): Promise<void> {
    this.selected.set(row); this.events.set([]); this.voidId.set(''); this.reason = ''; this.historyError.set(''); this.dialog?.nativeElement.showModal();
    await this.loadEvents();
  }
  private async loadEvents(): Promise<void> {
    this.busy.set(true);
    try { this.events.set(await this.api.get('/api/v1/gate/students/' + this.selected()!.studentId + '/events?from=' + this.reportDate + '&to=' + this.reportDate)); }
    catch (e) { this.historyError.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
  async voidMark(): Promise<void> {
    if (this.busy() || !this.voidId()) return;
    this.busy.set(true); this.historyError.set('');
    try { await this.api.post('/api/v1/gate/events/' + this.voidId() + '/void', { reason: this.reason }); this.voidId.set(''); this.reason = ''; this.notice.set('La marca se anuló; su historial se conserva.'); }
    catch (e) { this.historyError.set(this.api.message(e)); }
    finally { this.busy.set(false); }
    await this.loadEvents(); await this.load();
  }
  close(): void { if (!this.busy()) this.dialog?.nativeElement.close(); }
  cancel(e: Event): void { if (this.busy()) e.preventDefault(); }
}
