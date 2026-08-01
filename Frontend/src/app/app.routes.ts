import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/guards/auth.guard';
import { Roles } from './core/models/auth.models';

export const routes: Routes = [
  {
    path: '',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
  { path: 'login', redirectTo: '', pathMatch: 'full' },
  {
    path: 'app',
    loadComponent: () =>
      import('./layout/main-layout/main-layout').then((m) => m.MainLayout),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'projects', pathMatch: 'full' },
      {
        path: 'dashboard',
        canActivate: [roleGuard([Roles.PlanningManager, Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'projects',
        loadComponent: () =>
          import('./features/projects/projects').then((m) => m.Projects),
      },
      {
        path: 'projects/:id',
        loadComponent: () =>
          import('./features/projects/details/sub-project-details').then(
            (m) => m.SubProjectDetails,
          ),
      },
      {
        path: 'plans',
        loadComponent: () =>
          import('./features/plans/plan-list').then((m) => m.PlanList),
      },
      {
        path: 'plans/:id',
        loadComponent: () =>
          import('./features/plans/plan-print').then((m) => m.PlanPrint),
      },
      // ===== الإدارة المالية (قسم مستقل — مراحل الطرح) =====
      {
        path: 'financial',
        loadComponent: () =>
          import('./features/financial/financial-list').then((m) => m.FinancialList),
      },
      {
        path: 'financial/memos',
        loadComponent: () =>
          import('./features/financial/presentation-memos').then((m) => m.PresentationMemos),
      },
      {
        path: 'financial/:id',
        loadComponent: () =>
          import('./features/financial/procurement-workflow').then((m) => m.ProcurementWorkflow),
      },
      {
        path: 'users',
        canActivate: [roleGuard([Roles.PlanningManager, Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/users/users').then((m) => m.Users),
      },
      {
        path: 'contractors',
        loadComponent: () =>
          import('./features/contractors/contractors').then((m) => m.Contractors),
      },
      {
        path: 'agencies',
        loadComponent: () =>
          import('./features/agencies/agencies').then((m) => m.Agencies),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
