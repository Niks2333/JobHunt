import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './features/auth/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/auth-page/auth-page.component').then(
        (module) => module.AuthPageComponent,
      ),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    data: { registering: true },
    loadComponent: () =>
      import('./features/auth/pages/auth-page/auth-page.component').then(
        (module) => module.AuthPageComponent,
      ),
  },
  {
    path: '',
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    loadComponent: () =>
      import('./layout/application-layout/application-layout.component').then(
        (module) => module.ApplicationLayoutComponent,
      ),
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/analytics/pages/application-dashboard/application-dashboard.component').then(
            (analyticsModule) => analyticsModule.ApplicationDashboardComponent,
          ),
      },
      {
        path: 'applications/:jobApplicationId',
        loadComponent: () =>
          import('./features/job-applications/pages/job-application-details/job-application-details.component').then(
            (applicationModule) => applicationModule.JobApplicationDetailsComponent,
          ),
      },
      {
        path: '',
        loadComponent: () =>
          import('./features/job-applications/pages/job-application-list/job-application-list.component').then(
            (applicationModule) => applicationModule.JobApplicationListComponent,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
