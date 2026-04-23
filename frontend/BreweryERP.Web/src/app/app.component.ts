import { Component, computed } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService }  from './core/auth.service';

interface NavItem {
  path:  string;
  label: string;
  icon:  string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {

  readonly navItems: NavItem[] = [
    { path: '/dashboard',   label: 'Дашборд',       icon: '📊' },
    { path: '/beer-styles', label: 'Стилі пива',    icon: '🍻' },
    { path: '/recipes',     label: 'Рецепти',        icon: '📋' },
    { path: '/batches',     label: 'Партії',          icon: '🍺' },
    { path: '/inventory',   label: 'Інгредієнти',   icon: '🌾' },
    { path: '/orders',      label: 'Замовлення',     icon: '🛒' },
    { path: '/clients',     label: 'Клієнти',        icon: '👥' },
    { path: '/suppliers',   label: 'Постачальники',  icon: '🏭' },
    { path: '/import',      label: 'Імпорт Excel',   icon: '📥' },
    { path: '/staff',       label: 'Персонал',       icon: '👤' },
  ];

  readonly isShellVisible = computed(() => this.auth.isLoggedIn());
  readonly userName        = this.auth.userName;
  readonly userRole        = this.auth.currentRole;

  constructor(public auth: AuthService, private router: Router) {}

  logout(): void {
    this.auth.logout();
  }
}
