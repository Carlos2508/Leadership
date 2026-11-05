import { Component, ElementRef, ViewChild, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { InstitutionService } from '../core/institution';
import { facultyLabels, formatDateOnly, relationshipLabels } from '../core/people-api';
import { Statement, chargeStatusLabels, methodLabels, money } from '../core/finance-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';
import { MyGateDay } from '../core/gate-api';

interface StudentAccessView {
  student: { id: string; displayName: string; studentCode: string; relationship: string };
  isSelf: boolean;
  faculties: string[];
  placement: { year: string; grade: string; section: string | null } | null;
}

interface StudentTask {
  id: string;
  course: string;
  title: string;
  instructions: string;
  dueAt: string;
  allowLate: boolean;
  closed: boolean;
  submissionStatus: 'Submitted' | 'Reviewed' | null;
  submittedAt: string | null;
  late: boolean;
  text: string | null;
  feedback: string | null;
  version: string | null;
  canSubmit: boolean;
}

/** Self-service view of one student for the student or a linked guardian; each section depends on a faculty. */
@Component({
  imports: [Shell, Icon, RouterLink, FormsModule, CyclePipe],
  template: `
    <app-shell [title]="view()?.student?.displayName ?? 'Estudiante'">
      <a class="back-link" routerLink="/workspace"><app-icon name="arrow" />Volver a mi espacio</a>
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>No tienes acceso a esta información</h2>
          <p>Si crees que es un error, comunícate con la administración del colegio.</p>
        </section>
      } @else if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      } @else if (view(); as v) {
        <div class="page-heading">
          <div>
            <span class="eyebrow">{{ v.isSelf ? 'MI FICHA' : 'ESTUDIANTE A MI CARGO' }}</span>
            <h1>{{ v.student.displayName }}</h1>
            <p class="role-list">
              <span class="pill blue">{{ v.student.studentCode }}</span>
              @if (!v.isSelf) {
                <span class="pill purple">{{ relationships[v.student.relationship] }}</span>
              }
            </p>
          </div>
        </div>
        <div class="detail-grid">
          <div class="detail-main">
            <section class="card detail-card" aria-labelledby="grades-title">
              <div class="card-title-row">
                <h2 id="grades-title">Calificaciones</h2>
                @if (grades()?.periods?.length) {
                  <a class="button outline compact-button" [href]="'/api/v1/me/students/' + id() + '/grades.pdf'" target="_blank" rel="noopener"
                    >Descargar PDF</a
                  >
                }
              </div>
              @if (grades(); as g) {
                @for (period of g.periods; track period.periodId) {
                  <div class="grade-period">
                    <h3 class="subheading">{{ period.name }}</h3>
                    @if (period.status === 'Available') {
                      <ul class="payment-list">
                        @for (grade of period.grades; track grade.subject) {
                          <li>
                            <span class="person-copy"><strong>{{ grade.subject }}</strong></span>
                            <strong class="grade-value">{{
                              grade.status === 'Scored' ? grade.score : grade.status === 'Excused' ? 'Exonerado' : 'Pendiente'
                            }}</strong>
                          </li>
                        }
                      </ul>
                    } @else {
                      <div class="blocked-grades" role="note">
                        <app-icon name="shield" />
                        <div>
                          <strong>Las calificaciones de este período están publicadas</strong>
                          <p>
                            {{
                              period.status === 'PendingFinancialSetup'
                                ? 'El colegio está completando la configuración de cuotas. Comunícate con caja.'
                                : 'Para consultarlas se necesitan cubiertas las colegiaturas requeridas para esta entrega.'
                            }}
                          </p>
                          @for (item of period.pending; track item.chargeId) {
                            <small>{{ item.label }} · {{ format(item.balance) }}</small>
                          }
                          @if (v.isSelf) {
                            <small>Consulta con tu encargado o en caja.</small>
                          }
                          <small>El acceso a tareas y entregas continúa disponible.</small>
                        </div>
                      </div>
                    }
                  </div>
                } @empty {
                  <p class="muted-copy">Aún no hay calificaciones publicadas.</p>
                }
              } @else if (!v.isSelf && !v.faculties.includes('AcademicRecords')) {
                <p class="muted-copy">Tu vínculo no incluye la consulta de información académica.</p>
              } @else {
                <p class="loading-state" role="status">Cargando calificaciones…</p>
              }
            </section>
            <section class="card detail-card" aria-labelledby="tasks-title">
              <h2 id="tasks-title">Tareas</h2>
              @if (taskNotice()) {
                <p class="feedback success-feedback" role="status">{{ taskNotice() }}</p>
              }
              @if (tasks(); as list) {
                <ul class="payment-list task-list">
                  @for (task of list; track task.id) {
                    <li>
                      <span class="person-copy"
                        ><strong>{{ task.title }}</strong
                        ><small
                          >{{ task.course }} · entrega {{ instant(task.dueAt) }}{{ task.closed ? ' · cerrada' : '' }}</small
                        >
                        @if (v.isSelf && task.instructions) {
                          <span class="assignment-text">{{ task.instructions }}</span>
                        }
                        @if (task.feedback) {
                          <small class="feedback-note">Retroalimentación: {{ task.feedback }}</small>
                        }</span
                      >
                      <span
                        class="pill"
                        [class.mint]="task.submissionStatus"
                        [class.gold]="!task.submissionStatus"
                        >{{
                          task.submissionStatus === 'Reviewed'
                            ? 'Revisada'
                            : task.submissionStatus
                              ? task.late
                                ? 'Entregada tarde'
                                : 'Entregada'
                              : 'Pendiente'
                        }}</span
                      >
                      @if (task.canSubmit) {
                        <button class="button outline compact-button" (click)="openSubmit(task)">
                          {{ task.submissionStatus ? 'Actualizar' : 'Entregar' }} <span class="sr-only">{{ task.title }}</span>
                        </button>
                      }
                    </li>
                  } @empty {
                    <li><span class="muted-copy">No hay tareas publicadas.</span></li>
                  }
                </ul>
              } @else if (!v.isSelf && !v.faculties.includes('AcademicRecords')) {
                <p class="muted-copy">Tu vínculo no incluye el seguimiento de tareas.</p>
              } @else {
                <p class="loading-state" role="status">Cargando tareas…</p>
              }
            </section>
          </div>
          <aside class="detail-side">
            @if (v.isSelf || v.faculties.includes('AcademicRecords')) {
              <section class="card detail-card" aria-labelledby="gate-title">
                <h2 id="gate-title">Entradas y salidas</h2><p class="muted-copy">Días con marcas en los últimos 45 días.</p>
                @if (gateError()) { <p class="feedback error-feedback" role="alert">{{ gateError() }} <button class="text-button" (click)="loadGate()">Reintentar</button></p> }
                @else if (gateDays(); as days) {
                  <ul class="payment-list">@for (day of days; track day.date) {
                    <li><span class="person-copy"><strong>{{ date(day.date) }}</strong><small>Entrada {{ api.time(day.firstIn) }} · Salida {{ api.time(day.lastOut) }}</small></span><span>{{ day.marks }} {{ day.marks === 1 ? 'marca' : 'marcas' }}</span></li>
                  } @empty { <li>Aún no hay entradas o salidas registradas en este período.</li> }</ul>
                } @else { <p role="status">Cargando entradas y salidas…</p> }
              </section>
            }
            <section class="card detail-card" aria-labelledby="attendance-title">
              <h2 id="attendance-title">Asistencia</h2>
              @if (attendance(); as a) {
                <div class="attendance-summary">
                  <span><strong>{{ a.present }}</strong> {{ a.present === 1 ? 'presente' : 'presentes' }}</span>
                  <span><strong>{{ a.absent }}</strong> {{ a.absent === 1 ? 'ausencia' : 'ausencias' }}</span>
                  <span><strong>{{ a.late }}</strong> {{ a.late === 1 ? 'tardanza' : 'tardanzas' }}</span>
                  <span><strong>{{ a.excused }}</strong> {{ a.excused === 1 ? 'justificada' : 'justificadas' }}</span>
                </div>
                @if (a.incidents.length) {
                  <ul class="payment-list">
                    @for (item of a.incidents; track $index) {
                      <li>
                        <span class="person-copy"
                          ><strong>{{ date(item.date) }} · {{ item.subject }}</strong
                          >@if (item.note) {
                            <small>{{ item.note }}</small>
                          }</span
                        >
                        <span class="pill" [class.suspended]="item.status === 'Absent'" [class.gold]="item.status !== 'Absent'">{{
                          attendanceLabels[item.status]
                        }}</span>
                      </li>
                    }
                  </ul>
                }
              } @else if (!v.isSelf && !v.faculties.includes('AcademicRecords')) {
                <p class="muted-copy">Tu vínculo no incluye la consulta de asistencia.</p>
              } @else {
                <p class="loading-state" role="status">Cargando asistencia…</p>
              }
            </section>
            <section class="card detail-card" aria-labelledby="school-title">
              <h2 id="school-title">Inscripción</h2>
              @if (v.placement; as p) {
                <p class="placement-line">{{ p.year | cycle }} · {{ p.grade }} {{ p.section ? '· Sección ' + p.section : '' }}</p>
              } @else if (v.isSelf || v.faculties.includes('AcademicRecords')) {
                <p class="muted-copy">Sin inscripción registrada.</p>
              } @else {
                <p class="muted-copy">Tu vínculo no incluye la consulta de información académica.</p>
              }
            </section>
            <section class="card detail-card" aria-labelledby="account-title">
              <h2 id="account-title">Estado de cuenta</h2>
              @if (statement(); as s) {
                <div class="balance-box inline" [class.zero]="s.totalBalance === 0">
                  <small>Saldo pendiente</small><strong>{{ format(s.totalBalance) }}</strong>
                </div>
                <ul class="payment-list">
                  @for (charge of s.charges; track charge.id) {
                    @if (charge.status !== 'Voided') {
                      <li>
                        <span class="person-copy"
                          ><strong>{{ charge.description }}</strong
                          ><small>Vence {{ date(charge.dueOn) }} · {{ statusLabels[charge.status] }}</small></span
                        >
                        <strong>{{ format(charge.balance) }}</strong>
                      </li>
                    }
                  } @empty {
                    <li><span class="muted-copy">Sin cargos emitidos.</span></li>
                  }
                </ul>
                @if (s.payments.length) {
                  <h3 class="subheading">Recibos</h3>
                  <ul class="payment-list">
                    @for (payment of s.payments; track payment.id) {
                      <li [class.reversed]="payment.reversed">
                        <span class="person-copy"
                          ><strong>No. {{ payment.receiptNumber }} · {{ format(payment.amount) }}</strong
                          ><small>{{ date(payment.receivedOn) }} · {{ methods[payment.method] }}{{ payment.reversed ? ' · Revertido' : '' }}</small></span
                        >
                        <a class="text-button" [routerLink]="['/workspace/my/students', v.student.id, 'receipts', payment.id]"
                          >Ver recibo <span class="sr-only">{{ payment.receiptNumber }}</span></a
                        >
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
              <section class="card detail-card" aria-labelledby="faculties-title">
                <h2 id="faculties-title">Lo que puedes hacer</h2>
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
          </aside>
        </div>
      } @else {
        <p class="loading-state card" role="status">Cargando…</p>
      }
      <dialog #submitDialog class="account-dialog wide-dialog" aria-labelledby="submit-title" (cancel)="onCancel($event)">
        <div class="dialog-heading">
          <h2 id="submit-title">{{ selectedTask()?.title }}</h2>
          <button class="icon-button" (click)="closeSubmit()" aria-label="Cerrar ventana" [disabled]="busy()"><app-icon name="close" /></button>
        </div>
        <form class="access-form" (ngSubmit)="submitTask()">
          @if (selectedTask()?.instructions) {
            <p class="assignment-text">{{ selectedTask()?.instructions }}</p>
          }
          <label class="form-field"
            ><span>Tu entrega</span><textarea name="submission" [(ngModel)]="submission" required maxlength="8000" rows="8"></textarea
          ></label>
          @if (selectedTask()?.submissionStatus) {
            <fieldset class="choice-group">
              <legend>Archivos adjuntos</legend>
              @for (file of files(); track file.id) {
                <div class="file-row">
                  <app-icon name="receipt" />
                  <a [href]="'/api/v1/files/' + file.id" download>{{ file.fileName }}</a>
                  <small>{{ api.size(file.size) }}</small>
                  <button type="button" class="text-button danger-text" (click)="removeFile(file.id)" [disabled]="busy()">
                    Quitar <span class="sr-only">{{ file.fileName }}</span>
                  </button>
                </div>
              } @empty {
                <p class="muted-copy">Sin archivos. Puedes adjuntar PDF, imágenes, documentos de Office o texto (máx. 10 MB).</p>
              }
              <label class="file-picker"
                ><span class="button outline compact-button">Adjuntar archivo</span
                ><input type="file" accept=".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.pptx,.txt" (change)="uploadFile($event)" [disabled]="busy()"
              /></label>
            </fieldset>
          } @else {
            <p class="muted-copy">Después de enviar el texto podrás adjuntar archivos.</p>
          }
          @if (submitError()) {
            <p class="feedback error-feedback" role="alert">{{ submitError() }}</p>
          }
          <div class="dialog-actions">
            <button class="button outline" type="button" (click)="closeSubmit()" [disabled]="busy()">Cerrar</button
            ><button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Enviando…' : selectedTask()?.submissionStatus ? 'Actualizar texto' : 'Enviar entrega' }}</button>
          </div>
        </form>
      </dialog>
    </app-shell>
  `,
})
export class MyStudent {
  readonly gateDays = signal<MyGateDay[] | null>(null);
  readonly gateError = signal('');
  async loadGate(): Promise<void> {
    this.gateError.set('');
    try { this.gateDays.set(await this.api.get('/api/v1/me/students/' + this.id() + '/gate')); }
    catch (e) { this.gateError.set(this.api.message(e)); }
  }
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly school = inject(InstitutionService).current;
  readonly view = signal<StudentAccessView | null>(null);
  readonly statement = signal<Statement | null>(null);
  @ViewChild('submitDialog') private submitDialog?: ElementRef<HTMLDialogElement>;
  readonly tasks = signal<StudentTask[] | null>(null);
  readonly attendance = signal<{
    present: number;
    absent: number;
    late: number;
    excused: number;
    incidents: { date: string; subject: string; status: string; note: string }[];
  } | null>(null);
  readonly attendanceLabels: Record<string, string> = { Absent: 'Ausente', Late: 'Tarde', Excused: 'Justificada', Present: 'Presente' };
  readonly selectedTask = signal<StudentTask | null>(null);
  readonly busy = signal(false);
  readonly submitError = signal('');
  readonly taskNotice = signal('');
  readonly files = signal<{ id: string; fileName: string; size: number }[]>([]);
  submission = '';
  readonly grades = signal<{
    year: string | null;
    periods: {
      periodId: string;
      name: string;
      status: 'Available' | 'Blocked' | 'PendingFinancialSetup';
      grades: { subject: string; score: number | null; status: string }[];
      pending: { chargeId: string | null; label: string; balance: number }[];
    }[];
  } | null>(null);
  readonly notFound = signal(false);
  readonly error = signal('');
  readonly faculties = facultyLabels;
  readonly relationships = relationshipLabels;
  readonly statusLabels = chargeStatusLabels;
  readonly methods = methodLabels;

  constructor() {
    queueMicrotask(() => void this.load());
  }
  format(value: number): string {
    return money(value, this.statement()?.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  instant(value: string): string {
    return this.api.date(value);
  }
  async loadTasks(studentId: string): Promise<void> {
    this.tasks.set(await this.api.get<StudentTask[]>(`/api/v1/me/students/${studentId}/assignments`));
  }
  openSubmit(task: StudentTask): void {
    this.selectedTask.set(task);
    this.submission = task.text ?? '';
    this.submitError.set('');
    this.files.set([]);
    if (task.submissionStatus) void this.loadFiles(task.id);
    this.submitDialog?.nativeElement.showModal();
  }
  async loadFiles(taskId: string): Promise<void> {
    this.files.set(await this.api.get(`/api/v1/me/assignments/${taskId}/files`));
  }
  async uploadFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const task = this.selectedTask();
    if (!file || !task || this.busy()) return;
    this.busy.set(true);
    this.submitError.set('');
    try {
      const data = new FormData();
      data.append('file', file);
      await this.api.upload(`/api/v1/me/assignments/${task.id}/files`, data);
      await this.loadFiles(task.id);
    } catch (error) {
      this.submitError.set(this.api.message(error));
    } finally {
      input.value = '';
      this.busy.set(false);
    }
  }
  async removeFile(fileId: string): Promise<void> {
    const task = this.selectedTask();
    if (!task || this.busy()) return;
    if (!(await this.alerts.confirm({ title: '¿Quitar este archivo de tu entrega?', confirmText: 'Quitar archivo', danger: true }))) return;
    this.busy.set(true);
    try {
      await this.api.post(`/api/v1/me/assignments/${task.id}/files/${fileId}/remove`, {});
      await this.loadFiles(task.id);
      this.alerts.success('Archivo quitado de la entrega.');
    } catch (error) {
      this.submitError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  closeSubmit(): void {
    if (!this.busy()) this.submitDialog?.nativeElement.close();
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeSubmit();
  }
  async submitTask(): Promise<void> {
    const task = this.selectedTask();
    const view = this.view();
    if (!task || !view || this.busy()) return;
    this.busy.set(true);
    this.submitError.set('');
    try {
      await this.api.post(`/api/v1/me/assignments/${task.id}/submission`, { text: this.submission, version: task.version });
      this.taskNotice.set('Tu entrega quedó registrada.');
      await this.loadTasks(view.student.id);
      const updated = this.tasks()?.find((x) => x.id === task.id) ?? null;
      this.selectedTask.set(updated);
      if (updated?.submissionStatus && !task.submissionStatus) {
        // First submission: stay in the dialog so the student can attach files.
        void this.loadFiles(updated.id);
        this.submitError.set('');
      } else {
        this.busy.set(false);
        this.submitDialog?.nativeElement.close();
      }
    } catch (error) {
      this.submitError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  date(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  async load(): Promise<void> {
    try {
      const view = await this.api.get<StudentAccessView>(`/api/v1/me/students/${encodeURIComponent(this.id())}`);
      this.view.set(view);
      if (view.isSelf || view.faculties.includes('AcademicRecords')) void this.loadGate();
      if (view.isSelf || view.faculties.includes('AcademicRecords')) {
        void this.loadTasks(view.student.id);
        void this.api.get<NonNullable<ReturnType<typeof this.attendance>>>(`/api/v1/me/students/${view.student.id}/attendance`)
          .then((x) => this.attendance.set(x));
      }
      if (view.isSelf || view.faculties.includes('AcademicRecords'))
        this.grades.set(await this.api.get(`/api/v1/me/students/${view.student.id}/grades`));
      if (!view.isSelf && view.faculties.includes('Finance'))
        this.statement.set(await this.api.get<Statement>(`/api/v1/me/students/${view.student.id}/statement`));
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
}
