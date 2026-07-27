import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards/auth.guard';
import { Roles } from './core/models/auth.models';

export const routes: Routes = [
  {
    path: '',
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
        canActivate: [roleGuard([Roles.PlanningManager])],
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
        path: 'plans/:id',
        loadComponent: () =>
          import('./features/plans/plan-print').then((m) => m.PlanPrint),
      },
      {
        path: 'users',
        canActivate: [roleGuard([Roles.PlanningManager])],
        loadComponent: () =>
          import('./features/users/users').then((m) => m.Users),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
