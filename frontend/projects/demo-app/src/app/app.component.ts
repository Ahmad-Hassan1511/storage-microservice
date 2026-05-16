import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <nav class="nav">
      <span class="nav-brand">Storage Demo</span>
      <a routerLink="/documents" routerLinkActive="active">Documents</a>
      <a routerLink="/profile" routerLinkActive="active">Profile</a>
    </nav>
    <main class="main">
      <router-outlet />
    </main>
  `,
  styles: [`
    .nav {
      display: flex; align-items: center; gap: 24px;
      padding: 12px 24px; background: #1976d2; color: white;
    }
    .nav-brand { font-weight: 700; font-size: 18px; margin-right: auto; }
    .nav a { color: rgba(255,255,255,0.85); text-decoration: none; font-size: 14px; }
    .nav a.active, .nav a:hover { color: white; }
    .main { padding: 24px; }
  `],
})
export class AppComponent {
  title = 'demo-app';
}
