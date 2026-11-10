import { Component, ViewEncapsulation, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { InstitutionService } from '../core/institution';
import { facultyLabels, formatDateOnly } from '../core/people-api';
import { Statement, chargeStatusLabels, methodLabels, money } from '../core/finance-api';
import { MyGateDay } from '../core/gate-api';
import { MyGrades, StudentAccessView, StudentTask, points, subjectTone } from '../core/student-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';
import { StudentHeader } from '../shared/student-header';

/** Summary of one student for the student or a linked guardian; each block depends on a faculty and links to its page. */
@Component({
  imports: [Shell, Icon, RouterLink, CyclePipe, StudentHeader],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../styles/_student-pages.scss', '../../styles/_student-summary.scss'],
  template: `
    <app-shell [title]="view()?.student?.displayName ?? 'Estudiante'">
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a esta información</h2>
          <p>Si crees que es un error, comunícate con la administración del colegio.</p>
        </section>
      } @else if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      } @else if (view(); as v) {
        <app-student-header [view]="v" />
        <div class="ss-grid">
          @if (academic()) {
            <section class="card ss-card" aria-labelledby="next-title">
              <header class="ss-head">
                <h2 id="next-title"><app-icon name="clipboard" />Próximas entregas</h2>
                <a class="text-button" [routerLink]="['/workspace/my/students', v.student.id, 'tasks']">Ver tareas<app-icon name="arrow" /></a>
              </header>
              @if (tasks(); as list) {
                <ul class="ss-list">
                  @for (t of upcoming(); track t.id) {
                    <li>
                      <span class="sp-subject" [attr.data-tone]="tone(t.course)">{{ t.course }}</span>
                      <span class="ss-copy"><strong>{{ t.title }}</strong><small>Entrega {{ api.date(t.dueAt) }}{{ t.points !== null ? ' · ' + fmt(t.points) + ' pts' : '' }}</small></span>
                    </li>
                  } @empty {
                    <li class="ss-empty"><app-icon name="check" />No hay entregas pendientes.</li>
                  }
                </ul>
                <p class="ss-foot">{{ pendingCount() }} por entregar · {{ list.length }} en total</p>
              } @else {
                <p class="loading-state" role="status">Cargando tareas…</p>
              }
            </section>
            <section class="card ss-card" aria-labelledby="grades-title">
              <header class="ss-head">
                <h2 id="grades-title"><app-icon name="chart" />Calificaciones</h2>
                <a class="text-button" [routerLink]="['/workspace/my/students', v.student.id, 'grades']">Ver detalle<app-icon name="arrow" /></a>
              </header>
              @if (grades(); as g) {
                @if (latest(); as p) {
                  <p class="ss-sub">{{ p.name }}</p>
                  <ul class="ss-list ss-scores">
                    @for (grade of p.grades.slice(0, 6); track grade.subject) {
                      <li>
                        <span class="ss-copy"><strong>{{ grade.subject }}</strong></span>
                        <span class="ss-bar" [attr.data-tone]="tone(grade.subject)"><span [style.width.%]="grade.status === 'Scored' ? grade.score : 0"></span></span>
                        <span class="ss-score">{{ grade.status === 'Scored' ? fmt(grade.score) : grade.status === 'Excused' ? 'EX' : 'P' }}</span>
                      </li>
                    }
                  </ul>
                } @else if (g.periods.length) {
                  <p class="muted-copy">Hay calificaciones publicadas pendientes de solvencia. Revisa el detalle.</p>
                } @else {
                  <p class="ss-empty"><app-icon name="info" />Aún no hay calificaciones publicadas.</p>
                }
              } @else {
                <p class="loading-state" role="status">Cargando calificaciones…</p>
              }
            </section>
            <section class="card ss-card" aria-labelledby="attendance-title">
              <header class="ss-head">
                <h2 id="attendance-title"><app-icon name="calendar" />Asistencia del ciclo</h2>
                <a class="text-button" [routerLink]="['/workspace/my/students', v.student.id, 'attendance']">Ver calendario<app-icon name="arrow" /></a>
              </header>
              @if (attendance(); as a) {
                <div class="ss-attendance">
                  <span data-state="Present"><strong>{{ a.present }}</strong>Presente</span>
                  <span data-state="Absent"><strong>{{ a.absent }}</strong>Ausencias</span>
                  <span data-state="Late"><strong>{{ a.late }}</strong>Tarde</span>
                  <span data-state="Excused"><strong>{{ a.excused }}</strong>Justificadas</span>
                </div>
              } @else {
                <p class="loading-state" role="status">Cargando asistencia…</p>
              }
            </section>
            <section class="card ss-card" aria-labelledby="gate-title">
              <header class="ss-head"><h2 id="gate-title"><app-icon name="scan" />Entradas y salidas</h2></header>
              @if (gateDays(); as days) {
                <ul class="ss-list">
                  @for (day of days.slice(0, 4); track day.date) {
                    <li><span class="ss-copy"><strong>{{ date(day.date) }}</strong><small>Entrada {{ api.time(day.firstIn) }} · Salida {{ api.time(day.lastOut) }}</small></span><span class="ss-marks">{{ day.marks }} {{ day.marks === 1 ? 'marca' : 'marcas' }}</span></li>
                  } @empty {
                    <li class="ss-empty"><app-icon name="info" />Sin marcas en los últimos 45 días.</li>
                  }
                </ul>
              } @else {
                <p class="loading-state" role="status">Cargando…</p>
              }
            </section>
          } @else {
            <section class="card ss-card"><p class="muted-copy">Tu vínculo no incluye la consulta de información académica.</p></section>
          }
          <section class="card ss-card" aria-labelledby="school-title">
            <header class="ss-head"><h2 id="school-title"><app-icon name="school" />Inscripción</h2></header>
            @if (v.placement; as p) {
              <p class="ss-place"><strong>{{ p.grade }}{{ p.section ? ' · Sección ' + p.section : '' }}</strong><small>{{ p.year | cycle }}</small></p>
            } @else {
              <p class="muted-copy">{{ academic() ? 'Sin inscripción registrada.' : 'Tu vínculo no incluye la consulta de información académica.' }}</p>
            }
          </section>
          <section class="card ss-card" aria-labelledby="account-title">
            <header class="ss-head"><h2 id="account-title"><app-icon name="wallet" />Estado de cuenta</h2></header>
            @if (statement(); as s) {
              <div class="balance-box inline" [class.zero]="s.totalBalance === 0">
                <small>Saldo pendiente</small><strong>{{ format(s.totalBalance) }}</strong>
              </div>
              <ul class="ss-list">
                @for (charge of openCharges(); track charge.id) {
                  <li><span class="ss-copy"><strong>{{ charge.description }}</strong><small>Vence {{ date(charge.dueOn) }} · {{ statusLabels[charge.status] }}</small></span><strong>{{ format(charge.balance) }}</strong></li>
                } @empty {
                  <li class="ss-empty"><app-icon name="check" />Sin cargos pendientes.</li>
                }
              </ul>
              @if (s.payments.length) {
                <h3 class="subheading">Recibos recientes</h3>
                <ul class="ss-list">
                  @for (payment of s.payments.slice(0, 5); track payment.id) {
                    <li [class.reversed]="payment.reversed">
                      <span class="ss-copy"><strong>No. {{ payment.receiptNumber }} · {{ format(payment.amount) }}</strong><small>{{ date(payment.receivedOn) }} · {{ methods[payment.method] }}{{ payment.reversed ? ' · Revertido' : '' }}</small></span>
                      <a class="text-button" [routerLink]="['/workspace/my/students', v.student.id, 'receipts', payment.id]">Ver recibo <span class="sr-only">{{ payment.receiptNumber }}</span></a>
                    </li>
                  }
                </ul>
              }
            } @else if (v.isSelf) {
              <p class="muted-copy">Consulta tu estado de cuenta con tu encargado o en caja.</p>
            } @else if (!v.faculties.includes('Finance')) {
              <p class="muted-copy">Tu vínculo no incluye la consulta del estado de cuenta.</p>
            } @else {
              <p class="loading-state" role="status">Cargando estado de cuenta…</p>
            }
          </section>
          @if (!v.isSelf) {
            <section class="card ss-card" aria-labelledby="faculties-title">
              <header class="ss-head"><h2 id="faculties-title"><app-icon name="key" />Lo que puedes hacer</h2></header>
              <div class="faculty-chips">
                @for (faculty of v.faculties; track faculty) {
                  <span class="chip">{{ faculties[faculty] }}</span>
                } @empty {
                  <span class="chip empty-chip">Sin facultades asignadas</span>
                }
              </div>
              <p class="muted-copy">Las facultades las asigna el colegio en cada vínculo.</p>
            </section>
          }
        </div>
      } @else {
        <p class="loading-state card" role="status">Cargando…</p>
      }
    </app-shell>
  `,
})
export class MyStudent {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  private readonly school = inject(InstitutionService).current;
  readonly view = signal<StudentAccessView | null>(null);
  readonly statement = signal<Statement | null>(null);
  readonly tasks = signal<StudentTask[] | null>(null);
  readonly grades = signal<MyGrades | null>(null);
  readonly gateDays = signal<MyGateDay[] | null>(null);
  readonly attendance = signal<{ present: number; absent: number; late: number; excused: number } | null>(null);
  readonly notFound = signal(false);
  readonly error = signal('');
  readonly faculties = facultyLabels;
  readonly statusLabels = chargeStatusLabels;
  readonly methods = methodLabels;
  readonly academic = computed(() => {
    const v = this.view();
    return !!v && (v.isSelf || v.faculties.includes('AcademicRecords'));
  });
  readonly upcoming = computed(() =>
    (this.tasks() ?? [])
      .filter((t) => !t.submissionStatus && !t.closed && new Date(t.dueAt).getTime() >= Date.now() - 86400000)
      .sort((a, b) => new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime())
      .slice(0, 4),
  );
  readonly pendingCount = computed(() => (this.tasks() ?? []).filter((t) => !t.submissionStatus && !t.closed).length);
  readonly latest = computed(() => (this.grades()?.periods ?? []).filter((p) => p.status === 'Available').at(-1) ?? null);
  readonly openCharges = computed(() => (this.statement()?.charges ?? []).filter((c) => c.status !== 'Voided' && c.balance > 0).slice(0, 6));

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
      const id = view.student.id;
      if (this.academic()) {
        void this.api.get<StudentTask[]>(`/api/v1/me/students/${id}/assignments`).then((x) => this.tasks.set(x)).catch(() => this.tasks.set([]));
        void this.api.get<MyGrades>(`/api/v1/me/students/${id}/grades`).then((x) => this.grades.set(x)).catch(() => undefined);
        void this.api.get<MyGateDay[]>(`/api/v1/me/students/${id}/gate`).then((x) => this.gateDays.set(x)).catch(() => this.gateDays.set([]));
        void this.api
          .get<{ present: number; absent: number; late: number; excused: number }>(`/api/v1/me/students/${id}/attendance`)
          .then((x) => this.attendance.set(x))
          .catch(() => undefined);
      }
      if (!view.isSelf && view.faculties.includes('Finance'))
        this.statement.set(await this.api.get<Statement>(`/api/v1/me/students/${id}/statement`));
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
  format(value: number): string {
    return money(value, this.statement()?.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  date(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  tone(name: string): number {
    return subjectTone(name);
  }
  fmt(value: number | null): string {
    return points(value);
  }
}
