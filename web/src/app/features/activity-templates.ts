import { Component, ElementRef, ViewChild, ViewEncapsulation, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';
import { points } from '../core/student-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

interface Template {
  id: string;
  name: string;
  items: { title: string; points: number }[];
  total: number;
  isActive: boolean;
  version: string;
}

/**
 * Institutional activity templates (for example the eight activities each course grades per period). Teachers apply them in
 * the gradebook with one click and can adjust them; academic management maintains them here, with a reason.
 */
@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../styles/_activity-templates.scss'],
  template: `
    <app-shell title="Plantillas de actividades">
      <a class="back-link" routerLink="/workspace/grades"><app-icon name="arrow" />Volver a calificaciones</a>
      <div class="page-heading">
        <div>
          <span class="eyebrow">EVALUACIÓN</span>
          <h1>Plantillas de actividades</h1>
          <p>Las actividades que cada curso califica por período, con su punteo. Los docentes las aplican en la libreta con un clic.</p>
        </div>
        @if (canManage()) {
          <button class="button" (click)="open()"><app-icon name="plus" />Nueva plantilla</button>
        }
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      <div class="tpl-grid">
        @for (t of templates(); track t.id) {
          <article class="card tpl-card" [class.inactive]="!t.isActive">
            <header>
              <h2>{{ t.name }}</h2>
              <span class="pill" [class.mint]="t.isActive">{{ t.isActive ? 'Activa' : 'Inactiva' }}</span>
            </header>
            <ul class="gb-template-items">
              @for (item of t.items; track $index) {
                <li><span>{{ item.title }}</span><strong>{{ fmt(item.points) }} pts</strong></li>
              }
            </ul>
            <footer>
              <strong>Total: {{ fmt(t.total) }} pts</strong>
              @if (canManage()) {
                <button class="text-button" (click)="open(t)">Editar <span class="sr-only">{{ t.name }}</span></button>
              }
            </footer>
          </article>
        } @empty {
          <section class="card empty-state">
            <app-icon name="folder" />
            <h2>Sin plantillas</h2>
            <p>Crea la lista de actividades del colegio (por ejemplo, tareas, laboratorios y examen) para que los docentes la usen.</p>
          </section>
        }
      </div>

      <dialog #dialog class="account-dialog wide-dialog" aria-labelledby="tpl-title" (cancel)="onCancel($event)">
        <div class="dialog-heading">
          <h2 id="tpl-title">{{ editing() ? 'Editar plantilla' : 'Nueva plantilla' }}</h2>
          <button class="icon-button" (click)="close()" aria-label="Cerrar ventana" [disabled]="busy()"><app-icon name="close" /></button>
        </div>
        <form class="access-form" (ngSubmit)="save()">
          <label class="form-field"><span>Nombre</span><input name="name" [(ngModel)]="form.name" required maxlength="80" autocomplete="off" placeholder="Ej.: Actividades por bimestre" /></label>
          <fieldset class="choice-group">
            <legend>Actividades</legend>
            @for (item of form.items; track $index; let i = $index) {
              <div class="tpl-row">
                <label class="form-field"><span class="sr-only">Actividad {{ i + 1 }}</span><input [name]="'title-' + i" [(ngModel)]="item.title" required maxlength="120" [placeholder]="'Actividad ' + (i + 1)" /></label>
                <label class="form-field tpl-points"><span class="sr-only">Puntos de la actividad {{ i + 1 }}</span><input [name]="'points-' + i" type="number" min="0.01" max="100" step="0.01" [(ngModel)]="item.points" required /></label>
                <button type="button" class="icon-button" (click)="removeItem(i)" [attr.aria-label]="'Quitar actividad ' + (i + 1)" [disabled]="form.items.length === 1"><app-icon name="close" /></button>
              </div>
            }
            <div class="tpl-foot">
              <button type="button" class="text-button" (click)="addItem()" [disabled]="form.items.length >= 20"><app-icon name="plus" />Agregar actividad</button>
              <strong [class.over]="formTotal() > 100">Total: {{ fmt(formTotal()) }} de 100 pts</strong>
            </div>
          </fieldset>
          <label class="checkbox-field"><input type="checkbox" class="switch" role="switch" name="active" [(ngModel)]="form.isActive" /><span>Disponible para los docentes</span></label>
          <label class="form-field"><span>Motivo del cambio</span><input name="reason" [(ngModel)]="form.reason" required minlength="8" maxlength="500" autocomplete="off" /></label>
          @if (dialogError()) {
            <p class="feedback error-feedback" role="alert">{{ dialogError() }}</p>
          }
          <div class="dialog-actions">
            <button class="button outline" type="button" (click)="close()" [disabled]="busy()">Cancelar</button>
            <button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Guardando…' : 'Guardar plantilla' }}</button>
          </div>
        </form>
      </dialog>
    </app-shell>
  `,
})
export class ActivityTemplates {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  @ViewChild('dialog') private dialog?: ElementRef<HTMLDialogElement>;
  readonly templates = signal<Template[]>([]);
  readonly editing = signal<Template | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly dialogError = signal('');
  readonly items = signal(0);
  readonly canManage = computed(() => this.session.can('academic.manage'));
  form = { name: '', items: [{ title: '', points: 10 as number | null }], isActive: true, reason: '' };

  constructor() {
    void this.load();
  }
  fmt(value: number | null): string {
    return points(value);
  }
  formTotal(): number {
    return this.form.items.reduce((sum, item) => sum + (Number(item.points) || 0), 0);
  }
  async load(): Promise<void> {
    try {
      this.templates.set(await this.api.get<Template[]>('/api/v1/activity-templates'));
    } catch (error) {
      this.error.set(this.api.message(error));
    }
  }
  open(template?: Template): void {
    this.editing.set(template ?? null);
    this.form = template
      ? { name: template.name, items: template.items.map((x) => ({ ...x })), isActive: template.isActive, reason: '' }
      : { name: '', items: [{ title: '', points: 10 }], isActive: true, reason: '' };
    this.dialogError.set('');
    this.dialog?.nativeElement.showModal();
  }
  addItem(): void {
    this.form.items.push({ title: '', points: 10 });
  }
  removeItem(index: number): void {
    this.form.items.splice(index, 1);
  }
  close(): void {
    if (!this.busy()) this.dialog?.nativeElement.close();
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
  }
  async save(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.dialogError.set('');
    try {
      const template = this.editing();
      await this.api.post(template ? `/api/v1/activity-templates/${template.id}` : '/api/v1/activity-templates', {
        name: this.form.name,
        items: this.form.items.map((x) => ({ title: x.title, points: Number(x.points) })),
        isActive: this.form.isActive,
        version: template?.version ?? null,
        reason: this.form.reason,
      });
      this.busy.set(false);
      this.dialog?.nativeElement.close();
      this.alerts.success('La plantilla se guardó.');
      await this.load();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
