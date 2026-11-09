import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, HostListener, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccessApi, PageResult } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { InstitutionService } from '../core/institution';
import {
  PeopleCatalog,
  PersonDetail,
  PersonSummary,
  facultyLabels,
  relationshipLabels,
} from '../core/people-api';
import { GradeView, YearDetail, YearSummary, todayIn } from '../core/academic-api';
import { PendingChanges } from '../core/pending-changes';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

interface PersonForm {
  existingId: string;
  existingName: string;
  version: string;
  givenNames: string;
  familyNames: string;
  birthDate: string;
  phone: string;
  email: string;
  studentCode: string;
  confirmDuplicate: boolean;
  search: string;
  candidates: PersonSummary[];
  searching: boolean;
  searchError: string;
}
interface GuardianForm extends PersonForm {
  kind: string;
  faculties: string[];
}
interface LinkForm {
  selected: boolean;
  custom: boolean;
  kind: string;
  faculties: string[];
}
interface StudentForm extends PersonForm {
  gradeId: string;
  sectionId: string;
  links: LinkForm[];
}
interface Result {
  requestId: string;
  academicYearId: string;
  guardianIds: string[];
  students: { personId: string; enrollmentId: string; relationshipIds: string[] }[];
}
const blankPerson = (): PersonForm => ({
  existingId: '',
  existingName: '',
  version: '',
  givenNames: '',
  familyNames: '',
  birthDate: '',
  phone: '',
  email: '',
  studentCode: '',
  confirmDuplicate: false,
  search: '',
  candidates: [],
  searching: false,
  searchError: '',
});

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, NgTemplateOutlet],
  templateUrl: './family-registration.html',
})
export class FamilyRegistration implements PendingChanges {
  readonly api = inject(AccessApi);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly alerts = inject(Alerts);
  private readonly school = inject(InstitutionService).current;
  private readonly route = inject(ActivatedRoute);
  readonly years = signal<YearSummary[]>([]);
  readonly grades = signal<GradeView[]>([]);
  readonly detail = signal<YearDetail | null>(null);
  readonly catalog = signal<PeopleCatalog | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly step = signal(0);
  readonly duplicate = signal(false);
  readonly result = signal<Result | null>(null);
  readonly pending = signal<object | null>(null);
  readonly facultyLabels = facultyLabels;
  readonly relationshipLabels = relationshipLabels;
  guardians: GuardianForm[] = [];
  students: StudentForm[] = [];
  yearId = '';
  enrolledOn = '';
  reason = 'Registro familiar e inscripción revisados por Secretaría.';
  private requestId = crypto.randomUUID();
  private baseline = '';
  private yearRequest = 0;
  constructor() {
    this.addGuardian();
    this.addStudent();
    this.baseline = this.snapshot();
    void this.load();
  }
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [years, grades, catalog] = await Promise.all([
        this.api.get<YearSummary[]>('/api/v1/academic/years'),
        this.api.get<GradeView[]>('/api/v1/academic/grades'),
        this.api.get<PeopleCatalog>('/api/v1/people/catalog'),
      ]);
      this.years.set(years.filter((y) => ['EnrollmentOpen', 'Active'].includes(y.status)));
      this.grades.set(grades.filter((g) => g.isActive));
      this.catalog.set(catalog);
      const requested = this.route.snapshot.queryParamMap.get('year');
      this.yearId =
        requested ?? this.years().find((y) => y.isDefault)?.id ?? this.years()[0]?.id ?? '';
      if (requested && !this.years().some((y) => y.id === requested)) {
        this.detail.set(null);
        this.error.set(
          'El ciclo indicado no admite inscripciones. Elige explícitamente otro ciclo.',
        );
      } else if (this.yearId) await this.changeYear();
    } catch (e) {
      this.error.set(this.api.message(e));
    } finally {
      this.loading.set(false);
    }
  }
  async changeYear(): Promise<void> {
    const request = ++this.yearRequest;
    this.detail.set(null);
    this.error.set('');
    for (const student of this.students) student.sectionId = '';
    if (!this.yearId) return;
    try {
      const detail = await this.api.get<YearDetail>('/api/v1/academic/years/' + this.yearId);
      if (request !== this.yearRequest) return;
      this.detail.set(detail);
      const today = todayIn(this.school.locale.timeZone);
      if (
        !this.enrolledOn ||
        this.enrolledOn < detail.year.startsOn ||
        this.enrolledOn > detail.year.endsOn
      )
        this.enrolledOn =
          today < detail.year.startsOn
            ? detail.year.startsOn
            : today > detail.year.endsOn
              ? detail.year.endsOn
              : today;
    } catch (e) {
      if (request === this.yearRequest) this.error.set(this.api.message(e));
    }
  }
  sections(student: StudentForm) {
    return this.detail()?.sections.filter((s) => s.gradeLevelId === student.gradeId) ?? [];
  }
  addGuardian(): void {
    if (this.guardians.length >= 4) return;
    this.guardians.push({ ...blankPerson(), kind: 'Mother', faculties: [] });
    for (const student of this.students)
      student.links.push({ selected: true, custom: false, kind: 'Mother', faculties: [] });
  }
  removeGuardian(index: number): void {
    this.guardians.splice(index, 1);
    for (const student of this.students) student.links.splice(index, 1);
  }
  addStudent(): void {
    if (this.students.length >= 8) return;
    this.students.push({
      ...blankPerson(),
      gradeId: '',
      sectionId: '',
      links: this.guardians.map((g) => ({
        selected: true,
        custom: false,
        kind: g.kind,
        faculties: [],
      })),
    });
  }
  async search(person: PersonForm, guardian: boolean): Promise<void> {
    if (person.search.trim().length < 2) {
      person.searchError = 'Escribe al menos dos letras.';
      return;
    }
    const term = person.search.trim();
    person.searching = true;
    person.searchError = '';
    person.candidates = [];
    try {
      const data = await this.api.get<PageResult<PersonSummary>>(
        `/api/v1/people?role=${guardian ? 'guardian' : 'student'}&status=Active&page=1&search=${encodeURIComponent(term)}`,
      );
      if (person.search.trim() === term) person.candidates = data.items;
    } catch (e) {
      person.searchError = this.api.message(e);
    } finally {
      person.searching = false;
      this.changeDetector.markForCheck();
    }
  }
  async select(person: PersonForm, item: PersonSummary, guardian: boolean): Promise<void> {
    person.searching = true;
    try {
      const record = await this.api.get<PersonDetail>('/api/v1/people/' + item.id);
      if (record.status !== 'Active' || (guardian ? !record.isGuardian : !record.studentCode)) {
        person.searchError = 'La ficha cambió. Vuelve a buscarla.';
        return;
      }
      person.existingId = record.id;
      person.existingName = record.displayName;
      person.version = record.version;
      person.candidates = [];
      if (!guardian) {
        const student = person as StudentForm;
        this.guardians.forEach((g, i) => {
          const existing = record.guardians.find((r) => r.personId === g.existingId && !r.endedAt);
          if (existing)
            student.links[i] = {
              selected: true,
              custom: true,
              kind: existing.kind,
              faculties: [...existing.faculties],
            };
        });
      }
    } catch (e) {
      person.searchError = this.api.message(e);
    } finally {
      person.searching = false;
      this.changeDetector.markForCheck();
    }
  }
  clear(person: PersonForm): void {
    person.existingId = '';
    person.existingName = '';
    person.version = '';
    person.searchError = '';
    const guardianIndex = this.guardians.indexOf(person as GuardianForm);
    if (guardianIndex >= 0) {
      this.students.forEach(
        (s) =>
          (s.links[guardianIndex] = {
            selected: true,
            custom: false,
            kind: 'Mother',
            faculties: [],
          }),
      );
    } else {
      const student = person as StudentForm;
      student.links = this.guardians.map(() => ({
        selected: true,
        custom: false,
        kind: 'Mother',
        faculties: [],
      }));
    }
  }
  toggle(list: string[], key: string): void {
    const index = list.indexOf(key);
    if (index < 0) list.push(key);
    else list.splice(index, 1);
  }
  name(person: PersonForm): string {
    return (
      person.existingName ||
      [person.givenNames, person.familyNames].filter(Boolean).join(' ') ||
      'Sin completar'
    );
  }
  grade(student: StudentForm): string {
    return [
      this.grades().find((g) => g.id === student.gradeId)?.name,
      this.sections(student).find((s) => s.id === student.sectionId)?.name,
    ]
      .filter(Boolean)
      .join(' · ');
  }
  linkKind(link: LinkForm, index: number): string {
    return link.custom ? link.kind : this.guardians[index].kind;
  }
  linkFaculties(link: LinkForm, index: number): string[] {
    return link.custom ? link.faculties : this.guardians[index].faculties;
  }
  private personInput(person: PersonForm, guardian: boolean): object {
    return person.existingId
      ? { existingId: person.existingId, version: person.version }
      : {
          givenNames: person.givenNames,
          familyNames: person.familyNames,
          birthDate: person.birthDate || null,
          phone: person.phone || null,
          email: person.email || null,
          studentCode: guardian ? null : person.studentCode,
          confirmDuplicate: person.confirmDuplicate,
        };
  }
  private payload(): object {
    return {
      requestId: this.requestId,
      academicYearId: this.yearId,
      enrolledOn: this.enrolledOn,
      reason: this.reason,
      guardians: this.guardians.map((g) => ({ person: this.personInput(g, true) })),
      students: this.students.map((s) => ({
        person: this.personInput(s, false),
        gradeId: s.gradeId,
        sectionId: s.sectionId || null,
        guardians: s.links.flatMap((l, i) =>
          l.selected
            ? [{ guardianIndex: i, kind: this.linkKind(l, i), faculties: this.linkFaculties(l, i) }]
            : [],
        ),
      })),
    };
  }
  private snapshot(): string {
    return JSON.stringify({
      guardians: this.guardians.map((g) => ({
        person: this.personInput(g, true),
        kind: g.kind,
        faculties: g.faculties,
      })),
      students: this.students.map((s) => ({
        person: this.personInput(s, false),
        grade: s.gradeId,
        section: s.sectionId,
        links: s.links,
      })),
    });
  }
  private validate(): boolean {
    const items: [PersonForm, boolean, string][] =
      this.step() === 0
        ? this.guardians.map((g, i) => [g, true, `Encargado ${i + 1}`])
        : this.students.map((s, i) => [s, false, `Estudiante ${i + 1}`]);
    for (const [person, guardian, label] of items)
      if (
        !person.existingId &&
        (!person.givenNames.trim() ||
          !person.familyNames.trim() ||
          (!guardian && !person.studentCode.trim()))
      ) {
        this.error.set(label + ': completa nombres, apellidos y código cuando corresponda.');
        return false;
      }
    if (
      this.step() !== 0 &&
      (!this.detail() || !this.students.every((s) => s.gradeId) || !this.enrolledOn)
    ) {
      this.error.set('Elige el ciclo, la fecha y el grado de cada estudiante.');
      return false;
    }
    this.error.set('');
    return true;
  }
  next(): void {
    if (this.validate()) this.step.update((s) => Math.min(2, s + 1));
  }
  async canLeave(): Promise<boolean> {
    if (this.busy()) return false;
    if (this.result() || (!this.pending() && this.baseline === this.snapshot())) return true;
    return this.alerts.confirm({
      title: this.pending() ? 'Hay un registro sin confirmar' : 'Hay una familia sin guardar',
      text: this.pending()
        ? 'El registro podría haberse guardado. Revisa las fichas e inscripciones antes de volver a registrarlo. ¿Quieres salir?'
        : '¿Quieres salir y descartar los datos de este formulario?',
      confirmText: 'Salir',
    });
  }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (!this.result() && (this.busy() || this.pending() || this.baseline !== this.snapshot())) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  async save(): Promise<void> {
    if (this.busy()) return;
    this.pending.set(this.pending() ?? structuredClone(this.payload()));
    this.busy.set(true);
    this.error.set('');
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      const result = await Promise.race([
        this.api.post<Result>('/api/v1/people/families', this.pending()!),
        new Promise<never>((_, reject) => {
          timer = setTimeout(() => reject(new Error('timeout')), 20000);
        }),
      ]);
      this.result.set(result);
      this.pending.set(null);
    } catch (e) {
      if (e instanceof HttpErrorResponse && [400, 401, 403, 404, 409, 429].includes(e.status))
        this.pending.set(null);
      if (e instanceof HttpErrorResponse && e.error?.code === 'possible-duplicate') {
        this.duplicate.set(true);
        this.step.set(String(e.error?.title).startsWith('Encargado') ? 0 : 1);
      }
      this.error.set(
        this.pending()
          ? 'No se pudo confirmar el registro. Reintenta para consultar o completar el mismo intento, sin duplicar personas ni inscripciones.'
          : this.api.message(e),
      );
    } finally {
      clearTimeout(timer);
      this.busy.set(false);
    }
  }
}
