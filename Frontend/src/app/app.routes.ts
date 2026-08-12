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
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/account-recovery').then((m) => m.AccountRecovery),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/account-recovery').then((m) => m.AccountRecovery),
  },
  {
    path: 'app',
    loadComponent: () =>
      import('./layout/main-layout/main-layout').then((m) => m.MainLayout),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'projects', pathMatch: 'full' },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/profile/profile').then((m) => m.Profile),
      },
      {
        path: 'dashboard',
        canActivate: [roleGuard([Roles.PlanningManager, Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'reports',
        canActivate: [roleGuard([Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/reports/reports').then((m) => m.Reports),
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
        canActivate: [roleGuard([Roles.PlanningEmployee, Roles.PlanningManager, Roles.FinancialEmployee, Roles.FinancialManager, Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/financial/financial-list').then((m) => m.FinancialList),
      },
      {
        path: 'financial/memos',
        canActivate: [roleGuard([Roles.PlanningEmployee, Roles.PlanningManager, Roles.FinancialEmployee, Roles.FinancialManager, Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/financial/presentation-memos').then((m) => m.PresentationMemos),
      },
      {
        path: 'financial/:id',
        canActivate: [roleGuard([Roles.PlanningEmployee, Roles.PlanningManager, Roles.FinancialEmployee, Roles.FinancialManager, Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/financial/procurement-workflow').then((m) => m.ProcurementWorkflow),
      },
      {
        path: 'follow-up',
        loadComponent: () =>
          import('./features/follow-up/follow-up-list').then((m) => m.FollowUpList),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings').then((m) => m.Settings),
        children: [
          {
            path: '',
            loadComponent: () => import('./features/settings/settings-index').then((m) => m.SettingsIndex),
          },
          {
            path: 'main-programs',
            data: { tab: 'mainProgram' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'sub-programs',
            data: { tab: 'subProgram' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'governorates',
            data: { tab: 'governorate' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'markaz',
            data: { tab: 'markaz' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'villages',
            data: { tab: 'village' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'priorities',
            data: { tab: 'priority' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'statuses',
            data: { tab: 'status' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'component-types',
            data: { tab: 'componentType' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'project-levels',
            data: { tab: 'projectLevel' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'accounting-units',
            data: { tab: 'accountingUnit' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'contract-types',
            data: { tab: 'contractType' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'units',
            data: { tab: 'unit' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'financial-years',
            loadComponent: () =>
              import('./features/settings/financial-years-settings').then((m) => m.FinancialYearsSettings),
          },
          {
            path: 'users',
            canActivate: [roleGuard([Roles.PlanningManager, Roles.SuperAdmin])],
            loadComponent: () =>
              import('./features/users/users').then((m) => m.Users),
          },
          {
            path: 'plan-approval-notifications',
            canActivate: [roleGuard([Roles.SuperAdmin])],
            loadComponent: () =>
              import('./features/settings/plan-approval-notifications').then((m) => m.PlanApprovalNotifications),
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
          {
            path: 'measurements',
            loadComponent: () =>
              import('./features/measurements/measurements').then((m) => m.Measurements),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
