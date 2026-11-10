/** Shared types of the student/family self-service pages (tasks, grades, attendance). */

export interface StudentAccessView {
  student: { id: string; displayName: string; studentCode: string; relationship: string };
  isSelf: boolean;
  faculties: string[];
  placement: { year: string; grade: string; section: string | null } | null;
}

export interface StudentTask {
  id: string;
  courseId: string;
  course: string;
  title: string;
  instructions: string;
  dueAt: string;
  allowLate: boolean;
  closed: boolean;
  submissionStatus: 'Submitted' | 'Reviewed' | null;
  submittedAt: string | null;
  late: boolean;
  text: string | null;
  feedback: string | null;
  version: string | null;
  canSubmit: boolean;
  points: number | null;
  period: string | null;
  publishedAt: string | null;
}

export interface MyActivity {
  title: string;
  maxPoints: number;
  points: number | null;
  status: 'Scored' | 'Missing' | 'Excused' | null;
  assignmentId: string | null;
}
export interface MyGrade {
  subject: string;
  score: number | null;
  status: 'Scored' | 'Missing' | 'Excused';
  activities: MyActivity[] | null;
}
export interface MyGradesPeriod {
  periodId: string;
  sequence: number;
  name: string;
  status: 'Available' | 'Blocked' | 'PendingFinancialSetup';
  grades: MyGrade[];
  pending: { chargeId: string | null; label: string; balance: number }[];
}
export interface MyGrades {
  studentId: string;
  year: string | null;
  periods: MyGradesPeriod[];
}

export type AttendanceState = 'Present' | 'Absent' | 'Late' | 'Excused';
export interface CalendarDay {
  date: string;
  entries: { subject: string; status: AttendanceState; note: string }[];
}
export interface AttendanceCalendar {
  from: string;
  to: string;
  present: number;
  absent: number;
  late: number;
  excused: number;
  days: CalendarDay[];
}

export const attendanceLabels: Record<AttendanceState, string> = {
  Present: 'Presente',
  Absent: 'Ausente',
  Late: 'Tarde',
  Excused: 'Justificada',
};

/** Worst state of a day: one absence marks the day, then lateness, then excused. */
export function dayState(day: CalendarDay | undefined): AttendanceState | null {
  if (!day?.entries.length) return null;
  const states = day.entries.map((x) => x.status);
  if (states.includes('Absent')) return 'Absent';
  if (states.includes('Late')) return 'Late';
  if (states.includes('Excused')) return 'Excused';
  return 'Present';
}

/** Local calendar date (yyyy-mm-dd) of a Date, without UTC shifting. */
export function isoDate(value: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
}

/** Monday-first weeks of a month, with leading/trailing days of the neighbour months marked as outside. */
export function monthWeeks(year: number, month: number): { date: string; day: number; inside: boolean }[][] {
  const first = new Date(year, month, 1);
  const start = new Date(year, month, 1 - ((first.getDay() + 6) % 7));
  const weeks: { date: string; day: number; inside: boolean }[][] = [];
  const cursor = new Date(start);
  do {
    const week: { date: string; day: number; inside: boolean }[] = [];
    for (let i = 0; i < 7; i++) {
      week.push({ date: isoDate(cursor), day: cursor.getDate(), inside: cursor.getMonth() === month });
      cursor.setDate(cursor.getDate() + 1);
    }
    weeks.push(week);
  } while (cursor.getMonth() === month);
  return weeks;
}

export const weekdayShort = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

/** Colour slot (0–5) for a subject name, stable across pages. */
export function subjectTone(name: string): number {
  let hash = 0;
  for (const char of name) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return hash % 6;
}

export function points(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/\.?0+$/, '');
}

/** First letter in upper case only («Noviembre de 2026», «Lunes, 9 de noviembre»). */
export function capitalize(text: string): string {
  return text ? text[0].toLocaleUpperCase() + text.slice(1) : text;
}
