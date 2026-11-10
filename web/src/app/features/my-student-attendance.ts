import { Component, ViewEncapsulation, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { InstitutionService } from '../core/institution';
import { MyGateDay } from '../core/gate-api';
import {
  AttendanceCalendar,
  AttendanceState,
  StudentAccessView,
  attendanceLabels,
  dayState,
  capitalize,
  isoDate,
  monthWeeks,
  weekdayShort,
} from '../core/student-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { StudentHeader } from '../shared/student-header';

/** Attendance calendar of a student: one mark per day (present, absent, late, excused), the day's detail and gate marks. */
@Component({
  imports: [Shell, Icon, StudentHeader],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../styles/_student-pages.scss', '../../styles/_student-attendance.scss'],
  template: `
    <app-shell title="Asistencia">
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a esta información</h2>
          <p>Si crees que es un error, comunícate con la administración del colegio.</p>
        </section>
      } @else if (view(); as v) {
        <app-student-header [view]="v" title="Asistencia" />
        @if (!v.isSelf && !v.faculties.includes('AcademicRecords')) {
          <section class="card empty-state"><app-icon name="shield" /><h2>Sin acceso a asistencia</h2><p>Tu vínculo no incluye la consulta académica.</p></section>
        } @else {
          <section class="sp-stats" aria-label="Resumen del mes">
            <div class="sp-stat static">
              <span class="sp-stat-icon tone-green"><app-icon name="check" /></span>
              <span><strong>{{ calendar()?.present ?? '—' }}</strong><small>Presente</small></span>
            </div>
            <div class="sp-stat static">
              <span class="sp-stat-icon tone-red"><app-icon name="close" /></span>
              <span><strong>{{ calendar()?.absent ?? '—' }}</strong><small>Ausencias</small></span>
            </div>
            <div class="sp-stat static">
              <span class="sp-stat-icon tone-gold"><app-icon name="clock" /></span>
              <span><strong>{{ calendar()?.late ?? '—' }}</strong><small>Llegadas tarde</small></span>
            </div>
            <div class="sp-stat static">
              <span class="sp-stat-icon tone-blue"><app-icon name="shield" /></span>
              <span><strong>{{ calendar()?.excused ?? '—' }}</strong><small>Justificadas</small></span>
            </div>
          </section>
          <p class="sp-note">Cada marca es una clase registrada por el docente del curso en el mes elegido.</p>
          @if (error()) {
            <p class="feedback error-feedback" role="alert">{{ error() }} <button class="text-button" (click)="loadMonth()">Reintentar</button></p>
          }
          <div class="sa-layout">
            <section class="card sp-calendar sa-calendar" aria-label="Calendario de asistencia" [attr.aria-busy]="loading()">
              <header class="sp-calendar-head">
                <button type="button" class="icon-button" (click)="shiftMonth(-1)" aria-label="Mes anterior" [disabled]="loading()"><app-icon name="chevron" class="flip" /></button>
                <h2>{{ monthLabel() }}</h2>
                <button type="button" class="icon-button" (click)="shiftMonth(1)" aria-label="Mes siguiente" [disabled]="loading()"><app-icon name="chevron" /></button>
              </header>
              <div class="sp-month" role="group" [attr.aria-label]="monthLabel()">
                @for (d of weekdays; track d) {
                  <span class="sp-weekday" aria-hidden="true">{{ d }}</span>
                }
                @for (week of weeks(); track $index) {
                  @for (cell of week; track cell.date) {
                    <button
                      type="button"
                      class="sp-day sa-day"
                      [class.outside]="!cell.inside"
                      [class.today]="cell.date === today"
                      [class.selected]="cell.date === selectedDay()"
                      [attr.data-state]="state(cell.date)"
                      [attr.aria-label]="dayLabel(cell.date)"
                      (click)="selectedDay.set(cell.date)"
                    >
                      <span class="sp-day-number">{{ cell.day }}</span>
                      @switch (state(cell.date)) {
                        @case ('Present') { <span class="sa-mark"><app-icon name="check" /></span> }
                        @case ('Absent') { <span class="sa-mark"><app-icon name="close" /></span> }
                        @case ('Late') { <span class="sa-mark"><app-icon name="clock" /></span> }
                        @case ('Excused') { <span class="sa-mark"><app-icon name="shield" /></span> }
                      }
                      @if (gateFor(cell.date); as g) {
                        <span class="sa-gate" title="Entrada registrada en portería"></span>
                      }
                    </button>
                  }
                }
              </div>
              <ul class="sa-legend" aria-label="Significado de las marcas">
                <li data-state="Present"><app-icon name="check" />Presente</li>
                <li data-state="Absent"><app-icon name="close" />Ausente</li>
                <li data-state="Late"><app-icon name="clock" />Tarde</li>
                <li data-state="Excused"><app-icon name="shield" />Justificada</li>
                <li class="gate"><span class="sa-gate"></span>Entrada en portería</li>
              </ul>
            </section>
            <aside class="card sa-detail" aria-live="polite">
              <span class="eyebrow">DETALLE DEL DÍA</span>
              <h2>{{ longDate(selectedDay()) }}</h2>
              @if (gateFor(selectedDay()); as g) {
                <p class="sa-gate-line"><app-icon name="scan" />Entrada {{ api.time(g.firstIn) }} · Salida {{ api.time(g.lastOut) }}</p>
              }
              @if (detail(); as d) {
                <ul class="sa-entries">
                  @for (e of d.entries; track $index) {
                    <li [attr.data-state]="e.status">
                      <span class="sa-entry-mark"><app-icon [name]="icon(e.status)" /></span>
                      <span class="sa-entry-copy"><strong>{{ e.subject }}</strong>@if (e.note) {<small>{{ e.note }}</small>}</span>
                      <span class="sa-entry-label">{{ labels[e.status] }}</span>
                    </li>
                  }
                </ul>
              } @else {
                <p class="muted-copy">No hay asistencia registrada este día.</p>
              }
            </aside>
          </div>
        }
      } @else if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      } @else {
        <p class="loading-state card" role="status">Cargando…</p>
      }
    </app-shell>
  `,
})
export class MyStudentAttendance {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  private readonly school = inject(InstitutionService).current;
  readonly view = signal<StudentAccessView | null>(null);
  readonly calendar = signal<AttendanceCalendar | null>(null);
  readonly gate = signal<MyGateDay[]>([]);
  readonly notFound = signal(false);
  readonly error = signal('');
  readonly loading = signal(false);
  readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly today = isoDate(new Date());
  readonly selectedDay = signal(this.today);
  readonly weekdays = weekdayShort;
  readonly labels = attendanceLabels;
  private request = 0;
  readonly weeks = computed(() => monthWeeks(this.month().getFullYear(), this.month().getMonth()));
  readonly byDate = computed(() => new Map((this.calendar()?.days ?? []).map((d) => [d.date, d])));
  readonly gateByDate = computed(() => new Map(this.gate().map((d) => [d.date, d])));
  readonly detail = computed(() => this.byDate().get(this.selectedDay()) ?? null);
  readonly monthLabel = computed(() =>
    capitalize(new Intl.DateTimeFormat(this.school.locale.culture, { month: 'long', year: 'numeric' }).format(this.month())),
  );

  constructor() {
    effect(() => {
      this.id();
      untracked(() => void this.load());
    });
  }
  async load(): Promise<void> {
    this.error.set('');
    try {
      const view = await this.api.get<StudentAccessView>(`/api/v1/me/students/${encodeURIComponent(this.id())}`);
      this.view.set(view);
      if (!view.isSelf && !view.faculties.includes('AcademicRecords')) return;
      void this.api.get<MyGateDay[]>(`/api/v1/me/students/${view.student.id}/gate`).then((x) => this.gate.set(x)).catch(() => undefined);
      await this.loadMonth();
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
  async loadMonth(): Promise<void> {
    const view = this.view();
    if (!view) return;
    const request = ++this.request;
    const m = this.month();
    const from = isoDate(m);
    const to = isoDate(new Date(m.getFullYear(), m.getMonth() + 1, 0));
    this.loading.set(true);
    this.error.set('');
    try {
      const calendar = await this.api.get<AttendanceCalendar>(`/api/v1/me/students/${view.student.id}/attendance/calendar?from=${from}&to=${to}`);
      if (request !== this.request) return;
      this.calendar.set(calendar);
    } catch (error) {
      if (request === this.request) {
        this.calendar.set(null);
        this.error.set(this.api.message(error));
      }
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }
  shiftMonth(step: number): void {
    const m = this.month();
    const next = new Date(m.getFullYear(), m.getMonth() + step, 1);
    this.month.set(next);
    this.selectedDay.set(isoDate(next));
    void this.loadMonth();
  }
  state(date: string): AttendanceState | null {
    return dayState(this.byDate().get(date));
  }
  gateFor(date: string): MyGateDay | null {
    return this.gateByDate().get(date) ?? null;
  }
  icon(status: AttendanceState): string {
    return { Present: 'check', Absent: 'close', Late: 'clock', Excused: 'shield' }[status];
  }
  dayLabel(date: string): string {
    const state = this.state(date);
    return `${this.longDate(date)}: ${state ? this.labels[state] : 'sin registro'}`;
  }
  longDate(value: string): string {
    return capitalize(new Intl.DateTimeFormat(this.school.locale.culture, { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date(value + 'T12:00:00')));
  }
}
