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
import { CourseView } from './courses';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';

type Mode = 'year' | 'status' | 'period' | 'grade' | 'section' | 'generate' | 'copy';

export interface SetupStep {
  key: string;
  title: string;
  detail: string;
  done: boolean;
}

/** Same arithmetic as the server: period i starts at floor(i·days/count); the last one ends with the cycle. */
export function splitCalendar(startsOn: string, endsOn: string, count: number): { startsOn: string; endsOn: string }[] {
  const day = (value: string) => Date.UTC(+value.slice(0, 4), +value.slice(5, 7) - 1, +value.slice(8, 10)) / 86_400_000;
  const iso = (n: number) => new Date(n * 86_400_000).toISOString().slice(0, 10);
  const start = day(startsOn);
  const days = day(endsOn) - start + 1;
  if (!(count > 0) || days < count) return [];
  return Array.from({ length: count }, (_, i) => ({
    startsOn: iso(start + Math.floor((i * days) / count)),
    endsOn: i === count - 1 ? endsOn : iso(start + Math.floor(((i + 1) * days) / count) - 1),
  }));
}

/** Next label and dates one year later, to prefill a new cycle from the latest one. */
export function nextCycle(latest: YearSummary | undefined): { label: string; startsOn: string; endsOn: string } {
  if (!latest) return { label: '', startsOn: '', endsOn: '' };
  const shift = (value: string) => `${+value.slice(0, 4) + 1}${value.slice(4)}`.replace(/-02-29$/, '-02-28');
  // «2026» → «2027», «Ciclo 2026» → «Ciclo 2027»; other labels are left for the user to write.
  const year = /\b(19|20)\d{2}\b/;
  const label = year.test(latest.label) ? latest.label.replace(year, (y) => String(+y + 1)) : '';
  return { label, startsOn: shift(latest.startsOn), endsOn: shift(latest.endsOn) };
}

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
  readonly courses = signal<CourseView[]>([]);
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
  /** Shortcuts offered when creating a cycle after another one. */
  setupForm = { periods: 4, generate: true, sections: true, courses: true, teachers: true };
  generateCount = 4;
  copyForm = { sections: true, courses: true, teachers: true };
  periodForm = { name: '', startsOn: '', endsOn: '' };
  gradeForm = { code: '', name: '', stage: '', sequence: 1, isActive: true, version: '' };
  sectionForm = { gradeLevelId: '', name: '', shift: '', capacity: null as number | null, version: '' };
  reason = '';

  /** Cycle that precedes the selected one (or the latest one when creating a new cycle). */
  readonly previous = computed<YearSummary | undefined>(() => {
    const current = this.detail()?.year;
    return this.years()
      .filter((x) => !current || (x.id !== current.id && x.startsOn < current.startsOn))
      .sort((a, b) => b.startsOn.localeCompare(a.startsOn))[0];
  });

  readonly steps = computed<SetupStep[]>(() => {
    const d = this.detail();
    if (!d) return [];
    const term = this.school.terms.period.toLowerCase();
    const courses = this.courses();
    const withoutTeacher = courses.filter((c) => !c.teachers.length).length;
    const status = d.year.status;
    return [
      {
        key: 'periods',
        title: `${this.school.terms.period}s`,
        detail: d.periods.length
          ? `${d.periods.length} ${term}${d.periods.length === 1 ? '' : 's'} definidos`
          : 'Sin fechas todavía',
        done: d.periods.length > 0,
      },
      {
        key: 'sections',
        title: 'Secciones',
        detail: d.sections.length
          ? `${d.sections.length} sección${d.sections.length === 1 ? '' : 'es'}`
          : 'Opcionales: sin secciones se inscribe por grado',
        done: d.sections.length > 0,
      },
      {
        key: 'courses',
        title: 'Cursos y docentes',
        detail: courses.length
          ? withoutTeacher
            ? `${courses.length} cursos · ${withoutTeacher} sin docente`
            : `${courses.length} cursos con docente`
          : 'Aún no hay cursos',
        done: courses.length > 0 && withoutTeacher === 0,
      },
      {
        key: 'enrollment',
        title: 'Inscripciones',
        detail:
          status === 'Draft'
            ? 'Se habilitan al abrir inscripciones'
            : `${d.year.enrollments} ${d.year.enrollments === 1 ? 'estudiante inscrito' : 'estudiantes inscritos'}`,
        done: d.year.enrollments > 0,
      },
      {
        key: 'start',
        title: 'Inicio de clases',
        detail: status === 'Active' ? 'El ciclo está en curso' : status === 'Closed' ? 'Ciclo cerrado' : 'Inicia el ciclo cuando todo esté listo',
        done: status === 'Active' || status === 'Closed',
      },
    ];
  });
  readonly progress = computed(() => this.steps().filter((x) => x.done).length);

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
      await this.loadYear(selected?.id ?? null);
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadYear(id: string | null): Promise<void> {
    if (!id) {
      this.detail.set(null);
      this.courses.set([]);
      return;
    }
    const [detail, courses] = await Promise.all([
      this.api.get<YearDetail>(`/api/v1/academic/years/${id}`),
      this.api.get<CourseView[]>(`/api/v1/years/${id}/courses`).catch(() => [] as CourseView[]),
    ]);
    this.detail.set(detail);
    this.courses.set(courses);
  }

  async select(year: YearSummary): Promise<void> {
    await this.router.navigate([], { queryParams: { year: year.id } });
    await this.loadYear(year.id);
  }

  preview(): { startsOn: string; endsOn: string }[] {
    const y = this.mode() === 'year' ? this.yearForm : this.detail()?.year;
    const count = this.mode() === 'year' ? this.setupForm.periods : this.generateCount;
    return y?.startsOn && y?.endsOn ? splitCalendar(y.startsOn, y.endsOn, +count) : [];
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
    const latest = [...this.years()].sort((a, b) => b.startsOn.localeCompare(a.startsOn))[0];
    this.yearForm = edit && y ? { label: y.label, startsOn: y.startsOn, endsOn: y.endsOn } : nextCycle(latest);
    this.setupForm = { periods: this.detail()?.periods.length || 4, generate: true, sections: true, courses: true, teachers: true };
    this.open('year', edit && y ? y.id : null);
  }
  /** Latest cycle, offered as the source when a new one is created. */
  latest(): YearSummary | undefined {
    return [...this.years()].sort((a, b) => b.startsOn.localeCompare(a.startsOn))[0];
  }
  openStatus(): void {
    const y = this.detail()?.year;
    this.open('status');
    if (y?.status === 'Draft') this.reason = `Apertura de inscripciones del ciclo ${y.label}`;
    else if (y?.status === 'EnrollmentOpen') this.reason = `Inicio de clases del ciclo ${y.label}`;
  }
  openGenerate(): void {
    this.generateCount = 4;
    this.open('generate');
  }
  openCopy(): void {
    const d = this.detail();
    this.copyForm = { sections: !d?.sections.length, courses: true, teachers: true };
    this.open('copy');
  }
  openPeriod(period?: PeriodView): void {
    const d = this.detail();
    const last = d?.periods.at(-1);
    const next = last ? new Date(Date.parse(last.endsOn + 'T00:00:00Z') + 86_400_000).toISOString().slice(0, 10) : d?.year.startsOn ?? '';
    this.periodForm = period
      ? { name: period.name, startsOn: period.startsOn, endsOn: period.endsOn }
      : { name: '', startsOn: next && d && next <= d.year.endsOn ? next : '', endsOn: '' };
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
      generate: `Generar ${this.school.terms.period.toLowerCase()}s`,
      copy: `Copiar de ${this.previous()?.label ?? 'otro ciclo'}`,
    }[this.mode()];
  }

  submitLabel(): string {
    const mode = this.mode();
    if (mode === 'status') return this.title();
    if (mode === 'generate') return 'Generar';
    if (mode === 'copy') return 'Copiar';
    if (mode === 'year' && !this.editingId()) return 'Crear ciclo';
    return 'Guardar';
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
            message = await this.prepareNewYear(goTo);
          }
          break;
        case 'generate':
          if (!year) return;
          await this.api.post(`/api/v1/academic/years/${year.id}/periods/generate`, {
            version: year.version,
            count: +this.generateCount,
          });
          message = `Se crearon ${this.generateCount} ${this.school.terms.period.toLowerCase()}s. Puedes ajustar sus fechas.`;
          break;
        case 'copy': {
          const source = this.previous();
          if (!year || !source) return;
          if (!this.copyForm.sections && !this.copyForm.courses) {
            this.dialogError.set('Elige qué copiar.');
            return;
          }
          message = (await this.copyFrom(year.id, source.id, this.copyForm)).join(' ') || 'No había nada nuevo que copiar.';
          break;
        }
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

  /** Optional shortcuts after creating a cycle; the cycle stays created even if one of them fails. */
  private async prepareNewYear(id: string): Promise<string> {
    const source = this.latest();
    const done: string[] = ['El ciclo se creó en borrador.'];
    try {
      if (this.setupForm.generate) {
        const detail = await this.api.get<YearDetail>(`/api/v1/academic/years/${id}`);
        await this.api.post(`/api/v1/academic/years/${id}/periods/generate`, {
          version: detail.year.version,
          count: +this.setupForm.periods,
        });
        done.push(`${this.setupForm.periods} ${this.school.terms.period.toLowerCase()}s generados.`);
      }
      if (source && (this.setupForm.sections || this.setupForm.courses))
        done.push(...(await this.copyFrom(id, source.id, this.setupForm)));
    } catch (error) {
      void this.alerts.error('El ciclo se creó, pero falta un paso', this.api.message(error));
    }
    return done.join(' ');
  }

  private async copyFrom(
    yearId: string,
    sourceId: string,
    options: { sections: boolean; courses: boolean; teachers: boolean },
  ): Promise<string[]> {
    const done: string[] = [];
    if (options.sections) {
      const r = await this.api.post<{ created: number }>(`/api/v1/academic/years/${yearId}/sections/copy`, {
        sourceYearId: sourceId,
      });
      if (r.created) done.push(`${r.created} sección${r.created === 1 ? '' : 'es'} copiada${r.created === 1 ? '' : 's'}.`);
    }
    if (options.courses) {
      const r = await this.api.post<{ created: number; skipped: number; teachers: number }>(
        `/api/v1/years/${yearId}/courses/copy`,
        { sourceYearId: sourceId, teachers: options.teachers },
      );
      if (r.created) done.push(`${r.created} curso${r.created === 1 ? '' : 's'} copiado${r.created === 1 ? '' : 's'}.`);
      if (r.teachers) done.push(`${r.teachers} docente${r.teachers === 1 ? '' : 's'} asignado${r.teachers === 1 ? '' : 's'}.`);
      if (r.skipped) done.push(`${r.skipped} curso${r.skipped === 1 ? '' : 's'} sin copiar (grado, sección o materia ya no vigente).`);
    }
    return done;
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
