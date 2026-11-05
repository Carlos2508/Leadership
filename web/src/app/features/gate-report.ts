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
        <form class="filter-bar" (ngSubmit)="load()"><label class="form-field">Fecha<input type="date" name="date" [(ngModel)]="date" (change)="load()" required /></label><button class="button" [disabled]="busy()">Consultar</button></form>
        @if (report(); as r) {
          <p class="sr-only" role="status">{{ r.expected }} esperados · {{ r.arrived }} con entrada · {{ r.inside }} dentro · {{ r.left }} salieron · {{ r.absent }} sin marcas</p>
          <div class="state-filters" role="group" aria-label="Filtrar por estado">
            <button type="button" class="state-chip" [class.active]="state() === ''" [attr.aria-pressed]="state() === ''" (click)="state.set('')">Todos <strong>{{ r.expected }}</strong></button>
            <button type="button" class="state-chip mint" [class.active]="state() === 'Inside'" [attr.aria-pressed]="state() === 'Inside'" (click)="state.set('Inside')">Dentro <strong>{{ r.inside }}</strong></button>
            <button type="button" class="state-chip blue" [class.active]="state() === 'Left'" [attr.aria-pressed]="state() === 'Left'" (click)="state.set('Left')">Salieron <strong>{{ r.left }}</strong></button>
            <button type="button" class="state-chip gold" [class.active]="state() === 'Absent'" [attr.aria-pressed]="state() === 'Absent'" (click)="state.set('Absent')">Sin marcas <strong>{{ r.absent }}</strong></button>
            <button type="button" class="text-button export-button" (click)="exportCsv()" [disabled]="!filtered().length">Exportar CSV</button>
          </div>
          <label class="form-field">Filtrar por nombre, código o grupo<input name="filter" [(ngModel)]="filter" type="search" /></label>
          <ul class="payment-list gate-report-list">@for (row of filtered(); track row.studentId) {
            <li><span class="person-copy"><strong>{{ row.name }}</strong><small>{{ row.code }} · {{ row.placement }}</small><small>Entrada {{ api.time(row.firstIn) }} · Salida {{ api.time(row.lastOut) }}</small></span><span class="pill" [class.mint]="row.state === 'Inside'" [class.blue]="row.state === 'Left'" [class.gold]="row.state === 'Absent'">{{ states[row.state] }}</span><button class="button outline compact-button" (click)="history(row)" [disabled]="busy()">Ver marcas<span class="sr-only"> {{ row.code }}</span></button></li>
          } @empty { <li>No hay estudiantes para esta consulta.</li> }</ul>
        } @else if (busy()) { <p role="status">Cargando reporte…</p> }
      </section>
      <dialog #historyDialog class="access-dialog" aria-labelledby="history-title" (cancel)="cancel($event)">
        <h2 id="history-title">Marcas de {{ selected()?.name }}</h2>
        @if (historyError()) { <p class="feedback error-feedback" role="alert">{{ historyError() }}</p> }
        <ul class="payment-list">@for (mark of events(); track mark.id) {
          <li><span class="person-copy"><strong>{{ direction(mark.direction) }} · {{ api.date(mark.occurredAt) }}</strong><small>{{ mark.recordedBy }}{{ mark.note ? ' · ' + mark.note : '' }}</small>@if (mark.voided) { <small>Anulada: {{ mark.voidReason }}</small> }</span>
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
  readonly state = signal('');
  filtered(): DailyRow[] {
    const text = this.filter.toLocaleLowerCase();
    return (this.report()?.rows ?? []).filter(x => (!this.state() || x.state === this.state()) && (x.name + ' ' + x.code + ' ' + x.placement).toLocaleLowerCase().includes(text));
  }
  /** Spreadsheet-friendly copy of the rows on screen (UTF-8 with BOM so Excel keeps accents). */
  exportCsv(): void {
    const cell = (value: string) => '"' + value.replaceAll('"', '""') + '"';
    const lines = [['Código', 'Estudiante', 'Grado', 'Entrada', 'Salida', 'Estado'].map(cell).join(',')];
    for (const row of this.filtered())
      lines.push([row.code, row.name, row.placement, row.firstIn ? this.api.time(row.firstIn) : '', row.lastOut ? this.api.time(row.lastOut) : '', this.states[row.state] ?? row.state].map(cell).join(','));
    const url = URL.createObjectURL(new Blob([String.fromCharCode(0xfeff) + lines.join(String.fromCharCode(13, 10))], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `entradas-${this.reportDate}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
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
