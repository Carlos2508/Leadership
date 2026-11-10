import { Component, ElementRef, ViewChild, ViewEncapsulation, computed, inject, input, signal, effect, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { Shell } from '../shared/shell';
import { CourseNav } from '../shared/course-nav';
import { Icon } from '../shared/icon';
import { PluralPipe } from '../shared/plural.pipe';

interface AssignmentView {
  id: string;
  title: string;
  instructions: string;
  dueAt: string;
  allowLate: boolean;
  status: 'Draft' | 'Published' | 'Closed';
  recipients: number;
  submitted: number;
  version: string;
  periodId: string | null;
  points: number | null;
  scored: number;
}
interface PeriodOption {
  id: string;
  name: string;
  published: boolean;
}
interface SubmissionRow {
  studentId: string;
  name: string;
  code: string;
  submissionId: string | null;
  text: string | null;
  submittedAt: string | null;
  late: boolean;
  attempts: number;
  status: 'Submitted' | 'Reviewed' | null;
  feedback: string | null;
  version: string | null;
  files: { id: string; fileName: string; size: number }[] | null;
  points: number | null;
  scoreStatus: 'Scored' | 'Missing' | 'Excused' | null;
}

const statusLabels = { Draft: 'Borrador', Published: 'Publicada', Closed: 'Cerrada' } as const;

/** Course activities for the assigned teacher (and academic staff): create, publish, close and review text submissions. */
@Component({
  imports: [Shell, Icon, CourseNav, FormsModule, PluralPipe],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../styles/_course-assignments.scss'],
  template: `
    <app-shell title="Actividades del curso">
      <app-course-nav [courseId]="id()" active="assignments" />
      @if (notFound()) {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" /><h2>No tienes acceso a este curso</h2>
          <p>Solo el personal académico y los docentes asignados pueden ver sus actividades.</p>
        </section>
      } @else {
        <div class="page-heading">
          <div>
            <span class="eyebrow">TAREAS Y ACTIVIDADES</span>
            <h1>{{ courseName() || 'Actividades del curso' }}</h1>
            @if (courseLine()) {
              <p class="course-line">{{ courseLine() }}</p>
            }
            <p>Las publicadas llegan a los estudiantes del curso entre su publicación y la fecha de entrega.</p>
          </div>
          <button class="button" (click)="openEdit()" [disabled]="busy()"><app-icon name="book" />Nueva actividad</button>
        </div>
        @if (notice()) {
          <p class="feedback success-feedback" role="status" tabindex="-1" #noticeRef>{{ notice() }}</p>
        }
        @if (error()) {
          <p class="feedback error-feedback" role="alert">{{ error() }}</p>
        }
        <div class="course-list">
          @for (item of items(); track item.id) {
            <article class="card detail-card assignment-card">
              <div class="section-heading">
                <span class="person-copy"
                  ><strong>{{ item.title }}</strong
                  ><small
                    >Entrega: {{ api.date(item.dueAt) }}{{ item.allowLate ? ' · acepta entregas tardías' : '' }}
                    @if (item.points !== null) {
                      · <strong class="points-tag">Vale {{ item.points }} pts · {{ periodName(item.periodId) }}</strong>
                    }</small
                  ></span
                >
                <span class="pill" [class.gold]="item.status === 'Draft'" [class.mint]="item.status === 'Published'">{{ labels[item.status] }}</span>
              </div>
              @if (item.instructions) {
                <p class="assignment-text">{{ item.instructions }}</p>
              }
              <div class="inline-actions">
                @if (item.status !== 'Draft') {
                  <span class="muted-copy">{{ item.submitted }} de {{ item.recipients | plural: 'entrega' : 'entregas' }}</span>
                  <button class="text-button" (click)="toggle(item)">
                    {{ openId() === item.id ? 'Ocultar entregas' : 'Ver entregas' }} <span class="sr-only">de {{ item.title }}</span>
                  </button>
                }
                @if (item.status !== 'Closed') {
                  <button class="text-button" (click)="openEdit(item)">Editar <span class="sr-only">{{ item.title }}</span></button>
                }
                @if (item.status === 'Draft') {
                  <button class="button compact-button" (click)="advance(item, 'publish')" [disabled]="busy()">
                    Publicar <span class="sr-only">{{ item.title }}</span>
                  </button>
                }
                @if (item.status === 'Published') {
                  <button class="text-button danger-text" (click)="advance(item, 'close')" [disabled]="busy()">
                    Cerrar <span class="sr-only">{{ item.title }}</span>
                  </button>
                }
              </div>
              @if (openId() === item.id) {
                <ul class="payment-list submission-list">
                  @for (row of submissions(); track row.studentId) {
                    <li>
                      <span class="person-copy"
                        ><strong>{{ row.name }}</strong
                        ><small
                          >{{ row.submittedAt ? 'Entregó ' + api.date(row.submittedAt) + (row.late ? ' · tarde' : '') : 'Sin entrega' }}
                          {{ row.status === 'Reviewed' ? '· Revisada' : '' }}</small
                        >
                        @if (row.text) {
                          <span class="assignment-text">{{ row.text }}</span>
                        }
                        @for (file of row.files ?? []; track file.id) {
                          <a class="file-link" [href]="'/api/v1/files/' + file.id" download
                            ><app-icon name="receipt" />{{ file.fileName }} <small>({{ api.size(file.size) }})</small></a
                          >
                        }
                        @if (row.feedback) {
                          <small class="feedback-note">Retroalimentación: {{ row.feedback }}</small>
                        }</span
                      >
                      <span class="submission-actions">
                        @if (item.points !== null) {
                          <span class="score-chip" [class.empty]="!row.scoreStatus">{{
                            row.scoreStatus === 'Scored' ? row.points + ' / ' + item.points + ' pts' : row.scoreStatus === 'Missing' ? 'No entregó · 0 pts' : row.scoreStatus === 'Excused' ? 'Exonerado' : 'Sin calificar'
                          }}</span>
                        }
                        @if (row.submissionId && row.status !== 'Reviewed') {
                          <button class="text-button" (click)="openFeedback(row, item)">
                            {{ item.points !== null ? 'Calificar' : 'Retroalimentar' }} <span class="sr-only">a {{ row.name }}</span>
                          </button>
                        } @else if (item.points !== null && item.status !== 'Draft') {
                          <button class="text-button" (click)="openScore(row, item)">
                            {{ row.scoreStatus ? 'Cambiar punteo' : row.submissionId ? 'Calificar' : 'Registrar punteo' }} <span class="sr-only">de {{ row.name }}</span>
                          </button>
                        }
                      </span>
                    </li>
                  } @empty {
                    <li class="no-recipients">
                      <app-icon name="info" />
                      <span
                        ><strong>Sin estudiantes destinatarios.</strong> La tarea llega a quienes están inscritos en el curso entre su publicación y la
                        fecha de entrega. Revisa que la fecha de entrega esté dentro del ciclo y que el curso tenga estudiantes inscritos.</span
                      >
                    </li>
                  }
                </ul>
              }
            </article>
          } @empty {
            <section class="card empty-state">
              <app-icon name="book" /><h2>Sin actividades</h2>
              <p>Crea la primera actividad; quedará en borrador hasta publicarla.</p>
            </section>
          }
        </div>
      }

      <dialog #assignmentDialog class="account-dialog wide-dialog" (cancel)="onCancel($event)" aria-labelledby="assignment-title">
        <div class="dialog-heading">
          <h2 id="assignment-title">{{ mode() === 'feedback' ? (scoring()?.points !== null && scoring() ? 'Calificar entrega' : 'Retroalimentación') : mode() === 'score' ? 'Registrar punteo' : editing() ? 'Editar actividad' : 'Nueva actividad' }}</h2>
          <button class="icon-button" (click)="close()" aria-label="Cerrar ventana" [disabled]="busy()"><app-icon name="close" /></button>
        </div>
        <form class="access-form" (ngSubmit)="submit()">
          @if (mode() === 'edit') {
            <label class="form-field"><span>Título</span><input name="title" [(ngModel)]="form.title" required maxlength="160" autocomplete="off" /></label>
            <label class="form-field"
              ><span>Instrucciones</span><textarea name="instructions" [(ngModel)]="form.instructions" maxlength="4000" rows="6"></textarea
            ></label>
            <label class="form-field"
              ><span>Fecha y hora de entrega</span><input name="due" type="datetime-local" [(ngModel)]="form.due" required
            /></label>
            <label class="checkbox-field"
              ><input type="checkbox" name="allowLate" [(ngModel)]="form.allowLate" /><span
                >Aceptar entregas tardías<small>Se marcarán como tardías.</small></span
              ></label
            >
            <label class="checkbox-field"
              ><input type="checkbox" name="graded" [(ngModel)]="form.graded" (ngModelChange)="gradedChanged()" [disabled]="(editing()?.scored ?? 0) > 0 || !openPeriods().length" /><span
                >Esta tarea vale puntos<small>Se agrega como actividad de la libreta y su punteo suma a la nota del período.</small></span
              ></label
            >
            @if (form.graded) {
              <div class="form-row two">
                <label class="form-field"
                  ><span>Período</span
                  ><select name="period" [(ngModel)]="form.periodId" required [disabled]="(editing()?.scored ?? 0) > 0">
                    @for (p of openPeriods(); track p.id) {
                      <option [value]="p.id">{{ p.name }}</option>
                    }
                  </select></label
                >
                <label class="form-field"
                  ><span>Punteo (puntos)</span
                  ><input name="points" type="number" min="0.01" max="100" step="0.01" [(ngModel)]="form.points" required
                /></label>
              </div>
            }
          } @else if (mode() === 'feedback') {
            <p class="managed-user">{{ feedbackRow()?.name }}</p>
            @if (scoring()?.points !== null && scoring()) {
              <div class="form-row two">
                <label class="form-field"
                  ><span>Resultado</span
                  ><select name="scoreStatus" [(ngModel)]="scoreForm.status">
                    <option value="Scored">Con punteo</option>
                    <option value="Missing">No entregó (0 pts)</option>
                    <option value="Excused">Exonerado</option>
                  </select></label
                >
                <label class="form-field"
                  ><span>Puntos (de {{ scoring()?.points }})</span
                  ><input name="scorePoints" type="number" min="0" [max]="scoring()?.points ?? 100" step="0.01" [(ngModel)]="scoreForm.points" [disabled]="scoreForm.status !== 'Scored'"
                /></label>
              </div>
            }
            <label class="form-field"
              ><span>Comentario para el estudiante{{ scoring()?.points !== null && scoring() ? ' (opcional)' : '' }}</span><textarea name="feedback" [(ngModel)]="feedback" [required]="!(scoring()?.points !== null && scoring())" maxlength="2000" rows="4"></textarea
            ></label>
          } @else {
            <p class="managed-user">{{ feedbackRow()?.name }}</p>
            <div class="form-row two">
              <label class="form-field"
                ><span>Resultado</span
                ><select name="scoreOnlyStatus" [(ngModel)]="scoreForm.status">
                  <option value="Scored">Con punteo</option>
                  <option value="Missing">No entregó (0 pts)</option>
                  <option value="Excused">Exonerado</option>
                </select></label
              >
              <label class="form-field"
                ><span>Puntos (de {{ scoring()?.points }})</span
                ><input name="scoreOnlyPoints" type="number" min="0" [max]="scoring()?.points ?? 100" step="0.01" [(ngModel)]="scoreForm.points" [disabled]="scoreForm.status !== 'Scored'"
              /></label>
            </div>
          }
          @if (dialogError()) {
            <p class="feedback error-feedback" role="alert">{{ dialogError() }}</p>
          }
          <div class="dialog-actions">
            <button class="button outline" type="button" (click)="close()" [disabled]="busy()">Cancelar</button
            ><button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Guardando…' : 'Guardar' }}</button>
          </div>
        </form>
      </dialog>
    </app-shell>
  `,
})
export class CourseAssignments {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  @ViewChild('assignmentDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;
  readonly items = signal<AssignmentView[]>([]);
  readonly courseName = signal('');
  readonly courseLine = signal('');
  readonly submissions = signal<SubmissionRow[]>([]);
  readonly openId = signal<string | null>(null);
  readonly editing = signal<AssignmentView | null>(null);
  readonly feedbackRow = signal<SubmissionRow | null>(null);
  readonly mode = signal<'edit' | 'feedback' | 'score'>('edit');
  readonly scoring = signal<AssignmentView | null>(null);
  readonly periods = signal<PeriodOption[]>([]);
  readonly openPeriods = computed(() => this.periods().filter((p) => !p.published));
  scoreForm = { status: 'Scored' as 'Scored' | 'Missing' | 'Excused', points: null as number | null };
  readonly notFound = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly labels = statusLabels;
  form = { title: '', instructions: '', due: '', allowLate: false, graded: false, periodId: '', points: 10 as number | null };
  feedback = '';

  constructor() {
    // The same page is reused when switching to another course: reload whenever the id changes.
    effect(() => {
      this.id();
      untracked(() => {
        this.openId.set(null);
        this.submissions.set([]);
        this.items.set([]);
        void this.load();
      });
    });
  }
  private root(): string {
    return `/api/v1/courses/${encodeURIComponent(this.id())}`;
  }
  async load(): Promise<void> {
    try {
      this.items.set(await this.api.get<AssignmentView[]>(`${this.root()}/assignments`));
      if (!this.periods().length) await this.loadPeriods();
      if (!this.courseName())
        void this.api
          .get<{ course: { subject: string; grade: string; section: string | null } }>(`${this.root()}/roster`)
          .then((x) => {
            this.courseName.set(x.course.subject);
            this.courseLine.set(`${x.course.grade} · ${x.course.section ? 'Sección ' + x.course.section : 'Todo el grado'}`);
          })
          .catch(() => undefined);
      const open = this.openId();
      if (open) this.submissions.set((await this.api.get<{ rows: SubmissionRow[] }>(`${this.root()}/assignments/${open}/submissions`)).rows);
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400)) this.notFound.set(true);
      else this.error.set(this.api.message(error));
    }
  }
  async toggle(item: AssignmentView): Promise<void> {
    this.openId.set(this.openId() === item.id ? null : item.id);
    if (this.openId()) await this.load();
  }
  private localValue(iso: string): string {
    const date = new Date(iso);
    const offset = date.getTimezoneOffset() * 60000;
    return new Date(date.getTime() - offset).toISOString().slice(0, 16);
  }
  openEdit(item?: AssignmentView): void {
    this.mode.set('edit');
    this.editing.set(item ?? null);
    const firstOpen = this.openPeriods()[0]?.id ?? '';
    this.form = item
      ? {
          title: item.title,
          instructions: item.instructions,
          due: this.localValue(item.dueAt),
          allowLate: item.allowLate,
          graded: item.points !== null,
          periodId: item.periodId ?? firstOpen,
          points: item.points ?? 10,
        }
      : { title: '', instructions: '', due: '', allowLate: false, graded: false, periodId: firstOpen, points: 10 };
    this.dialogError.set('');
    this.dialog.nativeElement.showModal();
  }
  /** Periods of the course's cycle (to say what a homework is worth); a cycle without periods simply offers none. */
  private async loadPeriods(): Promise<void> {
    try {
      this.periods.set((await this.api.get<{ periods: PeriodOption[] }>(`${this.root()}/activities`)).periods);
    } catch {
      this.periods.set([]);
    }
  }
  gradedChanged(): void {
    if (this.form.graded && !this.form.periodId) this.form.periodId = this.openPeriods()[0]?.id ?? '';
  }
  periodName(id: string | null): string {
    return this.periods().find((p) => p.id === id)?.name ?? 'período';
  }
  openFeedback(row: SubmissionRow, item?: AssignmentView): void {
    this.mode.set('feedback');
    this.feedbackRow.set(row);
    this.scoring.set(item ?? null);
    this.scoreForm = { status: row.scoreStatus ?? 'Scored', points: row.points };
    this.feedback = '';
    this.dialogError.set('');
    this.dialog.nativeElement.showModal();
  }
  openScore(row: SubmissionRow, item: AssignmentView): void {
    this.mode.set('score');
    this.feedbackRow.set(row);
    this.scoring.set(item);
    this.scoreForm = { status: row.scoreStatus ?? (row.submissionId ? 'Scored' : 'Missing'), points: row.points };
    this.dialogError.set('');
    this.dialog.nativeElement.showModal();
  }
  close(): void {
    if (!this.busy()) this.dialog.nativeElement.close();
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.close();
  }
  private done(message: string): void {
    this.alerts.success(message);
  }
  async submit(): Promise<void> {
    if (this.busy()) return;
    if (this.mode() === 'edit' && !this.form.due) {
      this.dialogError.set('Indica la fecha y hora de entrega.');
      return;
    }
    this.busy.set(true);
    this.dialogError.set('');
    try {
      if (this.mode() === 'edit') {
        const item = this.editing();
        const body = {
          title: this.form.title,
          instructions: this.form.instructions,
          dueAt: new Date(this.form.due).toISOString(),
          allowLate: this.form.allowLate,
          version: item?.version ?? null,
          periodId: this.form.graded ? this.form.periodId || null : null,
          points: this.form.graded ? Number(this.form.points) : null,
        };
        await this.api.post(item ? `${this.root()}/assignments/${item.id}` : `${this.root()}/assignments`, body);
      } else if (this.mode() === 'feedback') {
        const row = this.feedbackRow()!;
        const graded = this.scoring()?.points !== null && !!this.scoring();
        await this.api.post(`${this.root()}/submissions/${row.submissionId}/feedback`, {
          feedback: this.feedback,
          version: row.version,
          scoreStatus: graded ? this.scoreForm.status : null,
          points: graded && this.scoreForm.status === 'Scored' ? Number(this.scoreForm.points) : null,
        });
      } else {
        const row = this.feedbackRow()!;
        await this.api.post(`${this.root()}/assignments/${this.scoring()!.id}/scores`, {
          studentId: row.studentId,
          status: this.scoreForm.status,
          points: this.scoreForm.status === 'Scored' ? Number(this.scoreForm.points) : null,
        });
      }
      this.busy.set(false);
      this.dialog.nativeElement.close();
      await this.load();
      this.done(this.mode() === 'edit' ? 'La actividad se guardó.' : this.mode() === 'score' ? 'El punteo quedó registrado.' : 'La retroalimentación se envió.');
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  async advance(item: AssignmentView, action: 'publish' | 'close'): Promise<void> {
    if (this.busy()) return;
    const ok = await this.alerts.confirm(
      action === 'publish'
        ? { title: `¿Publicar «${item.title}»?`, text: 'Los estudiantes del curso la verán y podrán entregar.', confirmText: 'Publicar actividad' }
        : { title: `¿Cerrar «${item.title}»?`, text: 'Ya no se recibirán entregas nuevas ni cambios.', confirmText: 'Cerrar actividad', danger: true },
    );
    if (!ok) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post(`${this.root()}/assignments/${item.id}/${action}`, { version: item.version });
      await this.load();
      this.done(action === 'publish' ? 'La actividad se publicó.' : 'La actividad se cerró.');
    } catch (error) {
      void this.alerts.error('No se pudo completar la acción', this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
