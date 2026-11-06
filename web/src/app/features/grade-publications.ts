import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { GradeView, SectionView, YearDetail, YearSummary, yearStatusLabels } from '../core/academic-api';
import { FinanceSetup } from '../core/finance-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { PluralPipe } from '../shared/plural.pipe';

interface PublicationView {
  periodId: string;
  sequence: number;
  name: string;
  startsOn: string;
  endsOn: string;
  published: boolean;
  publishedAt: string | null;
  cutoff: string | null;
  expected: number;
  recorded: number;
}

/** Publication of period results, each tied to the clearance cutoff the school chooses for that release. */
@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, PluralPipe],
  template: `
    <app-shell title="Publicación de notas">
      <div class="page-heading">
        <div>
          <span class="eyebrow">EVALUACIÓN</span>
          <h1>Publicación de notas</h1>
          <p>
            Publica cada período cuando todas las notas estén registradas. Familias y estudiantes las ven solo con
            la facultad académica y la solvencia del corte elegido.
          </p>
        </div>
      </div>
      @if (notice()) {
        <p class="feedback success-feedback" role="status" tabindex="-1" #noticeRef>{{ notice() }}</p>
      }
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      <section class="card directory-card">
        <div class="filters">
          <label class="select-field"
            ><span>Ciclo</span
            ><select name="year" [(ngModel)]="yearId" (ngModelChange)="changeYear()">
              @for (item of years(); track item.id) {
                <option [value]="item.id">{{ item.label }} · {{ yearLabels[item.status] }}</option>
              }
            </select></label
          >
        </div>
        @if (items().length) {
          <ul class="publication-list">
            @for (item of items(); track item.periodId) {
              <li>
                <span class="period-number">{{ item.sequence }}</span>
                <span class="person-copy"
                  ><strong>{{ item.name }}</strong
                  ><small>{{ date(item.startsOn) }} – {{ date(item.endsOn) }}@if (item.published) { · Corte: {{ item.cutoff }}}</small></span
                >
                <span class="progress-copy">
                  <span class="progress-bar" aria-hidden="true"
                    ><span [style.width.%]="item.expected ? (item.recorded / item.expected) * 100 : 0"></span
                  ></span>
                  <small>{{ item.recorded }} de {{ item.expected | plural: 'nota' : 'notas' }}</small>
                </span>
                @if (item.published) {
                  <span class="row-actions">
                    <span class="pill mint">Publicado</span>
                    <button class="button outline compact-button" (click)="openReports(item)">
                      Informes PDF <span class="sr-only">de {{ item.name }}</span>
                    </button>
                  </span>
                } @else if (canPublish()) {
                  <button class="button outline compact-button" (click)="openPublish(item)" [disabled]="!cutoffs().length">
                    Publicar <span class="sr-only">{{ item.name }}</span>
                  </button>
                } @else {
                  <span class="pill gold">Sin publicar</span>
                }
              </li>
            }
          </ul>
          @if (canPublish() && !cutoffs().length) {
            <p class="year-notice">
              <app-icon name="info" /><span
                >Define primero un corte de solvencia para este ciclo en
                <a routerLink="/workspace/finance/setup" [queryParams]="{ year: yearId }">Configuración financiera</a>.</span
              >
            </p>
          }
        } @else if (!loading()) {
          <p class="empty-inline padded">Este ciclo no tiene períodos.</p>
        }
      </section>

      <dialog #publishDialog class="account-dialog" aria-labelledby="publish-title" (cancel)="onCancel($event)">
        <div class="dialog-heading">
          <h2 id="publish-title">Publicar {{ selected()?.name }}</h2>
          <button class="icon-button" (click)="close()" aria-label="Cerrar ventana" [disabled]="busy()"><app-icon name="close" /></button>
        </div>
        <form class="access-form" (ngSubmit)="publish()">
          <p class="action-explanation">
            Después de publicar, los docentes ya no podrán modificar este período; las correcciones quedarán a cargo de
            coordinación, con motivo.
          </p>
          <label class="form-field"
            ><span>Corte de solvencia requerido</span
            ><select name="cutoff" [(ngModel)]="cutoffId" required>
              @for (cutoff of cutoffs(); track cutoff.id) {
                <option [value]="cutoff.id">{{ cutoff.name }} · cuotas 1 a {{ cutoff.throughSequence }}</option>
              }
            </select></label
          >
          <label class="form-field"
            ><span>Nota interna <small>(opcional)</small></span
            ><input name="note" [(ngModel)]="note" maxlength="500" autocomplete="off"
          /></label>
          @if (dialogError()) {
            <p class="feedback error-feedback" role="alert">{{ dialogError() }}</p>
          }
          <div class="dialog-actions">
            <button class="button outline" type="button" (click)="close()" [disabled]="busy()">Cancelar</button
            ><button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Publicando…' : 'Publicar' }}</button>
          </div>
        </form>
      </dialog>

      <dialog #reportsDialog class="account-dialog" aria-labelledby="reports-title" (cancel)="reportsDialog.close()">
        <div class="dialog-heading">
          <h2 id="reports-title">Informes de {{ reportPeriod()?.name }}</h2>
          <button class="icon-button" (click)="reportsDialog.close()" aria-label="Cerrar ventana"><app-icon name="close" /></button>
        </div>
        <div class="access-form">
          <p class="action-explanation">
            Un informe por estudiante con el resultado de cada materia en el período. Quien no esté solvente con el corte
            «{{ reportPeriod()?.cutoff }}» aparece con el informe retenido, sin notas.
          </p>
          <label class="form-field"
            ><span>Grado</span
            ><select name="reportGrade" [(ngModel)]="reportGrade" (ngModelChange)="reportSection = ''">
              @for (grade of grades(); track grade.id) {
                <option [value]="grade.id">{{ grade.name }}</option>
              }
            </select></label
          >
          <label class="form-field"
            ><span>Sección</span
            ><select name="reportSection" [(ngModel)]="reportSection">
              <option value="">Todo el grado</option>
              @for (section of sectionsOf(reportGrade); track section.id) {
                <option [value]="section.id">Sección {{ section.name }}</option>
              }
            </select></label
          >
          <div class="dialog-actions">
            <button class="button outline" type="button" (click)="reportsDialog.close()">Cerrar</button
            ><a class="button" [class.disabled-link]="!reportGrade" [attr.aria-disabled]="!reportGrade" [href]="reportUrl()" target="_blank" rel="noopener"
              >Abrir PDF</a
            >
          </div>
        </div>
      </dialog>
    </app-shell>
  `,
})
export class GradePublications {
  readonly year = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('publishDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;
  @ViewChild('reportsDialog') private reportsDialog!: ElementRef<HTMLDialogElement>;
  readonly grades = signal<GradeView[]>([]);
  readonly sections = signal<SectionView[]>([]);
  readonly reportPeriod = signal<PublicationView | null>(null);
  reportGrade = '';
  reportSection = '';
  readonly years = signal<YearSummary[]>([]);
  readonly items = signal<PublicationView[]>([]);
  readonly cutoffs = signal<FinanceSetup['cutoffs']>([]);
  readonly selected = signal<PublicationView | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly canPublish = computed(() => this.session.can('academic.manage'));
  readonly yearLabels = yearStatusLabels;
  yearId = '';
  cutoffId = '';
  note = '';

  constructor() {
    queueMicrotask(() => void this.init());
  }
  date(value: string): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  async init(): Promise<void> {
    try {
      const years = await this.api.get<YearSummary[]>('/api/v1/academic/years');
      this.years.set(years);
      const selected = years.find((x) => x.id === this.year()) ?? years.find((x) => x.isDefault) ?? years[0];
      if (selected) {
        this.yearId = selected.id;
        await this.load();
      }
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }
  async changeYear(): Promise<void> {
    await this.router.navigate([], { queryParams: { year: this.yearId } });
    await this.load();
  }
  async load(): Promise<void> {
    this.items.set(await this.api.get<PublicationView[]>(`/api/v1/years/${this.yearId}/publications`));
    this.cutoffs.set(await this.api.get<FinanceSetup['cutoffs']>(`/api/v1/years/${this.yearId}/clearance-cutoffs`));
  }
  sectionsOf(gradeId: string): SectionView[] {
    return this.sections().filter((x) => x.gradeLevelId === gradeId);
  }
  reportUrl(): string | null {
    const period = this.reportPeriod();
    if (!period || !this.reportGrade) return null;
    const section = this.reportSection ? `&sectionId=${this.reportSection}` : '';
    return `/api/v1/years/${this.yearId}/periods/${period.periodId}/report-cards.pdf?gradeId=${this.reportGrade}${section}`;
  }
  async openReports(item: PublicationView): Promise<void> {
    this.reportPeriod.set(item);
    try {
      if (!this.grades().length) this.grades.set((await this.api.get<GradeView[]>('/api/v1/academic/grades')).filter((x) => x.isActive));
      this.sections.set((await this.api.get<YearDetail>(`/api/v1/academic/years/${this.yearId}`)).sections);
    } catch (error) {
      this.error.set(this.api.message(error));
      return;
    }
    this.reportGrade ||= this.grades()[0]?.id ?? '';
    this.reportsDialog.nativeElement.showModal();
  }
  openPublish(item: PublicationView): void {
    this.selected.set(item);
    this.cutoffId = this.cutoffs().at(-1)?.id ?? '';
    this.note = '';
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
  async publish(): Promise<void> {
    const item = this.selected();
    if (!item || this.busy()) return;
    this.busy.set(true);
    this.dialogError.set('');
    try {
      await this.api.post(`/api/v1/years/${this.yearId}/publications`, {
        periodId: item.periodId,
        cutoffId: this.cutoffId,
        note: this.note,
      });
      this.busy.set(false);
      this.dialog.nativeElement.close();
      this.alerts.success(`${item.name} quedó publicado.`);
      await this.load();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
