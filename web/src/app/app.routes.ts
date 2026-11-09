import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { InstitutionService } from './core/institution';
import { sessionGuard, permissionGuard } from './core/session';
import { Router } from '@angular/router';
import { pendingChangesGuard } from './core/pending-changes';

const previewGuard = () =>
  inject(InstitutionService).current.previewEnabled || inject(Router).createUrlTree(['/']);
export const routes: Routes = [
  {
    path: 'workspace/timetables/edit',
    canActivate: [permissionGuard('academic.read'), permissionGuard('academic.manage')],
    canDeactivate: [pendingChangesGuard],
    loadComponent: () => import('./features/timetable-editor').then((x) => x.TimetableEditor),
  },
  {
    path: 'workspace/timetables',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/timetables').then((x) => x.Timetables),
  },
  {
    path: 'workspace/messages',
    canActivate: [sessionGuard],
    canDeactivate: [pendingChangesGuard],
    loadComponent: () => import('./features/messages').then((x) => x.Messages),
  },
  {
    path: 'workspace/migration',
    canActivate: [
      permissionGuard('people.manage'),
      permissionGuard('enrollment.manage'),
      permissionGuard('accounts.manage'),
    ],
    canDeactivate: [pendingChangesGuard],
    loadComponent: () => import('./features/migration-assistant').then((x) => x.MigrationAssistant),
  },
  {
    path: 'workspace/roles',
    canActivate: [permissionGuard('accounts.manage')],
    loadComponent: () => import('./features/access-roles').then((x) => x.AccessRoles),
  },
  {
    path: 'workspace/gate',
    canActivate: [permissionGuard('attendance.gate')],
    loadComponent: () => import('./features/gate-kiosk').then((x) => x.GateKiosk),
  },
  {
    path: 'workspace/gate/report',
    canActivate: [permissionGuard('attendance.read')],
    loadComponent: () => import('./features/gate-report').then((x) => x.GateReport),
  },
  {
    path: 'workspace/gate/devices',
    canActivate: [permissionGuard('attendance.manage')],
    loadComponent: () => import('./features/gate-devices').then((x) => x.GateDevices),
  },
  { path: '', loadComponent: () => import('./features/welcome').then((x) => x.Welcome) },
  {
    path: 'workspace',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/workspace').then((x) => x.Workspace),
  },
  {
    path: 'workspace/security',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/security-settings').then((x) => x.SecuritySettings),
  },
  {
    path: 'workspace/announcements',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/announcements').then((x) => x.Announcements),
  },
  {
    path: 'workspace/access',
    canActivate: [permissionGuard('accounts.manage')],
    loadComponent: () => import('./features/access-management').then((x) => x.AccessManagement),
  },
  {
    path: 'workspace/audit',
    canActivate: [permissionGuard('audit.read')],
    loadComponent: () => import('./features/security-audit').then((x) => x.SecurityAudit),
  },
  {
    path: 'workspace/students',
    canActivate: [permissionGuard('people.read')],
    data: { directory: 'student' },
    loadComponent: () => import('./features/people-directory').then((x) => x.PeopleDirectory),
  },
  {
    path: 'workspace/parents',
    canActivate: [permissionGuard('people.read')],
    data: { directory: 'guardian' },
    loadComponent: () => import('./features/people-directory').then((x) => x.PeopleDirectory),
  },
  {
    path: 'workspace/new-family',
    canActivate: [
      permissionGuard('people.read'),
      permissionGuard('people.manage'),
      permissionGuard('academic.read'),
      permissionGuard('enrollment.manage'),
    ],
    canDeactivate: [pendingChangesGuard],
    loadComponent: () => import('./features/family-registration').then((x) => x.FamilyRegistration),
  },
  {
    path: 'workspace/teachers',
    canActivate: [permissionGuard('people.read')],
    data: { directory: 'staff' },
    loadComponent: () => import('./features/people-directory').then((x) => x.PeopleDirectory),
  },
  {
    path: 'workspace/people',
    canActivate: [permissionGuard('people.read')],
    loadComponent: () => import('./features/people-directory').then((x) => x.PeopleDirectory),
  },
  {
    path: 'workspace/people/:id',
    canActivate: [permissionGuard('people.read')],
    loadComponent: () => import('./features/person-detail').then((x) => x.PersonDetail),
  },
  {
    path: 'workspace/academic',
    canActivate: [permissionGuard('academic.read')],
    loadComponent: () => import('./features/academic-structure').then((x) => x.AcademicStructure),
  },
  {
    path: 'workspace/enrollment',
    canActivate: [permissionGuard('academic.read')],
    loadComponent: () => import('./features/enrollment-roster').then((x) => x.EnrollmentRoster),
  },
  {
    path: 'workspace/courses',
    canActivate: [permissionGuard('academic.read')],
    loadComponent: () => import('./features/courses').then((x) => x.Courses),
  },
  {
    path: 'workspace/courses/:id/attendance',
    canActivate: [sessionGuard],
    canDeactivate: [pendingChangesGuard],
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    loadComponent: () => import('./features/course-attendance').then((x) => x.CourseAttendance),
  },
  {
    path: 'workspace/courses/:id/assignments',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/course-assignments').then((x) => x.CourseAssignments),
  },
  {
    path: 'workspace/courses/:id/grades',
    canActivate: [sessionGuard],
    canDeactivate: [pendingChangesGuard],
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    loadComponent: () => import('./features/course-grades').then((x) => x.CourseGrades),
  },
  {
    path: 'workspace/grades',
    canActivate: [permissionGuard('academic.read')],
    loadComponent: () => import('./features/grade-publications').then((x) => x.GradePublications),
  },
  {
    path: 'workspace/courses/:id',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/course-roster').then((x) => x.CourseRoster),
  },
  {
    path: 'workspace/finance',
    canActivate: [permissionGuard('finance.read')],
    loadComponent: () => import('./features/finance-cashier').then((x) => x.FinanceCashier),
  },
  {
    path: 'workspace/inventory',
    canActivate: [permissionGuard('inventory.read')],
    canDeactivate: [pendingChangesGuard],
    loadComponent: () => import('./features/inventory').then((x) => x.Inventory),
  },
  {
    path: 'workspace/finance/setup',
    canActivate: [permissionGuard('finance.read')],
    loadComponent: () => import('./features/finance-setup').then((x) => x.FinanceSetup),
  },
  {
    path: 'workspace/finance/clearance',
    canActivate: [permissionGuard('finance.read')],
    loadComponent: () => import('./features/finance-clearance').then((x) => x.FinanceClearance),
  },
  {
    path: 'workspace/finance/receipts/:id',
    canActivate: [permissionGuard('finance.read')],
    loadComponent: () => import('./features/receipt-view').then((x) => x.ReceiptView),
  },
  {
    path: 'workspace/my/students/:id',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/my-student').then((x) => x.MyStudent),
  },
  {
    path: 'workspace/my/students/:studentId/receipts/:id',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/receipt-view').then((x) => x.ReceiptView),
  },
  {
    path: 'preview',
    canActivate: [previewGuard],
    loadComponent: () => import('./features/preview-dashboard').then((x) => x.PreviewDashboard),
  },
  {
    path: 'preview/students',
    canActivate: [previewGuard],
    loadComponent: () => import('./features/preview-students').then((x) => x.PreviewStudents),
  },
  { path: '**', redirectTo: '' },
];
