import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { InstitutionService } from '../core/institution';
import { GradeView, YearSummary } from '../core/academic-api';
import { CourseView } from './courses';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { PluralPipe } from '../shared/plural.pipe';

export interface FeedItem {
  id: string;
  title: string;
  body: string;
  audience: string;
  publishedAt: string;
  author: string;
  read: boolean;
  students: string[];
}
interface AnnouncementView {
  id: string;
  title: string;
  body: string;
  audienceKind: 'School' | 'Grade' | 'Course';
  academicYearId: string | null;
  audienceId: string | null;
  audience: string;
  forStudents: boolean;
  forGuardians: boolean;
  forStaff: boolean;
  status: 'Draft' | 'Published' | 'Archived';
  createdAt: string;
  author: string;
  publishedAt: string | null;
  reads: number;
  canEdit: boolean;
  version: string;
}
interface Options {
  canPublishSchool: boolean;
  courses: CourseView[];
}

/** Personal inbox of in-app notices plus authoring for staff who may publish (school/grade or their own courses). */
@Component({
  imports: [Shell, Icon, FormsModule, PluralPipe],
  template: `
    <app-shell title="Avisos">
      <div class="page-heading">
        <div>
          <span class="eyebrow">COMUNICACIÓN</span>
          <h1>Avisos</h1>
          <p>Mensajes del colegio dentro de la plataforma. No se envían por correo ni SMS.</p>
        </div>
        @if (canAuthor()) {
          <button class="button" (click)="openEdit()" [disabled]="busy()"><app-icon name="mail" />Nuevo aviso</button>
        }
      </div>
      @if (notice()) {
        <p class="feedback success-feedback" role="status" tabindex="-1" #noticeRef>{{ notice() }}</p>
      }
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      @if (canAuthor()) {
        <div class="period-tabs" role="tablist" aria-label="Vista de avisos">
          <button role="tab" type="button" [attr.aria-selected]="tab() === 'inbox'" [class.active]="tab() === 'inbox'" (click)="tab.set('inbox')">
            Recibidos
            @if (unread()) {
              <span class="pill gold">{{ unread() }} sin leer</span>
            }
          </button>
          <button role="tab" type="button" [attr.aria-selected]="tab() === 'authored'" [class.active]="tab() === 'authored'" (click)="tab.set('authored')">
            Redactados
          </button>
        </div>
      }
      @if (tab() === 'inbox') {
        <section class="announcement-list" aria-label="Avisos recibidos">
          @for (item of feed(); track item.id) {
            <article class="card announcement-card" [class.unread]="!item.read">
              <button class="announcement-toggle" (click)="toggle(item)" [attr.aria-expanded]="openId() === item.id">
                <span class="unread-dot" [class.hidden-dot]="item.read" aria-hidden="true"></span>
                <span class="person-copy"
                  ><strong>{{ item.title }}</strong
                  ><small>{{ item.author }} · {{ api.date(item.publishedAt) }} · {{ item.audience }}
                    {{ item.students.length ? '· ' + item.students.join(', ') : '' }}</small></span
                >
                <span class="sr-only">{{ item.read ? 'Leído' : 'Sin leer' }}</span>
                <app-icon name="arrow" />
              </button>
              @if (openId() === item.id) {
                <p class="assignment-text announcement-body">{{ item.body }}</p>
              }
            </article>
          } @empty {
            <section class="card empty-state">
              <app-icon name="mail" />
              <h2>No tienes avisos</h2>
              <p>Cuando el colegio publique un aviso para ti, aparecerá aquí.</p>
            </section>
          }
        </section>
      } @else {
        <section class="announcement-list" aria-label="Avisos redactados">
          @for (item of authored(); track item.id) {
            <article class="card detail-card assignment-card">
              <div class="section-heading">
                <span class="person-copy"
                  ><strong>{{ item.title }}</strong
                  ><small>{{ item.audience }} · {{ recipients(item) }} · {{ item.author }}</small></span
                >
                <span class="pill" [class.gold]="item.status === 'Draft'" [class.mint]="item.status === 'Published'">{{ statusLabels[item.status] }}</span>
              </div>
              <p class="assignment-text">{{ item.body }}</p>
              <div class="inline-actions">
                @if (item.status === 'Published') {
                  <span class="muted-copy">Publicado {{ api.date(item.publishedAt) }} · {{ item.reads | plural: 'lectura' : 'lecturas' }}</span>
                }
                @if (item.canEdit && item.status === 'Draft') {
                  <button class="text-button" (click)="openEdit(item)">Editar <span class="sr-only">{{ item.title }}</span></button>
                  <button class="button compact-button" (click)="advance(item, 'publish')" [disabled]="busy()">
                    Publicar <span class="sr-only">{{ item.title }}</span>
                  </button>
                }
                @if (item.canEdit && item.status !== 'Archived') {
                  <button class="text-button danger-text" (click)="advance(item, 'archive')" [disabled]="busy()">
                    Archivar <span class="sr-only">{{ item.title }}</span>
                  </button>
                }
              </div>
            </article>
          } @empty {
            <section class="card empty-state">
              <app-icon name="mail" /><h2>Sin avisos redactados</h2>
              <p>Usa «Nuevo aviso»; quedará en borrador hasta publicarlo.</p>
            </section>
          }
        </section>
      }

      <dialog #editDialog class="account-dialog wide-dialog" (cancel)="onCancel($event)" aria-labelledby="announcement-title">
        <div class="dialog-heading">
          <h2 id="announcement-title">{{ editing() ? 'Editar aviso' : 'Nuevo aviso' }}</h2>
          <button class="icon-button" (click)="close()" aria-label="Cerrar ventana" [disabled]="busy()"><app-icon name="close" /></button>
        </div>
        <form class="access-form" (ngSubmit)="save()">
          <label class="form-field"><span>Título</span><input name="title" [(ngModel)]="form.title" required maxlength="160" autocomplete="off" /></label>
          <label class="form-field"
            ><span>Mensaje</span><textarea name="body" [(ngModel)]="form.body" required maxlength="4000" rows="6"></textarea
          ></label>
          <div class="form-grid">
            <label class="form-field"
              ><span>Audiencia</span
              ><select name="kind" [(ngModel)]="form.audienceKind" (ngModelChange)="form.audienceId = ''">
                @if (options()?.canPublishSchool) {
                  <option value="School">Todo el colegio</option>
                  <option value="Grade">Un grado</option>
                }
                <option value="Course">Un curso</option>
              </select></label
            >
            @if (form.audienceKind === 'Grade') {
              <label class="form-field"
                ><span>Grado</span
                ><select name="grade" [(ngModel)]="form.audienceId" required>
                  <option value="" disabled>Selecciona</option>
                  @for (grade of grades(); track grade.id) {
                    <option [value]="grade.id">{{ grade.name }}</option>
                  }
                </select></label
              >
            }
            @if (form.audienceKind === 'Course') {
              <label class="form-field"
                ><span>Curso</span
                ><select name="course" [(ngModel)]="form.audienceId" required>
                  <option value="" disabled>Selecciona</option>
                  @for (course of courseChoices(); track course.id) {
                    <option [value]="course.id">{{ course.subject }} · {{ course.grade }}{{ course.section ? ' · ' + course.section : '' }}</option>
                  }
                </select></label
              >
            }
          </div>
          <fieldset class="choice-group">
            <legend>Destinatarios</legend>
            <label class="checkbox-field"><input type="checkbox" name="students" [(ngModel)]="form.forStudents" /><span>{{ school.terms.studentPlural }}</span></label>
            <label class="checkbox-field"
              ><input type="checkbox" name="guardians" [(ngModel)]="form.forGuardians" /><span
                >{{ school.terms.guardianPlural }}<small>Solo quienes tengan la facultad «Recibir comunicaciones».</small></span
              ></label
            >
            @if (form.audienceKind === 'School') {
              <label class="checkbox-field"><input type="checkbox" name="staff" [(ngModel)]="form.forStaff" /><span>Personal del colegio</span></label>
            }
          </fieldset>
          @if (dialogError()) {
            <p class="feedback error-feedback" role="alert">{{ dialogError() }}</p>
          }
          <div class="dialog-actions">
            <button class="button outline" type="button" (click)="close()" [disabled]="busy()">Cancelar</button
            ><button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Guardando…' : 'Guardar borrador' }}</button>
          </div>
        </form>
      </dialog>
    </app-shell>
  `,
})
export class Announcements {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly school = inject(InstitutionService).current;
  @ViewChild('editDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;
  readonly feed = signal<FeedItem[]>([]);
  readonly authored = signal<AnnouncementView[]>([]);
  readonly options = signal<Options | null>(null);
  readonly grades = signal<GradeView[]>([]);
  readonly years = signal<YearSummary[]>([]);
  readonly openId = signal<string | null>(null);
  readonly editing = signal<AnnouncementView | null>(null);
  readonly tab = signal<'inbox' | 'authored'>('inbox');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly canAuthor = computed(() => !!this.options()?.canPublishSchool || !!this.options()?.courses.length);
  readonly unread = computed(() => this.feed().filter((x) => !x.read).length);
  readonly courseChoices = computed(() => this.options()?.courses ?? []);
  readonly statusLabels = { Draft: 'Borrador', Published: 'Publicado', Archived: 'Archivado' };
  form = this.empty();

  constructor() {
    queueMicrotask(() => void this.load());
  }
  private empty() {
    return { title: '', body: '', audienceKind: 'School' as 'School' | 'Grade' | 'Course', audienceId: '', forStudents: true, forGuardians: true, forStaff: false };
  }
  recipients(item: AnnouncementView): string {
    return [item.forStudents ? 'estudiantes' : '', item.forGuardians ? 'encargados' : '', item.forStaff ? 'personal' : '']
      .filter(Boolean).join(', ');
  }
  async load(): Promise<void> {
    try {
      const [feed, options] = await Promise.all([
        this.api.get<FeedItem[]>('/api/v1/me/announcements'),
        this.api.get<Options>('/api/v1/announcements/options'),
      ]);
      this.feed.set(feed);
      this.options.set(options);
      if (options.canPublishSchool || options.courses.length) this.authored.set(await this.api.get<AnnouncementView[]>('/api/v1/announcements'));
      if (options.canPublishSchool && !this.grades().length) {
        const [grades, years] = await Promise.all([
          this.api.get<GradeView[]>('/api/v1/academic/grades').catch(() => []),
          this.api.get<YearSummary[]>('/api/v1/academic/years').catch(() => []),
        ]);
        this.grades.set(grades.filter((x) => x.isActive));
        this.years.set(years);
      }
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }
  async toggle(item: FeedItem): Promise<void> {
    this.openId.set(this.openId() === item.id ? null : item.id);
    if (!item.read && this.openId() === item.id) {
      try {
        await this.api.post(`/api/v1/me/announcements/${item.id}/read`, {});
        this.feed.update((list) => list.map((x) => (x.id === item.id ? { ...x, read: true } : x)));
      } catch {
        /* Reading still works; the receipt will be retried next time. */
      }
    }
  }
  openEdit(item?: AnnouncementView): void {
    this.editing.set(item ?? null);
    this.form = item
      ? { title: item.title, body: item.body, audienceKind: item.audienceKind, audienceId: item.audienceId ?? '', forStudents: item.forStudents, forGuardians: item.forGuardians, forStaff: item.forStaff }
      : { ...this.empty(), audienceKind: this.options()?.canPublishSchool ? 'School' : 'Course' };
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
  private yearFor(): string | null {
    if (this.form.audienceKind === 'Course') return this.courseChoices().find((x) => x.id === this.form.audienceId)?.academicYearId ?? null;
    if (this.form.audienceKind === 'Grade') return (this.years().find((x) => x.isDefault) ?? this.years()[0])?.id ?? null;
    return null;
  }
  async save(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.dialogError.set('');
    try {
      const item = this.editing();
      const body = { ...this.form, audienceId: this.form.audienceId || null, academicYearId: this.yearFor(), version: item?.version ?? null };
      await this.api.post(item ? `/api/v1/announcements/${item.id}` : '/api/v1/announcements', body);
      this.busy.set(false);
      this.dialog.nativeElement.close();
      this.tab.set('authored');
      await this.load();
      this.done('El aviso quedó en borrador. Publícalo cuando esté listo.');
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  async advance(item: AnnouncementView, action: 'publish' | 'archive'): Promise<void> {
    if (this.busy()) return;
    const ok = await this.alerts.confirm(
      action === 'publish'
        ? { title: `¿Publicar «${item.title}»?`, text: 'Lo verán sus destinatarios dentro de la plataforma y ya no se podrá editar.', confirmText: 'Publicar aviso' }
        : { title: `¿Archivar «${item.title}»?`, text: 'Dejará de mostrarse a sus destinatarios.', confirmText: 'Archivar', danger: true },
    );
    if (!ok) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post(`/api/v1/announcements/${item.id}/${action}`, { version: item.version });
      await this.load();
      this.done(action === 'publish' ? 'El aviso se publicó.' : 'El aviso se archivó.');
    } catch (error) {
      void this.alerts.error('No se pudo completar la acción', this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  private done(message: string): void {
    this.alerts.success(message);
  }
}
