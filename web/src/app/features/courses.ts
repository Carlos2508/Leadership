import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
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
type Mode = 'subject' | 'create' | 'assign' | 'unassign';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  templateUrl: './courses.html',
})
export class Courses {
  readonly year = input<string | undefined>();
  readonly api = inject(AccessApi);
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
  readonly groups = computed(() => {
    const map = new Map<string, { grade: string; courses: CourseView[] }>();
    for (const course of this.courses()) {
      const group = map.get(course.gradeLevelId) ?? { grade: course.grade, courses: [] };
      group.courses.push(course);
      map.set(course.gradeLevelId, group);
    }
    return [...map.values()];
  });

  yearId = '';
  gradeFilter = '';
  subjectForm = { code: '', name: '', isActive: true };
  createForm = { gradeLevelId: '', sectionId: '', subjects: {} as Record<string, boolean> };
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
    this.createForm = { gradeLevelId: this.gradeFilter, sectionId: '', subjects: {} };
    this.open('create');
  }
  openAssign(course: CourseView): void {
    this.selectedCourse.set(course);
    this.assignForm = { search: '', staffId: '', role: course.teachers.length ? 'Assistant' : 'Lead', startsOn: '' };
    this.staff.set([]);
    this.open('assign');
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
          if (!this.createForm.gradeLevelId || !subjectIds.length) {
            this.dialogError.set('Selecciona el grado y al menos una materia.');
            return;
          }
          const result = await this.api.post<{ created: number }>(`/api/v1/years/${this.yearId}/courses`, {
            gradeLevelId: this.createForm.gradeLevelId,
            sectionId: this.createForm.sectionId || null,
            subjectIds,
          });
          message = result.created ? `Se crearon ${result.created} cursos.` : 'Esos cursos ya existían.';
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
      this.notice.set(message);
      await this.load();
      requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
