import { YearStatus } from './academic-api';
export interface DashboardYear {
  id: string;
  label: string;
  status: YearStatus;
  isDefault: boolean;
}
export interface DashboardPeriod {
  periodId: string;
  name: string;
  startsOn: string;
  endsOn: string;
  expected: number;
  recorded: number;
  published: boolean;
}
export interface DashboardTask {
  course: string;
  title: string;
  dueAt: string;
  overdue: boolean;
}
export interface Dashboard {
  asOf: string;
  on: string;
  years: DashboardYear[];
  yearId: string | null;
  academic: {
    year: string;
    enrolled: number;
    nextPeriod: string | null;
    recorded: number;
    expected: number;
    courses: number;
    unassignedCourses: number;
    byGrade: { grade: string; count: number }[];
    periods: DashboardPeriod[];
  } | null;
  finance: {
    pending: number;
    studentsWithBalance: number;
    paymentsToday: number;
    collectedToday: number;
    currency: string;
    overdue: number;
    dueToday: number;
    future: number;
    chargeCount: number;
    enrollmentsWithoutCharges: number;
    lastSevenDays: { date: string; amount: number }[];
    recentReceipts: { id: string; number: number; date: string; applied: number }[];
  } | null;
  gate: { expected: number; arrived: number; inside: number; left: number } | null;
  people: number | null;
  teaching: {
    id: string;
    subject: string;
    grade: string;
    section: string | null;
    students: number;
    pendingReviews: number;
    attendanceRecorded: number;
    upcoming: DashboardTask[];
  }[];
  students: {
    id: string;
    name: string;
    code: string;
    canAcademic: boolean;
    canFinance: boolean;
    enrolled: boolean | null;
    pendingTasks: number | null;
    tasks: DashboardTask[];
    balance: number | null;
    charges: number | null;
  }[];
  access: { pendingActivation: number; suspended: number } | null;
}
