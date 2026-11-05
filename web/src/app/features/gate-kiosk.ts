import { Component, ElementRef, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Shell } from '../shared/shell';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { GateEvent, GateStudent, ScanResult, directionLabel } from '../core/gate-api';

@Component({
  imports: [Shell, FormsModule],
  template: `
    <app-shell title="Portería">
      <div class="page-heading"><div><span class="eyebrow">CONTROL DE ACCESO</span><h1>Portería</h1><p>Lee el carnet o escribe su código y pulsa Enter.</p></div></div>
      @if (error()) { <p class="feedback error-feedback" role="alert">{{ error() }}</p> }
      <section class="card detail-card" aria-labelledby="reader-title">
        <h2 id="reader-title">Lector de carnets</h2>
        <form class="filter-bar" (ngSubmit)="scan()">
          <label class="form-field">Modo de lectura<select name="mode" [(ngModel)]="mode" [disabled]="busy()"><option value="Auto">Automático</option><option value="In">Solo entrada</option><option value="Out">Solo salida</option></select></label>
          <label class="form-field">Código del carnet<input #reader name="code" [(ngModel)]="code" autocomplete="off" maxlength="32" [readOnly]="busy()" /></label>
          <button class="button" [disabled]="busy() || !code.trim()">Registrar lectura</button>
        </form>
        <p class="muted-copy">En automático, una segunda lectura solo registra salida tras la estancia mínima configurada por el colegio.</p>
        @if (result(); as r) {
          <div class="gate-result" [attr.data-outcome]="r.outcome" [attr.data-direction]="r.direction" role="status">
            <div class="gate-result-photo" aria-hidden="true">
              @if (r.student?.hasPhoto) {
                <img [src]="'/api/v1/people/' + r.student!.id + '/photo'" alt="" />
              } @else {
                <span>{{ initials(r.student?.name) }}</span>
              }
            </div>
            <div class="gate-result-body">
              <span class="gate-result-badge">{{ badge(r) }}</span>
              <strong class="gate-result-message">{{ r.message }}</strong>
              @if (r.student; as st) {
                <p class="gate-result-name">{{ st.name }}</p>
                <p class="gate-result-meta">{{ st.code }} · {{ st.placement || 'Sin inscripción vigente' }}</p>
              }
            </div>
            @if (r.at) {
              <div class="gate-result-time">
                <small>{{ r.outcome === 'Duplicate' ? 'Marcada a las' : 'Hora' }}</small><strong>{{ api.time(r.at) }}</strong>
              </div>
            }
          </div>
        }
      </section>
      <section class="card detail-card" aria-labelledby="manual-title">
        <h2 id="manual-title">Registro manual</h2>
        <form class="filter-bar" (ngSubmit)="search()"><label class="form-field">Buscar estudiante<input name="query" [(ngModel)]="query" minlength="2" maxlength="100" /></label><button class="button outline" [disabled]="busy() || query.trim().length < 2">Buscar</button></form>
        @if (searched()) {
          <ul class="payment-list">@for (s of students(); track s.id) {
            <li><span class="person-copy"><strong>{{ s.name }}</strong><small>{{ s.code }} · {{ s.placement }}</small></span><button class="button outline compact-button" (click)="select(s)" [disabled]="busy()">Seleccionar {{ s.code }}</button></li>
          } @empty { <li>No encontramos estudiantes con inscripción vigente.</li> }</ul>
        }
      </section>
      <section class="card detail-card" aria-labelledby="recent-title">
        <div class="card-title-row"><h2 id="recent-title">Últimas marcas de hoy</h2><button class="text-button" (click)="refresh()" [disabled]="busy()">Actualizar</button></div>
        <ul class="payment-list">@for (mark of recent(); track mark.id) {
          <li><span class="person-copy"><strong>{{ mark.studentName }}</strong><small>{{ mark.studentCode }} · {{ api.time(mark.occurredAt) }}</small></span><span>{{ direction(mark.direction) }}{{ mark.voided ? ' anulada' : '' }}</span></li>
        } @empty { <li>Aún no hay marcas hoy.</li> }</ul>
      </section>
      <dialog #manualDialog class="access-dialog" aria-labelledby="manual-dialog-title" (cancel)="cancel($event)">
        <form class="access-form" (ngSubmit)="manual()"><h2 id="manual-dialog-title">Marca manual</h2><p>{{ selected()?.name }}</p>
          @if (manualError()) { <p class="feedback error-feedback" role="alert">{{ manualError() }}</p> }
          <label class="form-field">Dirección<select name="direction" [(ngModel)]="manualDirection"><option value="In">Entrada</option><option value="Out">Salida</option></select></label>
          <label class="form-field">Motivo<textarea name="reason" [(ngModel)]="reason" required minlength="10" maxlength="500"></textarea></label>
          <p class="muted-copy">La marca y su motivo quedarán en la auditoría.</p>
          <div class="dialog-actions"><button type="button" class="button outline" (click)="close()" [disabled]="busy()">Cancelar</button><button class="button" [disabled]="busy()">Guardar marca</button></div>
        </form>
      </dialog>
    </app-shell>`,
})
export class GateKiosk implements OnDestroy {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  @ViewChild('reader') reader?: ElementRef<HTMLInputElement>;
  @ViewChild('manualDialog') dialog?: ElementRef<HTMLDialogElement>;
  readonly busy = signal(false); readonly error = signal(''); readonly manualError = signal('');
  readonly result = signal<ScanResult | null>(null); readonly recent = signal<GateEvent[]>([]);
  readonly students = signal<GateStudent[]>([]); readonly selected = signal<GateStudent | null>(null); readonly searched = signal(false);
  readonly direction = directionLabel;
  code = ''; mode = 'In'; query = ''; reason = ''; manualDirection = 'In';
  private alive = true;
  constructor() { queueMicrotask(() => { void this.refresh(); this.focus(); }); }
  ngOnDestroy(): void { this.alive = false; }
  private focus(): void { requestAnimationFrame(() => { if (this.alive) this.reader?.nativeElement.focus(); }); }
  async refresh(): Promise<void> {
    try { this.recent.set(await this.api.get('/api/v1/gate/recent')); }
    catch (e) { this.error.set(this.api.message(e)); }
  }
  async scan(): Promise<void> {
    if (this.busy() || !this.code.trim()) return;
    this.busy.set(true); this.error.set(''); this.result.set(null);
    try {
      const result = await this.api.post<ScanResult>('/api/v1/gate/scan', { code: this.code, mode: this.mode });
      this.result.set(result); this.code = ''; this.beep(result.outcome); await this.refresh();
    }
    catch (e) { void this.alerts.error('No se pudo completar la acción', this.api.message(e)); }
    finally { this.busy.set(false); this.focus(); }
  }
  badge(r: ScanResult): string {
    if (r.outcome === 'Recorded') return r.direction === 'Out' ? 'Salida' : 'Entrada';
    return ({ Duplicate: 'Lectura repetida', Unknown: 'No reconocido', Revoked: 'Carnet reemplazado', NotEnrolled: 'Sin inscripción' } as Record<string, string>)[r.outcome] ?? r.outcome;
  }
  initials(name?: string): string {
    return (name ?? '?').split(' ').filter(Boolean).slice(0, 2).map((x) => x[0]).join('').toUpperCase();
  }
  /** Short audible confirmation for the gate operator: one high tone when marked, two low tones otherwise. */
  private beep(outcome: string): void {
    try {
      const AudioCtx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!AudioCtx) return;
      this.audio ??= new AudioCtx();
      const ok = outcome === 'Recorded';
      (ok ? [0] : [0, 0.18]).forEach((delay) => {
        const osc = this.audio!.createOscillator();
        const gain = this.audio!.createGain();
        osc.frequency.value = ok ? 880 : 330;
        gain.gain.value = 0.08;
        osc.connect(gain).connect(this.audio!.destination);
        const at = this.audio!.currentTime + delay;
        osc.start(at);
        osc.stop(at + 0.12);
      });
    } catch {
      /* sound is optional */
    }
  }
  private audio?: AudioContext;
  async search(): Promise<void> {
    if (this.busy() || this.query.trim().length < 2) return;
    this.busy.set(true); this.error.set('');
    try { this.students.set(await this.api.get('/api/v1/gate/search?q=' + encodeURIComponent(this.query))); this.searched.set(true); }
    catch (e) { this.error.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
  select(student: GateStudent): void { this.selected.set(student); this.reason = ''; this.manualError.set(''); this.dialog?.nativeElement.showModal(); }
  close(): void { if (!this.busy()) this.dialog?.nativeElement.close(); }
  cancel(event: Event): void { if (this.busy()) event.preventDefault(); }
  async manual(): Promise<void> {
    if (this.busy() || !this.selected()) return;
    this.busy.set(true); this.manualError.set('');
    try {
      this.result.set(await this.api.post('/api/v1/gate/manual', { studentId: this.selected()!.id, direction: this.manualDirection, reason: this.reason }));
      this.dialog?.nativeElement.close(); await this.refresh(); this.focus();
    } catch (e) { this.manualError.set(this.api.message(e)); }
    finally { this.busy.set(false); }
  }
}
