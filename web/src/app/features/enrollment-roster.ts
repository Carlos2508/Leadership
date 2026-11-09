import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { distinctUntilChanged, map } from 'rxjs';
import { AccessApi, PageResult } from '../core/access-api';
import { Alerts } from '../core/alerts';
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
import { CyclePipe } from '../shared/cycle.pipe';

type Mode = 'enroll' | 'placement' | 'withdraw' | 'reactivate';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, CyclePipe],
  templateUrl: './enrollment-roster.html',
})
export class EnrollmentRoster {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private request = 0;
  private contextYear = '';
  @ViewChild('enrollmentDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('cardsDialog') private cardsDialog!: ElementRef<HTMLDialogElement>;
  cardGrade = '';
  cardSection = '';
  cardLayout = 'sheet';
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
    if (this.loading() || this.error()) return false;
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
    this.route.queryParamMap
      .pipe(
        map((params) =>
          JSON.stringify(
            ['year', 'grade', 'section', 'status', 'search', 'page'].map((key) => params.get(key)),
          ),
        ),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe(() => void this.init());
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
    const request = ++this.request;
    const params = this.route.snapshot.queryParamMap;
    const requested = params.get('year');
    const gradeId = params.get('grade') ?? '';
    const sectionId = params.get('section') ?? '';
    const status = params.get('status') ?? 'Active';
    const search = params.get('search') ?? '';
    const page = Math.max(1, Number(params.get('page')) || 1);
    this.loading.set(true);
    this.error.set('');
    this.data.set(null);
    this.gradeId = gradeId;
    this.sectionId = sectionId;
    this.status = status;
    this.search = search;
    if (this.contextYear !== requested) {
      this.detail.set(null);
      this.dialog?.nativeElement.close();
      this.cardsDialog?.nativeElement.close();
      this.selected.set(null);
      this.candidates.set([]);
    }
    try {
      if (!this.years().length) {
        const [years, grades] = await Promise.all([
          this.api.get<YearSummary[]>('/api/v1/academic/years'),
          this.api.get<GradeView[]>('/api/v1/academic/grades'),
        ]);
        if (request !== this.request) return;
        this.years.set(years);
        this.grades.set(grades);
      }
      const selected = requested
        ? this.years().find((x) => x.id === requested)
        : (this.years().find((x) => x.isDefault) ?? this.years()[0]);
      this.yearId = selected?.id ?? requested ?? '';
      if (!selected) {
        if (requested) this.error.set('No encontramos el ciclo solicitado. Selecciona otro ciclo.');
        return;
      }
      if (!requested) {
        await this.router.navigate([], { queryParams: { year: selected.id }, replaceUrl: true });
        return;
      }
      if (this.detail()?.year.id !== selected.id) {
        const detail = await this.api.get<YearDetail>(`/api/v1/academic/years/${selected.id}`);
        if (request !== this.request) return;
        this.detail.set(detail);
      }
      this.contextYear = selected.id;
      const query = new URLSearchParams({ status, search, page: String(page) });
      if (gradeId) query.set('gradeId', gradeId);
      if (sectionId) query.set('sectionId', sectionId);
      const data = await this.api.get<PageResult<EnrollmentRow>>(
        `/api/v1/years/${selected.id}/enrollments?${query}`,
      );
      if (request === this.request) this.data.set(data);
    } catch (error) {
      if (request === this.request) this.error.set(this.api.message(error));
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }

  async changeYear(): Promise<void> {
    await this.router.navigate([], { queryParams: { year: this.yearId } });
  }

  openCards(): void {
    this.cardGrade = this.gradeId || this.grades()[0]?.id || '';
    this.cardSection = this.sectionId;
    this.cardLayout = 'sheet';
    this.cardsDialog.nativeElement.showModal();
  }

  cardUrl(): string | null {
    if (!this.cardGrade || !this.detail() || this.loading() || this.error()) return null;
    const query = new URLSearchParams({ gradeId: this.cardGrade, layout: this.cardLayout });
    if (this.cardSection) query.set('sectionId', this.cardSection);
    return `/api/v1/years/${this.detail()!.year.id}/cards.pdf?${query}`;
  }

  async load(page = 1): Promise<void> {
    if (!this.yearId) return;
    const navigated = await this.router.navigate([], {
      queryParams: {
        year: this.yearId,
        grade: this.gradeId || null,
        section: this.sectionId || null,
        status: this.status === 'Active' ? null : this.status,
        search: this.search.trim() || null,
        page: page > 1 ? page : null,
      },
    });
    if (!navigated) await this.init();
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
      year && today < year.startsOn
        ? year.startsOn
        : year && today > year.endsOn
          ? year.endsOn
          : today;
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
      this.alerts.success(message);
      this.detail.set(null);
      await this.init();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
