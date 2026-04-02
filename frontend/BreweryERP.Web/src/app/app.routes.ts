import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },

  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login.component').then(m => m.LoginComponent)
  },

  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },

  {
    path: 'recipes',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/recipes/recipes.component').then(m => m.RecipesComponent)
  },

  {
    path: 'batches',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/batches/batches.component').then(m => m.BatchesComponent)
  },

  {
    path: 'inventory',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/inventory/inventory.component').then(m => m.InventoryComponent)
  },

  {
    path: 'orders',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/orders/orders.component').then(m => m.OrdersComponent)
  },

  {
    path: 'clients',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/clients/clients.component').then(m => m.ClientsComponent)
  },

  {
    path: 'suppliers',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/suppliers/suppliers.component').then(m => m.SuppliersComponent)
  },

  {
    path: 'staff',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/staff/staff.component').then(m => m.StaffComponent)
  },

  {
    path: 'beer-styles',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/beer-styles/beer-styles.component').then(m => m.BeerStylesComponent)
  },

  { path: '**', redirectTo: 'dashboard' }
];
