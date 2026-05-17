import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'documents', pathMatch: 'full' },
  {
    path: 'documents',
    loadComponent: () =>
      import('./documents/documents.component').then(m => m.DocumentsComponent),
  },
  {
    path: 'profile',
    loadComponent: () =>
      import('./profile/profile.component').then(m => m.ProfileComponent),
  },
];
