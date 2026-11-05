export type YearStatus = 'Draft' | 'EnrollmentOpen' | 'Active' | 'Closed';
export type EnrollmentStatus = 'Active' | 'Withdrawn' | 'Cancelled' | 'Completed';

export interface YearSummary {
  id: string;
  label: string;
  startsOn: string;
  endsOn: string;
  status: YearStatus;
  isDefault: boolean;
  version: string;
  enrollments: number;
}
export interface PeriodView {
  id: string;
  sequence: number;
  name: string;
  startsOn: string;
  endsOn: string;
}
export interface SectionView {
  id: string;
  gradeLevelId: string;
  name: string;
  shift: string | null;
  capacity: number | null;
  current: number;
  version: string;
}
export interface YearDetail {
  year: YearSummary;
  periods: PeriodView[];
  sections: SectionView[];
}
export interface GradeView {
  id: string;
  code: string;
  name: string;
  stage: string;
  sequence: number;
  isActive: boolean;
  version: string;
}
export interface EnrollmentRow {
  id: string;
  studentId: string;
  studentName: string;
  studentCode: string;
  status: EnrollmentStatus;
  enrolledOn: string;
  withdrawnOn: string | null;
  gradeLevelId: string | null;
  grade: string | null;
  sectionId: string | null;
  section: string | null;
  version: string;
}
export interface PlacementView {
  id: string;
  gradeLevelId: string;
  grade: string;
  sectionId: string | null;
  section: string | null;
  startsOn: string;
  endsOn: string | null;
  reason: string;
}
export interface EnrollmentHistory {
  id: string;
  academicYearId: string;
  year: string;
  yearStatus: YearStatus;
  status: EnrollmentStatus;
  enrolledOn: string;
  withdrawnOn: string | null;
  version: string;
  placements: PlacementView[];
}
export interface CurrentPlacement {
  year: string;
  grade: string;
  section: string | null;
  status: EnrollmentStatus;
}

export const yearStatusLabels: Record<YearStatus, string> = {
  Draft: 'Borrador',
  EnrollmentOpen: 'Inscripciones abiertas',
  Active: 'En curso',
  Closed: 'Cerrado',
};
export const nextYearStatus: Record<YearStatus, YearStatus | null> = {
  Draft: 'EnrollmentOpen',
  EnrollmentOpen: 'Active',
  Active: 'Closed',
  Closed: null,
};
export const nextYearAction: Record<YearStatus, string> = {
  Draft: 'Abrir inscripciones',
  EnrollmentOpen: 'Iniciar ciclo',
  Active: 'Cerrar ciclo',
  Closed: '',
};
export const enrollmentStatusLabels: Record<EnrollmentStatus, string> = {
  Active: 'Inscrito',
  Withdrawn: 'Retirado',
  Cancelled: 'Anulado',
  Completed: 'Finalizado',
};

/** Today's calendar date in the institution's time zone, as yyyy-MM-dd. */
export function todayIn(timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone }).format(new Date());
}
