import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'dashboard',
    loadComponent: () => import('./features/analytics/pages/application-dashboard/application-dashboard.component')
      .then((analyticsModule) => analyticsModule.ApplicationDashboardComponent),
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
  { path: '**', redirectTo: '' },
];
