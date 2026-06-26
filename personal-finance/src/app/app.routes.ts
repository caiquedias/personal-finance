import { Routes } from '@angular/router';
import { authGuard, adminGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  // Rota pública
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/components/login/login.component')
        .then(m => m.LoginComponent)
  },

  // Rotas protegidas — dentro do shell com sidebar + header
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./shared/components/page-shell/page-shell.component')
        .then(m => m.PageShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/dashboard/components/dashboard/dashboard.component')
            .then(m => m.DashboardComponent)
      },
      {
        path: 'periods',
        loadComponent: () =>
          import('./features/periods/components/periods/periods.component')
            .then(m => m.PeriodsComponent)
      },
      {
        path: 'periods/:id',
        loadComponent: () =>
          import('./features/periods/components/period-detail/period-detail.component')
            .then(m => m.PeriodDetailComponent)
      },
      {
        path: 'expenses',
        loadComponent: () =>
          import('./features/expenses/components/expenses/expenses.component')
            .then(m => m.ExpensesComponent)
      },
      {
        path: 'incomes',
        loadComponent: () =>
          import('./features/incomes/components/incomes/incomes.component')
            .then(m => m.IncomesComponent)
      },
      {
        path: 'config',
        loadComponent: () =>
          import('./features/config/components/config/config.component')
            .then(m => m.ConfigComponent)
      },
      {
        path: 'import',
        loadComponent: () =>
          import('./features/import/components/import/import.component')
            .then(m => m.ImportComponent)
      },
      {
        path: 'purge',
        loadComponent: () =>
          import('./features/purge/components/purge/purge.component')
            .then(m => m.PurgeComponent)
      },
      {
        path: 'admin/users',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/config/components/admin-users/admin-users.component')
            .then(m => m.AdminUsersComponent)
      },
    ]
  },

  // Fallback
  { path: '**', redirectTo: '' }
];
