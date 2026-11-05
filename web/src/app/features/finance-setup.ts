import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import { GradeView, YearSummary, yearStatusLabels } from '../core/academic-api';
import {
  BillingPeriodView,
  ConceptView,
  FinanceSetup as Setup,
  GenerationPreview,
  money,
} from '../core/finance-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { PluralPipe } from '../shared/plural.pipe';

type Mode = 'concept' | 'period' | 'cutoff';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, PluralPipe],
  templateUrl: './finance-setup.html',
})
export class FinanceSetup {
  readonly year = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('setupDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;

  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly setup = signal<Setup | null>(null);
  readonly preview = signal<GenerationPreview | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly generationError = signal('');
  readonly mode = signal<Mode>('concept');
  readonly editing = signal<string | null>(null);
  readonly canEdit = computed(() => this.session.can('finance.charges'));
  readonly yearLabels = yearStatusLabels;

  yearId = '';
  conceptForm = { code: '', name: '', isRecurring: true, countsForClearance: true, isActive: true, version: '' };
  periodForm = { label: '', dueOn: '' };
  cutoffForm = { name: '', throughSequence: 1 };
  generation = {
    conceptId: '',
    billingPeriodId: '',
    amount: null as number | null,
    dueOn: '',
    description: '',
    grades: {} as Record<string, boolean>,
    excluded: {} as Record<string, boolean>,
  };

  constructor() {
    queueMicrotask(() => void this.init());
  }

  money(value: number): string {
    return money(value, this.setup()?.currency ?? this.school.locale.currency, this.school.locale.culture);
  }
  date(value: string | null): string {
    return formatDateOnly(value, this.school.locale.culture);
  }
  selectedConcept(): ConceptView | undefined {
    return this.setup()?.concepts.find((x) => x.id === this.generation.conceptId);
  }

  async init(): Promise<void> {
    this.loading.set(true);
    try {
      const [years, grades] = await Promise.all([
        this.api.get<YearSummary[]>('/api/v1/academic/years'),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
      ]);
      this.years.set(years);
      this.grades.set(grades.filter((x) => x.isActive));
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
    this.preview.set(null);
    await this.router.navigate([], { queryParams: { year: this.yearId } });
    await this.load();
  }

  async load(): Promise<void> {
    this.setup.set(await this.api.get<Setup>(`/api/v1/finance/years/${this.yearId}/setup`));
  }

  openConcept(concept?: ConceptView): void {
    this.conceptForm = concept
      ? { ...concept }
      : { code: '', name: '', isRecurring: true, countsForClearance: true, isActive: true, version: '' };
    this.open('concept', concept?.id ?? null);
  }
  openPeriod(period?: BillingPeriodView): void {
    this.periodForm = period ? { label: period.label, dueOn: period.dueOn } : { label: '', dueOn: '' };
    this.open('period', period?.id ?? null);
  }
  openCutoff(): void {
    this.cutoffForm = { name: '', throughSequence: this.setup()?.periods.at(-1)?.sequence ?? 1 };
    this.open('cutoff', null);
  }
  private open(mode: Mode, id: string | null): void {
    this.mode.set(mode);
    this.editing.set(id);
    this.dialogError.set('');
    this.dialog.nativeElement.showModal();
  }
  closeDialog(): void {
    if (!this.busy()) this.dialog.nativeElement.close();
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeDialog();
  }
  title(): string {
    const editing = !!this.editing();
    return {
      concept: editing ? 'Editar concepto' : 'Nuevo concepto de cobro',
      period: editing ? 'Editar cuota' : 'Agregar cuota al calendario',
      cutoff: 'Nuevo corte de solvencia',
    }[this.mode()];
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    if (this.mode() === 'period' && !this.periodForm.dueOn) {
      this.dialogError.set('Indica la fecha de vencimiento.');
      return;
    }
    this.busy.set(true);
    this.dialogError.set('');
    const id = this.editing();
    try {
      if (this.mode() === 'concept')
        await this.api.post(id ? `/api/v1/finance/concepts/${id}` : '/api/v1/finance/concepts', this.conceptForm);
      else if (this.mode() === 'period')
        await this.api.post(
          id ? `/api/v1/finance/periods/${id}` : `/api/v1/finance/years/${this.yearId}/periods`,
          this.periodForm,
        );
      else await this.api.post(`/api/v1/finance/years/${this.yearId}/cutoffs`, this.cutoffForm);
      this.busy.set(false);
      this.dialog.nativeElement.close();
      this.notice.set('El cambio se guardó.');
      await this.load();
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }

  private generationBody(previewHash?: string) {
    const g = this.generation;
    return {
      academicYearId: this.yearId,
      conceptId: g.conceptId,
      billingPeriodId: this.selectedConcept()?.isRecurring ? g.billingPeriodId || null : null,
      occurrence: 1,
      amount: g.amount,
      dueOn: g.dueOn || null,
      description: g.description || null,
      gradeIds: Object.keys(g.grades).filter((x) => g.grades[x]),
      sectionIds: [],
      excludedEnrollmentIds: Object.keys(g.excluded).filter((x) => g.excluded[x]),
      previewHash: previewHash ?? null,
    };
  }

  async runPreview(): Promise<void> {
    if (this.busy()) return;
    this.generationError.set('');
    if (!this.generation.conceptId || !this.generation.amount) {
      this.generationError.set('Selecciona el concepto e indica el importe.');
      return;
    }
    this.busy.set(true);
    try {
      this.preview.set(await this.api.post<GenerationPreview>('/api/v1/finance/charges/preview', this.generationBody()));
    } catch (error) {
      this.preview.set(null);
      this.generationError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }

  async toggleExcluded(enrollmentId: string, excluded: boolean): Promise<void> {
    this.generation.excluded[enrollmentId] = excluded;
    await this.runPreview();
  }

  async confirm(): Promise<void> {
    const preview = this.preview();
    if (!preview || this.busy()) return;
    this.busy.set(true);
    this.generationError.set('');
    try {
      const result = await this.api.post<{ created: number; existing: number; excluded: number }>(
        '/api/v1/finance/charges/generate',
        this.generationBody(preview.previewHash),
      );
      this.preview.set(null);
      this.generation.excluded = {};
      this.notice.set(
        `Cargos emitidos: ${result.created}. Ya existían: ${result.existing}. Excluidos: ${result.excluded}.`,
      );
      await this.load();
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.generationError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
