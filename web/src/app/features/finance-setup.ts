import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { distinctUntilChanged, map } from 'rxjs';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
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
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private loadRequest = 0;
  private contextYear = '';
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
    this.route.queryParamMap.pipe(
      map(params => params.get('year')),
      distinctUntilChanged(),
      takeUntilDestroyed(),
    ).subscribe(() => void this.load());
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

  async load(): Promise<void> {
    const request = ++this.loadRequest;
    const requested = this.route.snapshot.queryParamMap.get('year');
    this.loading.set(true);
    this.error.set('');
    this.setup.set(null);
    this.preview.set(null);
    this.dialog?.nativeElement.close();
    try {
      if (!this.years().length) {
        const [years, grades] = await Promise.all([
          this.api.get<YearSummary[]>('/api/v1/academic/years'),
          this.api.get<GradeView[]>('/api/v1/academic/grades'),
        ]);
        if (request !== this.loadRequest) return;
        this.years.set(years);
        this.grades.set(grades.filter(x => x.isActive));
      }
      const selected = requested ? this.years().find(x => x.id === requested)
        : this.years().find(x => x.isDefault) ?? this.years()[0];
      this.yearId = selected?.id ?? requested ?? '';
      if (!selected) {
        if (requested) this.error.set('No encontramos el ciclo solicitado. Selecciona otro ciclo.');
        return;
      }
      if (!requested) {
        await this.router.navigate([], { queryParams: { year: selected.id }, replaceUrl: true });
        return;
      }
      if (this.contextYear !== selected.id) {
        this.contextYear = selected.id;
        this.generation = { conceptId: '', billingPeriodId: '', amount: null, dueOn: '', description: '', grades: {}, excluded: {} };
        this.generationError.set('');
      }
      const setup = await this.api.get<Setup>(`/api/v1/finance/years/${selected.id}/setup`);
      if (request === this.loadRequest) this.setup.set(setup);
    } catch (error) {
      if (request === this.loadRequest) this.error.set(this.api.message(error));
    } finally {
      if (request === this.loadRequest) this.loading.set(false);
    }
  }

  async changeYear(): Promise<void> {
    await this.router.navigate([], { queryParams: { year: this.yearId } });
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
      this.alerts.success('El cambio se guardó.');
      await this.load();
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
    if (this.busy() || !this.setup() || this.loading()) return;
    const request = this.loadRequest;
    this.generationError.set('');
    if (!this.generation.conceptId || !this.generation.amount) {
      this.generationError.set('Selecciona el concepto e indica el importe.');
      return;
    }
    this.busy.set(true);
    try {
      const preview = await this.api.post<GenerationPreview>('/api/v1/finance/charges/preview', this.generationBody());
      if (request === this.loadRequest) this.preview.set(preview);
    } catch (error) {
      if (request === this.loadRequest) {
        this.preview.set(null);
        this.generationError.set(this.api.message(error));
      }
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
      this.alerts.success(
        `Cargos emitidos: ${result.created}. Ya existían: ${result.existing}. Excluidos: ${result.excluded}.`,
      );
      await this.load();
    } catch (error) {
      this.generationError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
