export interface TimePeriod {
  id: string;
  name: string;
  start: number;
  end: number;
}
export interface TimeStructure {
  days: number[];
  periods: TimePeriod[];
}
export interface TimeCell {
  periodId: string;
  day: number;
  kind: 'Lesson' | 'Break';
  courseId: string | null;
  assignmentId: string | null;
  roomId: string | null;
}
export interface TimeIssue {
  kind: string;
  message: string;
  day: number;
  periodId: string;
}
export interface TimeView {
  id: string;
  planId: string;
  yearId: string;
  year: string;
  gradeId: string;
  grade: string;
  sectionId: string | null;
  section: string | null;
  state: 'Draft' | 'Published';
  startsOn: string;
  through: string;
  publishedAt: string | null;
  version: string;
  structure: TimeStructure;
  cells: TimeCell[];
  issues: TimeIssue[];
}
export interface TimeSummary {
  planId: string;
  gradeId: string;
  grade: string;
  sectionId: string | null;
  section: string | null;
  draftId: string | null;
  publishedId: string | null;
  startsOn: string | null;
  through: string | null;
}
export interface TimeAssignment {
  id: string;
  courseId: string;
  staffId: string;
  name: string;
  startsOn: string;
  endsOn: string | null;
}
export interface TimeCatalog {
  rooms: { id: string; name: string }[];
  templates: { id: string; name: string; structure: TimeStructure }[];
  assignments: TimeAssignment[];
}
export interface TimeCourse {
  id: string;
  subjectId: string;
  subject: string;
  gradeLevelId: string;
  sectionId: string | null;
}
export interface TimeSlot {
  day: number;
  start: number;
  end: number;
  kind: 'Lesson' | 'Break';
  subject: string;
  teacher: string;
  grade: string;
  section: string | null;
  room: string;
  courseId: string | null;
  gradeId: string;
  sectionId: string | null;
}
export interface PublishedTime {
  structure: TimeStructure;
  title: string;
  year: string;
  on: string;
  slots: TimeSlot[];
}
export interface BoardCell {
  periodId: string;
  day: number;
  kind: string;
  title: string;
  teacher: string;
  room: string;
  warning?: boolean;
}
export const weekdayNames = [
  '',
  'Lunes',
  'Martes',
  'Miércoles',
  'Jueves',
  'Viernes',
  'Sábado',
  'Domingo',
];
export const clock = (minutes: number) =>
  `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
export const minutes = (value: string) => {
  const [h, m] = value.split(':').map(Number);
  return h * 60 + m;
};
