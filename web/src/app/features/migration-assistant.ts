import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Shell } from '../shared/shell';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { SessionService } from '../core/session';

interface Year {
  id: string;
  label: string;
  startsOn: string;
  endsOn: string;
  status: string;
}
interface Grade {
  id: string;
  name: string;
  sequence: number;
  stage: string;
}
interface Section {
  id: string;
  academicYearId: string;
  gradeLevelId: string;
  name: string;
}
interface Catalog {
  server: string;
  database: string;
  years: Year[];
  grades: Grade[];
  sections: Section[];
  needsPreparation: boolean;
}
interface Student {
  legacyId: string;
  fullName: string;
  givenNames: string;
  familyNames: string;
  birthDate: string | null;
  code: string;
  sourceGradeId: string;
  sourceGradeName: string;
  familyId: string | null;
  include: boolean;
  gradeId: string | null;
  sectionId: string | null;
  distinctPersonConfirmed: boolean;
  notices: string[];
}
interface Adult {
  key: string;
  fullName: string;
  givenNames: string;
  familyNames: string;
  kind: string;
  include: boolean;
  distinctPersonConfirmed: boolean;
  sameAsKey: string | null;
}
interface Family {
  legacyId: string;
  adults: Adult[];
  notices: string[];
}
interface Review {
  runId: string;
  nameFormat: string;
  yearId: string;
  yearLabel: string;
  enrolledOn: string;
  students: Student[];
  families: Family[];
  confirmed: boolean;
}
interface Report {
  batchId: string;
  yearId: string;
  yearLabel: string;
  server: string;
  database: string;
  planSha256: string;
  students: number;
  guardians: number;
  relationships: number;
  accounts: number;
  excluded: number;
  alreadyApplied: boolean;
  observations: string[];
}
interface Result {
  report: Report;
  backupVerified: boolean;
}
const base = '/api/v1/migration-assistant';

@Component({ imports: [Shell, FormsModule, RouterLink], templateUrl: './migration-assistant.html' })
export class MigrationAssistant {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  readonly session = inject(SessionService);
  readonly catalog = signal<Catalog | null>(null);
  readonly sourceYears = signal<{ schoolyearID: string; schoolyear: string; students: number }[]>(
    [],
  );
  readonly recent = signal<Review[]>([]);
  readonly review = signal<Review | null>(null);
  readonly plan = signal<Report | null>(null);
  readonly result = signal<Result | null>(null);
  readonly loading = signal(true);
  readonly busy = signal('');
  readonly error = signal('');
  readonly dirty = signal(false);
  readonly step = computed(() => (this.result() ? 4 : this.plan() ? 3 : this.review() ? 2 : 1));
  readonly openYears = computed(
    () =>
      this.catalog()?.years.filter((y) => y.status === 'EnrollmentOpen' || y.status === 'Active') ??
      [],
  );
  readonly selectedYear = computed(() =>
    this.catalog()?.years.find((y) => y.id === this.targetYearId()),
  );
  readonly targetYearId = signal('');
  readonly search = signal('');
  readonly warningsOnly = signal(true);
  readonly familyWarningsOnly = signal(true);
  readonly limit = signal(25);
  readonly familyLimit = signal(20);
  readonly included = computed(() => this.review()?.students.filter((s) => s.include).length ?? 0);
  readonly groups = computed(() => {
    const map = new Map<
      string,
      { id: string; name: string; count: number; grade: string; section: string | null }
    >();
    for (const s of this.review()?.students ?? []) {
      const g = map.get(s.sourceGradeId) ?? {
        id: s.sourceGradeId,
        name: s.sourceGradeName,
        count: 0,
        grade: s.gradeId ?? '',
        section: s.sectionId,
      };
      g.count++;
      map.set(g.id, g);
    }
    return [...map.values()].map((g) => ({
      ...g,
      grade: this.review()
        ?.students.filter((s) => s.sourceGradeId === g.id)
        .every((s) => !s.include)
        ? 'exclude'
        : g.grade,
    }));
  });
  readonly students = computed(() =>
    (this.review()?.students ?? [])
      .filter(
        (s) =>
          (!this.warningsOnly() || s.notices.length > 0 || !s.givenNames || !s.familyNames) &&
          (!this.search() ||
            (s.fullName + ' ' + s.code)
              .toLocaleLowerCase()
              .includes(this.search().toLocaleLowerCase())),
      )
      .slice(0, this.limit()),
  );
  readonly filteredFamilies = computed(() =>
    (this.review()?.families ?? []).filter(
      (f) => !this.familyWarningsOnly() || f.notices.length > 0,
    ),
  );
  readonly familyRows = computed(() => this.filteredFamilies().slice(0, this.familyLimit()));
  readonly filteredStudentCount = computed(
    () =>
      (this.review()?.students ?? []).filter(
        (s) =>
          (!this.warningsOnly() || s.notices.length > 0 || !s.givenNames || !s.familyNames) &&
          (!this.search() ||
            (s.fullName + ' ' + s.code)
              .toLocaleLowerCase()
              .includes(this.search().toLocaleLowerCase())),
      ).length,
  );
  manual = false;
  connection = { host: 'localhost', database: '', username: '', password: '', port: 3306 };
  sourceYearId = '';
  nameFormat = 'two-surnames';
  yearForm = { label: '2027', startsOn: '', endsOn: '' };

  constructor() {
    queueMicrotask(() => void this.load());
  }
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const c = await this.api.get<Catalog>(base + '/catalog');
      this.catalog.set(c);
      this.targetYearId.set(
        c.years.find((y) => y.label === '2027' && ['EnrollmentOpen', 'Active'].includes(y.status))
          ?.id ??
          c.years.find((y) => ['EnrollmentOpen', 'Active'].includes(y.status))?.id ??
          '',
      );
      if (!c.needsPreparation) this.recent.set(await this.api.get<Review[]>(base + '/recent'));
    } catch {
      this.error.set(
        'Abre «Migrar estudiantes» desde el acceso local y entra con una cuenta que gestione personas, inscripciones y cuentas.',
      );
    } finally {
      this.loading.set(false);
    }
  }
  private async action(label: string, run: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(label);
    try {
      await run();
    } catch (error) {
      await this.alerts.error('Revisa este paso', this.api.message(error));
    } finally {
      this.busy.set('');
    }
  }
  async connect(): Promise<void> {
    await this.action('Buscando la plataforma anterior…', async () => {
      if (this.catalog()?.needsPreparation)
        this.catalog.set(await this.api.post<Catalog>(base + '/prepare', {}));
      const source = await this.api.post<{
        years: { schoolyearID: string; schoolyear: string; students: number }[];
      }>(base + '/source', { connection: this.manual ? this.connection : null });
      this.sourceYears.set(source.years);
      this.sourceYearId =
        source.years.find((y) => y.schoolyear === '2027' && y.students > 0)?.schoolyearID ??
        source.years.filter((y) => y.students > 0).at(-1)?.schoolyearID ??
        '';
    });
  }
  async createYear(): Promise<void> {
    await this.action('Preparando el ciclo…', async () => {
      const created = await this.api.post<{ id: string }>(base + '/year', this.yearForm);
      this.catalog.set(await this.api.get<Catalog>(base + '/catalog'));
      this.targetYearId.set(created.id);
      this.alerts.success('Ciclo preparado', 'Las inscripciones están abiertas.');
    });
  }
  async read(): Promise<void> {
    await this.action('Preparando estudiantes y familias…', async () => {
      const review = await this.api.post<Review>(base + '/read', {
        sourceYearId: this.sourceYearId,
        targetYearId: this.targetYearId(),
        nameFormat: this.nameFormat,
        connection: this.manual ? this.connection : null,
      });
      this.connection.password = '';
      this.review.set(review);
      this.dirty.set(false);
      this.limit.set(25);
    });
  }
  resume(review: Review): void {
    this.review.set(structuredClone(review));
    this.plan.set(null);
    this.result.set(null);
    this.dirty.set(false);
  }
  changed(): void {
    this.review.update((r) => (r ? { ...r, confirmed: false } : null));
    this.plan.set(null);
    this.dirty.set(true);
  }
  mapGrade(id: string, value: string): void {
    const allExcluded = this.review()
      ?.students.filter((s) => s.sourceGradeId === id)
      .every((s) => !s.include);
    this.review.update((r) =>
      r
        ? {
            ...r,
            students: r.students.map((s) =>
              s.sourceGradeId === id
                ? {
                    ...s,
                    gradeId: value === 'exclude' ? null : value || null,
                    sectionId: null,
                    include: value === 'exclude' ? false : allExcluded || s.include,
                  }
                : s,
            ),
          }
        : null,
    );
    this.changed();
  }
  sections(student: Student): Section[] {
    return (
      this.catalog()?.sections.filter(
        (s) => s.academicYearId === this.review()?.yearId && s.gradeLevelId === student.gradeId,
      ) ?? []
    );
  }
  familyChildren(id: string): string {
    return (
      this.review()
        ?.students.filter((s) => s.familyId === id && s.include)
        .map((s) => s.fullName)
        .join(', ') ?? ''
    );
  }
  groupSections(grade: string): Section[] {
    return (
      this.catalog()?.sections.filter(
        (s) => s.academicYearId === this.review()?.yearId && s.gradeLevelId === grade,
      ) ?? []
    );
  }
  mapSection(source: string, grade: string, section: string | null): void {
    this.review.update((r) =>
      r
        ? {
            ...r,
            students: r.students.map((s) =>
              s.include && s.sourceGradeId === source && s.gradeId === grade
                ? { ...s, sectionId: section }
                : s,
            ),
          }
        : null,
    );
    this.changed();
  }
  adultMatches(adult: Adult): Adult[] {
    return (
      this.review()
        ?.families.flatMap((f) => f.adults)
        .filter(
          (a) =>
            a.key !== adult.key &&
            a.include &&
            !a.sameAsKey &&
            (a.key === adult.sameAsKey ||
              (a.givenNames.trim().toLocaleLowerCase() ===
                adult.givenNames.trim().toLocaleLowerCase() &&
                a.familyNames.trim().toLocaleLowerCase() ===
                  adult.familyNames.trim().toLocaleLowerCase())),
        ) ?? []
    );
  }
  clearDate(student: Student): void {
    student.birthDate = null;
    this.changed();
  }
  async copyGrades(): Promise<void> {
    const r = this.review();
    if (!r) return;
    if (
      !(await this.alerts.confirm({
        title: '¿Usar los grados del origen?',
        text: 'Se crearán los grados que todavía no existen. Después puedes elegir el destino de cada grupo.',
        confirmText: 'Crear grados faltantes',
      }))
    )
      return;
    await this.action('Preparando los grados…', async () => {
      await this.api.post(base + '/grades/' + r.runId, {});
      this.catalog.set(await this.api.get<Catalog>(base + '/catalog'));
      for (const group of this.groups()) {
        const match = this.catalog()?.grades.find((g) => g.name === group.name);
        if (match) this.mapGrade(group.id, match.id);
      }
    });
  }
  async save(): Promise<void> {
    const r = this.review();
    if (!r) return;
    await this.action('Guardando tu revisión…', async () => {
      this.review.set(await this.api.post<Review>(base + '/draft', { review: r }));
      this.plan.set(null);
      this.dirty.set(false);
      this.alerts.success('Revisión guardada', 'Puedes cerrar y continuar después.');
    });
  }
  async check(): Promise<void> {
    const r = this.review();
    if (!r) return;
    await this.action('Comprobando estudiantes, familias y cupos…', async () => {
      this.plan.set(await this.api.post<Report>(base + '/plan', { review: r }));
      this.dirty.set(false);
    });
  }
  async apply(): Promise<void> {
    const p = this.plan();
    const r = this.review();
    if (!p || !r) return;
    if (
      !(await this.alerts.confirm({
        title: `¿Importar ${p.students} estudiantes en ${p.yearLabel}?`,
        text: `Se creará y verificará un respaldo de ${p.database}. Todos los accesos nuevos quedarán inactivos.`,
        confirmText: 'Sí, importar estudiantes',
      }))
    )
      return;
    await this.action(
      'Creando respaldo e importando. Puedes dejar esta ventana abierta…',
      async () => {
        this.result.set(
          await this.api.post<Result>(base + '/apply', {
            runId: r.runId,
            planSha256: p.planSha256,
          }),
        );
        this.dirty.set(false);
        this.alerts.success('Importación verificada', 'Los accesos nuevos permanecen inactivos.');
      },
    );
  }
  async verify(): Promise<void> {
    const r = this.review();
    if (!r) return;
    await this.action('Verificando el resultado guardado…', async () => {
      this.result.set(await this.api.post<Result>(base + '/reconcile/' + r.runId, {}));
      this.dirty.set(false);
    });
  }
  backToReview(): void {
    this.plan.set(null);
  }
  async canLeave(): Promise<boolean> {
    if (!this.dirty() && !this.busy()) return true;
    return this.alerts.confirm({
      title: '¿Salir del asistente?',
      text: this.busy()
        ? 'Hay una operación en curso. Es mejor esperar su resultado.'
        : 'Guarda la revisión para conservar los últimos cambios.',
      confirmText: 'Salir',
      cancelText: 'Continuar aquí',
    });
  }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty() || this.busy()) event.preventDefault();
  }
}
