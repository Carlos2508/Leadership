import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { InstitutionService } from './core/institution';
import { sessionGuard, permissionGuard } from './core/session';
import { Router } from '@angular/router';

const previewGuard = () =>
  inject(InstitutionService).current.previewEnabled || inject(Router).createUrlTree(['/']);
export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/welcome').then((x) => x.Welcome) },
  {
    path: 'workspace',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/workspace').then((x) => x.Workspace),
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
