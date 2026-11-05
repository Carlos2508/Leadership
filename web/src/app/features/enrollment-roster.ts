import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi, PageResult } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { PersonSummary, formatDateOnly } from '../core/people-api';
import {
  EnrollmentRow,
  GradeView,
  YearDetail,
  YearSummary,
  enrollmentStatusLabels,
  todayIn,
  yearStatusLabels,
} from '../core/academic-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

type Mode = 'enroll' | 'placement' | 'withdraw' | 'reactivate';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  templateUrl: './enrollment-roster.html',
})
export class EnrollmentRoster {
  readonly year = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('enrollmentDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;

  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly detail = signal<YearDetail | null>(null);
  readonly data = signal<PageResult<EnrollmentRow> | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly mode = signal<Mode>('enroll');
  readonly selected = signal<EnrollmentRow | null>(null);
  readonly candidates = signal<PersonSummary[]>([]);
  readonly searching = signal(false);
  readonly canEnroll = computed(() => this.session.can('enrollment.manage'));
  readonly open = computed(() => {
    const status = this.detail()?.year.status;
    return status === 'EnrollmentOpen' || status === 'Active';
  });
  readonly statusLabels = enrollmentStatusLabels;
  readonly yearLabels = yearStatusLabels;

  yearId = '';
  gradeId = '';
  sectionId = '';
  status = 'Active';
  search = '';
  form = { studentId: '', gradeId: '', sectionId: '', date: '', reason: '' };
  candidateSearch = '';

  constructor() {
    queueMicrotask(() => void this.init());
  }

  date(value: string | null): string {
    return formatDateOnly(value, this.school.locale.culture);
  }

  activeGrades(): GradeView[] {
    return this.grades().filter((x) => x.isActive);
  }
  sectionsFor(gradeId: string) {
    return (this.detail()?.sections ?? []).filter((x) => x.gradeLevelId === gradeId);
  }

  async init(): Promise<void> {
    this.loading.set(true);
    try {
      const [years, grades] = await Promise.all([
        this.api.get<YearSummary[]>('/api/v1/academic/years'),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
      ]);
      this.years.set(years);
      this.grades.set(grades);
      const selected =
        years.find((x) => x.id === this.year()) ?? years.find((x) => x.isDefault) ?? years[0];
      if (!selected) {
        this.loading.set(false);
        return;
      }
      this.yearId = selected.id;
      await this.loadYear();
    } catch (error) {
      this.error.set(this.api.message(error));
      this.loading.set(false);
    }
  }

  async changeYear(): Promise<void> {
    this.gradeId = '';
    this.sectionId = '';
    await this.router.navigate([], { queryParams: { year: this.yearId } });
    await this.loadYear();
  }

  async loadYear(): Promise<void> {
    this.detail.set(await this.api.get<YearDetail>(`/api/v1/academic/years/${this.yearId}`));
    await this.load();
  }

  async load(page = 1): Promise<void> {
    if (!this.yearId) return;
    this.loading.set(true);
    this.error.set('');
    try {
      const query = new URLSearchParams({ status: this.status, search: this.search, page: String(page) });
      if (this.gradeId) query.set('gradeId', this.gradeId);
      if (this.sectionId) query.set('sectionId', this.sectionId);
      this.data.set(
        await this.api.get<PageResult<EnrollmentRow>>(
          `/api/v1/years/${this.yearId}/enrollments?${query}`,
        ),
      );
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }

  clearFilters(): void {
    this.gradeId = '';
    this.sectionId = '';
    this.status = 'Active';
    this.search = '';
    void this.load();
  }

  private openDialog(mode: Mode, row: EnrollmentRow | null): void {
    const today = todayIn(this.school.locale.timeZone);
    const year = this.detail()?.year;
    const clamped =
      year && today < year.startsOn ? year.startsOn : year && today > year.endsOn ? year.endsOn : today;
    this.mode.set(mode);
    this.selected.set(row);
    this.dialogError.set('');
    this.form = {
      studentId: '',
      gradeId: row?.gradeLevelId ?? '',
      sectionId: mode === 'placement' ? '' : (row?.sectionId ?? ''),
      date: clamped,
      reason: '',
    };
    this.candidateSearch = '';
    this.candidates.set([]);
    this.dialog.nativeElement.showModal();
  }
  openEnroll(): void {
    this.openDialog('enroll', null);
  }
  openPlacement(row: EnrollmentRow): void {
    this.openDialog('placement', row);
  }
  openWithdraw(row: EnrollmentRow): void {
    this.openDialog('withdraw', row);
  }
  openReactivate(row: EnrollmentRow): void {
    this.openDialog('reactivate', row);
  }
  closeDialog(): void {
    if (!this.busy()) this.dialog.nativeElement.close();
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeDialog();
  }

  title(): string {
    return {
      enroll: 'Inscribir estudiante',
      placement: 'Cambiar grado o sección',
      withdraw: 'Registrar retiro',
      reactivate: 'Registrar reingreso',
    }[this.mode()];
  }

  async searchCandidates(): Promise<void> {
    this.searching.set(true);
    try {
      const query = new URLSearchParams({
        search: this.candidateSearch,
        role: 'student',
        status: 'Active',
        page: '1',
      });
      this.candidates.set(
        (await this.api.get<PageResult<PersonSummary>>(`/api/v1/people?${query}`)).items,
      );
      if (!this.candidates().some((x) => x.id === this.form.studentId)) this.form.studentId = '';
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.searching.set(false);
    }
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    this.dialogError.set('');
    const row = this.selected();
    const f = this.form;
    if (this.mode() === 'enroll' && !f.studentId) {
      this.dialogError.set('Busca y selecciona al estudiante.');
      return;
    }
    if (!f.date) {
      this.dialogError.set('Indica la fecha efectiva.');
      return;
    }
    if (this.mode() !== 'withdraw' && !f.gradeId) {
      this.dialogError.set('Selecciona el grado.');
      return;
    }
    this.busy.set(true);
    try {
      let message = '';
      const section = f.sectionId || null;
      switch (this.mode()) {
        case 'enroll':
          await this.api.post('/api/v1/enrollments', {
            studentId: f.studentId,
            academicYearId: this.yearId,
            gradeLevelId: f.gradeId,
            sectionId: section,
            enrolledOn: f.date || null,
            reason: f.reason || null,
          });
          message = 'La inscripción quedó registrada.';
          break;
        case 'placement':
          await this.api.post(`/api/v1/enrollments/${row!.id}/placement`, {
            version: row!.version,
            gradeLevelId: f.gradeId,
            sectionId: section,
            effectiveOn: f.date,
            reason: f.reason,
          });
          message = 'La nueva ubicación quedó registrada; la anterior se conserva en el historial.';
          break;
        case 'withdraw':
          await this.api.post(`/api/v1/enrollments/${row!.id}/withdraw`, {
            version: row!.version,
            effectiveOn: f.date,
            reason: f.reason,
          });
          message = 'El retiro quedó registrado.';
          break;
        case 'reactivate':
          await this.api.post(`/api/v1/enrollments/${row!.id}/reactivate`, {
            version: row!.version,
            gradeLevelId: f.gradeId,
            sectionId: section,
            effectiveOn: f.date,
            reason: f.reason,
          });
          message = 'El reingreso quedó registrado.';
          break;
      }
      this.busy.set(false);
      this.dialog.nativeElement.close();
      this.notice.set(message);
      await this.loadYear();
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
