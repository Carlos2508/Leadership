import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { GradeView, YearDetail, YearSummary, todayIn, yearStatusLabels } from '../core/academic-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

export interface SubjectView {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  version: string;
}
export interface TeacherView {
  assignmentId: string;
  staffId: string;
  name: string;
  role: 'Lead' | 'Assistant';
  startsOn: string;
  endsOn: string | null;
}
export interface CourseView {
  id: string;
  academicYearId: string;
  year: string;
  gradeLevelId: string;
  grade: string;
  sectionId: string | null;
  section: string | null;
  subjectId: string;
  subject: string;
  teachers: TeacherView[];
  students: number;
}
interface StaffOption {
  id: string;
  name: string;
  position: string;
}
type Mode = 'subject' | 'create' | 'assign' | 'unassign' | 'copy';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  templateUrl: './courses.html',
})
export class Courses {
  readonly year = input<string | undefined>();
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('courseDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;

  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly subjects = signal<SubjectView[]>([]);
  readonly detail = signal<YearDetail | null>(null);
  readonly courses = signal<CourseView[]>([]);
  readonly staff = signal<StaffOption[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly mode = signal<Mode>('subject');
  readonly selectedCourse = signal<CourseView | null>(null);
  readonly selectedTeacher = signal<TeacherView | null>(null);
  readonly editingSubject = signal<SubjectView | null>(null);
  readonly canManage = computed(() => this.session.can('academic.manage'));
  readonly yearLabels = yearStatusLabels;
  readonly onlyUnassigned = signal(false);
  readonly unassigned = computed(() => this.courses().filter((c) => !c.teachers.length).length);
  /** Cycle before the selected one: source for «Copiar de…». */
  readonly previous = computed<YearSummary | undefined>(() => {
    const current = this.years().find((x) => x.id === this.selectedYear());
    return this.years()
      .filter((x) => current && x.id !== current.id && x.startsOn < current.startsOn)
      .sort((a, b) => b.startsOn.localeCompare(a.startsOn))[0];
  });
  private readonly selectedYear = signal('');
  readonly groups = computed(() => {
    const map = new Map<string, { grade: string; courses: CourseView[] }>();
    for (const course of this.courses().filter((c) => !this.onlyUnassigned() || !c.teachers.length)) {
      const group = map.get(course.gradeLevelId) ?? { grade: course.grade, courses: [] };
      group.courses.push(course);
      map.set(course.gradeLevelId, group);
    }
    return [...map.values()];
  });

  yearId = '';
  gradeFilter = '';
  subjectForm = { code: '', name: '', isActive: true };
  createForm = { grades: {} as Record<string, boolean>, perSection: false, subjects: {} as Record<string, boolean> };
  copyTeachers = true;
  assignForm = { search: '', staffId: '', role: 'Lead', startsOn: '' };
  reason = '';

  constructor() {
    queueMicrotask(() => void this.init());
  }

  sectionsFor(gradeId: string) {
    return (this.detail()?.sections ?? []).filter((x) => x.gradeLevelId === gradeId);
  }

  async init(): Promise<void> {
    this.loading.set(true);
    try {
      const [years, grades, subjects] = await Promise.all([
        this.api.get<YearSummary[]>('/api/v1/academic/years'),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
        this.api.get<SubjectView[]>('/api/v1/academic/subjects'),
      ]);
      this.years.set(years);
      this.grades.set(grades);
      this.subjects.set(subjects);
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
    this.selectedYear.set(this.yearId);
    const query = this.gradeFilter ? `?gradeId=${this.gradeFilter}` : '';
    const [detail, courses] = await Promise.all([
      this.api.get<YearDetail>(`/api/v1/academic/years/${this.yearId}`),
      this.api.get<CourseView[]>(`/api/v1/years/${this.yearId}/courses${query}`),
    ]);
    this.detail.set(detail);
    this.courses.set(courses);
  }

  private open(mode: Mode): void {
    this.mode.set(mode);
    this.dialogError.set('');
    this.reason = '';
    this.dialog.nativeElement.showModal();
  }
  openSubject(subject?: SubjectView): void {
    this.editingSubject.set(subject ?? null);
    this.subjectForm = subject
      ? { code: subject.code, name: subject.name, isActive: subject.isActive }
      : { code: '', name: '', isActive: true };
    this.open('subject');
  }
  openCreate(): void {
    this.createForm = {
      grades: this.gradeFilter ? { [this.gradeFilter]: true } : {},
      perSection: false,
      subjects: {},
    };
    this.open('create');
  }
  openCopy(): void {
    this.copyTeachers = true;
    this.open('copy');
  }
  activeGrades() {
    return this.grades().filter((x) => x.isActive);
  }
  activeSubjects() {
    return this.subjects().filter((x) => x.isActive);
  }
  allChecked(map: Record<string, boolean>, ids: string[]): boolean {
    return ids.length > 0 && ids.every((id) => map[id]);
  }
  toggleAll(map: Record<string, boolean>, ids: string[]): void {
    const value = !this.allChecked(map, ids);
    for (const id of ids) map[id] = value;
  }
  ids(list: { id: string }[]): string[] {
    return list.map((x) => x.id);
  }
  /** How many courses the dialog would create (before skipping the ones that already exist). */
  plannedCourses(): number {
    const subjects = Object.values(this.createForm.subjects).filter(Boolean).length;
    return Object.keys(this.createForm.grades)
      .filter((g) => this.createForm.grades[g])
      .reduce((n, g) => n + subjects * (this.createForm.perSection ? Math.max(1, this.sectionsFor(g).length) : 1), 0);
  }
  openAssign(course: CourseView): void {
    this.selectedCourse.set(course);
    this.assignForm = { search: '', staffId: '', role: course.teachers.length ? 'Assistant' : 'Lead', startsOn: '' };
    this.staff.set([]);
    this.open('assign');
    this.reason = `Asignación de carga académica ${course.year}`;
    void this.searchStaff();
  }
  openUnassign(course: CourseView, teacher: TeacherView): void {
    this.selectedCourse.set(course);
    this.selectedTeacher.set(teacher);
    this.open('unassign');
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
      subject: this.editingSubject() ? 'Editar materia' : 'Nueva materia',
      create: 'Crear cursos',
      assign: 'Asignar docente',
      unassign: 'Finalizar asignación',
      copy: `Copiar cursos de ${this.previous()?.label ?? 'otro ciclo'}`,
    }[this.mode()];
  }

  async searchStaff(): Promise<void> {
    try {
      this.staff.set(
        await this.api.get<StaffOption[]>(`/api/v1/academic/staff?search=${encodeURIComponent(this.assignForm.search)}`),
      );
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    }
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    this.dialogError.set('');
    this.busy.set(true);
    try {
      let message = 'El cambio se guardó.';
      switch (this.mode()) {
        case 'subject': {
          const subject = this.editingSubject();
          await this.api.post(subject ? `/api/v1/academic/subjects/${subject.id}` : '/api/v1/academic/subjects', {
            ...this.subjectForm,
            version: subject?.version ?? null,
          });
          this.subjects.set(await this.api.get<SubjectView[]>('/api/v1/academic/subjects'));
          message = 'La materia se guardó.';
          break;
        }
        case 'create': {
          const subjectIds = Object.keys(this.createForm.subjects).filter((x) => this.createForm.subjects[x]);
          const gradeIds = this.activeGrades()
            .map((g) => g.id)
            .filter((id) => this.createForm.grades[id]);
          if (!gradeIds.length || !subjectIds.length) {
            this.dialogError.set('Selecciona al menos un grado y una materia.');
            return;
          }
          // One request per grade or section: each one is atomic on the server and reports its own conflicts.
          let created = 0;
          const failed: string[] = [];
          for (const gradeId of gradeIds) {
            const sections = this.createForm.perSection ? this.sectionsFor(gradeId) : [];
            for (const sectionId of sections.length ? sections.map((x) => x.id) : [null]) {
              try {
                created += (
                  await this.api.post<{ created: number }>(`/api/v1/years/${this.yearId}/courses`, {
                    gradeLevelId: gradeId,
                    sectionId,
                    subjectIds,
                  })
                ).created;
              } catch (error) {
                const grade = this.grades().find((g) => g.id === gradeId)?.name ?? '';
                const section = sections.find((x) => x.id === sectionId)?.name;
                failed.push(`${grade}${section ? ' ' + section : ''}: ${this.api.message(error)}`);
              }
            }
          }
          if (failed.length) {
            await this.load();
            this.dialogError.set(
              `${created ? `Se crearon ${created} cursos. ` : ''}No se pudieron crear en ${failed.join(' · ')}`,
            );
            return;
          }
          message = created ? (created === 1 ? 'Se creó 1 curso.' : `Se crearon ${created} cursos.`) : 'Esos cursos ya existían.';
          break;
        }
        case 'copy': {
          const source = this.previous();
          if (!source) return;
          const r = await this.api.post<{ created: number; skipped: number; teachers: number }>(
            `/api/v1/years/${this.yearId}/courses/copy`,
            { sourceYearId: source.id, teachers: this.copyTeachers },
          );
          message =
            [
              r.created ? `Se copiaron ${r.created} cursos.` : '',
              r.teachers ? `${r.teachers} con su docente.` : '',
              r.skipped ? `${r.skipped} no se copiaron porque su grado, sección o materia ya no está vigente.` : '',
            ]
              .filter(Boolean)
              .join(' ') || 'No había cursos nuevos que copiar.';
          break;
        }
        case 'assign': {
          if (!this.assignForm.staffId) {
            this.dialogError.set('Selecciona a la persona docente.');
            return;
          }
          await this.api.post(`/api/v1/courses/${this.selectedCourse()!.id}/teachers`, {
            staffId: this.assignForm.staffId,
            role: this.assignForm.role,
            startsOn: this.assignForm.startsOn || null,
            reason: this.reason,
          });
          message = 'La asignación docente quedó registrada.';
          break;
        }
        case 'unassign':
          await this.api.post(
            `/api/v1/courses/${this.selectedCourse()!.id}/teachers/${this.selectedTeacher()!.assignmentId}/end`,
            { endsOn: todayIn(this.school.locale.timeZone), reason: this.reason },
          );
          message = 'La asignación finalizó. Se conserva en el historial.';
          break;
      }
      this.busy.set(false);
      this.dialog.nativeElement.close();
      this.alerts.success(message);
      await this.load();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
