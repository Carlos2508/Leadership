import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { formatDateOnly } from '../core/people-api';
import {
  GradeView,
  PeriodView,
  SectionView,
  YearDetail,
  YearSummary,
  nextYearAction,
  nextYearStatus,
  yearStatusLabels,
} from '../core/academic-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';

type Mode = 'year' | 'status' | 'period' | 'grade' | 'section';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, CyclePipe],
  templateUrl: './academic-structure.html',
})
export class AcademicStructure {
  readonly year = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('academicDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;

  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly detail = signal<YearDetail | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly mode = signal<Mode>('year');
  readonly editingId = signal<string | null>(null);
  readonly canManage = computed(() => this.session.can('academic.manage'));
  readonly statusLabels = yearStatusLabels;
  readonly nextAction = nextYearAction;
  readonly sectionsByGrade = computed(() => {
    const sections = this.detail()?.sections ?? [];
    return this.grades()
      .filter((g) => g.isActive || sections.some((s) => s.gradeLevelId === g.id))
      .map((grade) => ({ grade, sections: sections.filter((s) => s.gradeLevelId === grade.id) }));
  });

  yearForm = { label: '', startsOn: '', endsOn: '' };
  periodForm = { name: '', startsOn: '', endsOn: '' };
  gradeForm = { code: '', name: '', stage: '', sequence: 1, isActive: true, version: '' };
  sectionForm = { gradeLevelId: '', name: '', shift: '', capacity: null as number | null, version: '' };
  reason = '';

  constructor() {
    queueMicrotask(() => void this.load());
  }

  date(value: string | null): string {
    return formatDateOnly(value, this.school.locale.culture);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [years, grades] = await Promise.all([
        this.api.get<YearSummary[]>('/api/v1/academic/years'),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
      ]);
      this.years.set(years);
      this.grades.set(grades);
      const selected =
        years.find((x) => x.id === this.year()) ?? years.find((x) => x.isDefault) ?? years[0];
      this.detail.set(
        selected
          ? await this.api.get<YearDetail>(`/api/v1/academic/years/${selected.id}`)
          : null,
      );
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }

  async select(year: YearSummary): Promise<void> {
    await this.router.navigate([], { queryParams: { year: year.id } });
    this.detail.set(await this.api.get<YearDetail>(`/api/v1/academic/years/${year.id}`));
  }

  private open(mode: Mode, id: string | null = null): void {
    this.mode.set(mode);
    this.editingId.set(id);
    this.dialogError.set('');
    this.reason = '';
    this.dialog.nativeElement.showModal();
  }

  openYear(edit = false): void {
    const y = this.detail()?.year;
    this.yearForm =
      edit && y
        ? { label: y.label, startsOn: y.startsOn, endsOn: y.endsOn }
        : { label: '', startsOn: '', endsOn: '' };
    this.open('year', edit && y ? y.id : null);
  }
  openStatus(): void {
    this.open('status');
  }
  openPeriod(period?: PeriodView): void {
    this.periodForm = period
      ? { name: period.name, startsOn: period.startsOn, endsOn: period.endsOn }
      : { name: '', startsOn: '', endsOn: '' };
    this.open('period', period?.id ?? null);
  }
  openGrade(grade?: GradeView): void {
    this.gradeForm = grade
      ? { ...grade }
      : {
          code: '',
          name: '',
          stage: '',
          sequence: (this.grades().at(-1)?.sequence ?? 0) + 1,
          isActive: true,
          version: '',
        };
    this.open('grade', grade?.id ?? null);
  }
  openSection(gradeId: string, section?: SectionView): void {
    this.sectionForm = section
      ? {
          gradeLevelId: section.gradeLevelId,
          name: section.name,
          shift: section.shift ?? '',
          capacity: section.capacity,
          version: section.version,
        }
      : { gradeLevelId: gradeId, name: '', shift: '', capacity: null, version: '' };
    this.open('section', section?.id ?? null);
  }

  closeDialog(): void {
    if (!this.busy()) this.dialog.nativeElement.close();
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeDialog();
  }

  gradeName(id: string): string {
    return this.grades().find((x) => x.id === id)?.name ?? '';
  }

  title(): string {
    const editing = !!this.editingId();
    return {
      year: editing ? 'Editar ciclo' : 'Nuevo ciclo escolar',
      status: this.nextAction[this.detail()?.year.status ?? 'Closed'],
      period: editing ? 'Editar período' : 'Agregar período',
      grade: editing ? 'Editar grado' : 'Nuevo grado',
      section: editing ? 'Editar sección' : 'Nueva sección',
    }[this.mode()];
  }

  private missingDates(): boolean {
    const form = this.mode() === 'year' ? this.yearForm : this.mode() === 'period' ? this.periodForm : null;
    return !!form && (!form.startsOn || !form.endsOn);
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    if (this.missingDates()) {
      this.dialogError.set('Completa las fechas de inicio y fin.');
      return;
    }
    this.busy.set(true);
    this.dialogError.set('');
    const year = this.detail()?.year;
    const id = this.editingId();
    try {
      let message = 'El cambio se guardó.';
      let goTo: string | null = null;
      switch (this.mode()) {
        case 'year':
          if (id && year) {
            await this.api.post(`/api/v1/academic/years/${id}`, {
              ...this.yearForm,
              version: year.version,
            });
          } else {
            goTo = (await this.api.post<{ id: string }>('/api/v1/academic/years', this.yearForm)).id;
            message = 'El ciclo se creó en borrador. Agrega sus períodos y secciones.';
          }
          break;
        case 'status':
          if (!year) return;
          await this.api.post(`/api/v1/academic/years/${year.id}/status`, {
            version: year.version,
            status: nextYearStatus[year.status],
            reason: this.reason,
          });
          message = 'El estado del ciclo cambió.';
          break;
        case 'period':
          if (!year) return;
          await this.api.post(
            id ? `/api/v1/academic/periods/${id}` : `/api/v1/academic/years/${year.id}/periods`,
            {
              name: this.periodForm.name || null,
              startsOn: this.periodForm.startsOn,
              endsOn: this.periodForm.endsOn,
            },
          );
          message = 'El período se guardó.';
          break;
        case 'grade':
          await this.api.post(
            id ? `/api/v1/academic/grades/${id}` : '/api/v1/academic/grades',
            this.gradeForm,
          );
          message = 'El grado se guardó.';
          break;
        case 'section':
          if (!year) return;
          await this.api.post(
            id
              ? `/api/v1/academic/years/${year.id}/sections/${id}`
              : `/api/v1/academic/years/${year.id}/sections`,
            {
              ...this.sectionForm,
              shift: this.sectionForm.shift || null,
              capacity: this.sectionForm.capacity || null,
            },
          );
          message = 'La sección se guardó.';
          break;
      }
      this.busy.set(false);
      this.dialog.nativeElement.close();
      this.alerts.success(message);
      if (goTo) await this.router.navigate([], { queryParams: { year: goTo } });
      await this.load();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }

  async quick(action: 'default' | 'remove-period' | 'remove-section', section?: SectionView) {
    const year = this.detail()?.year;
    if (!year || this.busy()) return;
    const ok = await this.alerts.confirm(
      action === 'default'
        ? { title: `¿Usar ${year.label} como ciclo predeterminado?`, text: 'Las pantallas abrirán este ciclo primero.', confirmText: 'Sí, usarlo' }
        : action === 'remove-period'
          ? { title: 'Quitar el último período', text: 'Solo es posible mientras el ciclo esté en borrador.', confirmText: 'Quitar período', danger: true }
          : { title: `Quitar la sección ${section?.name ?? ''}`, text: 'Solo se puede quitar si no tiene estudiantes ni cursos.', confirmText: 'Quitar sección', danger: true },
    );
    if (!ok) return;
    this.busy.set(true);
    this.error.set('');
    try {
      if (action === 'default')
        await this.api.post(`/api/v1/academic/years/${year.id}/default`, {
          version: year.version,
        });
      else if (action === 'remove-period')
        await this.api.post(`/api/v1/academic/years/${year.id}/periods/remove-last`, {
          version: year.version,
        });
      else if (section)
        await this.api.post(`/api/v1/academic/years/${year.id}/sections/${section.id}/remove`, {
          version: section.version,
        });
      this.alerts.success('El cambio se guardó.');
      await this.load();
    } catch (error) {
      void this.alerts.error('No se pudo completar la acción', this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
