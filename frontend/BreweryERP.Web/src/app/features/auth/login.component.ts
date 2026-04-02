import { Component, OnInit, signal } from '@angular/core';
import { FormsModule }               from '@angular/forms';
import { CommonModule }              from '@angular/common';
import { Router }                    from '@angular/router';
import { AuthService }               from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, CommonModule],
  template: `
    <div class="login-page">
      <div class="bg-orbs">
        <div class="orb orb-1"></div>
        <div class="orb orb-2"></div>
        <div class="orb orb-3"></div>
      </div>

      <div class="login-card animate-in">
        <!-- Logo -->
        <div class="login-header">
          <div class="login-logo">🍺</div>
          <h1>Brewery ERP</h1>
          <p>Система управління крафтовою броварнею</p>
        </div>

        <!-- Tabs -->
        <div class="auth-tabs">
          <button class="tab-btn" [class.tab-btn--active]="mode() === 'login'"
            (click)="mode.set('login')" id="tab-login">Вхід</button>
          <button class="tab-btn" [class.tab-btn--active]="mode() === 'register'"
            (click)="mode.set('register')" id="tab-register">Реєстрація</button>
        </div>

        <!-- Error banner -->
        @if (error()) {
          <div class="error-msg animate-in">
            <span>⚠️</span>
            <div>
              <strong>Помилка</strong>
              <p>{{ error() }}</p>
            </div>
          </div>
        }

        <!-- ─── LOGIN ─────────────────────────── -->
        @if (mode() === 'login') {
          <form class="auth-form animate-in" (ngSubmit)="onLogin()" #lf="ngForm">
            <div class="form-group">
              <label for="login-email">Email</label>
              <input id="login-email" type="email" name="email"
                [(ngModel)]="loginEmail" placeholder="admin@brewery.ua"
                required autocomplete="email">
            </div>
            <div class="form-group">
              <label for="login-password">Пароль</label>
              <input id="login-password" type="password" name="password"
                [(ngModel)]="loginPassword" placeholder="••••••••"
                required autocomplete="current-password">
            </div>

            <button class="btn btn-primary w-full" id="btn-login-submit"
              type="submit" [disabled]="loading()">
              @if (loading()) { <span class="spinner"></span> Вхід... }
              @else { 🔑 Увійти }
            </button>
          </form>
        }

        <!-- ─── REGISTER ──────────────────────── -->
        @if (mode() === 'register') {
          <form class="auth-form animate-in" (ngSubmit)="onRegister()" #rf="ngForm">
            <div class="form-group">
              <label for="reg-name">Повне ім'я</label>
              <input id="reg-name" type="text" name="fullName"
                [(ngModel)]="regData.fullName" placeholder="Іван Варило" required>
            </div>
            <div class="form-group">
              <label for="reg-email">Email</label>
              <input id="reg-email" type="email" name="email"
                [(ngModel)]="regData.email" placeholder="ivan@brewery.ua" required>
            </div>
            <div class="form-group">
              <label for="reg-password">Пароль <span class="text-muted">(мін. 6 символів)</span></label>
              <input id="reg-password" type="password" name="password"
                [(ngModel)]="regData.password" placeholder="••••••••" required minlength="6">
            </div>

            <!-- Role picker — cards -->
            <div class="form-group">
              <label>Роль</label>
              <div class="role-cards">
                @for (r of roles; track r.value) {
                  <div class="role-card" [class.role-card--active]="regData.role === r.value"
                    (click)="regData.role = r.value" [id]="'role-card-' + r.value">
                    <span class="role-card-icon">{{ r.icon }}</span>
                    <div>
                      <strong>{{ r.value }}</strong>
                      <small>{{ r.desc }}</small>
                    </div>
                  </div>
                }
              </div>
            </div>

            <button class="btn btn-primary w-full" id="btn-register-submit"
              type="submit" [disabled]="loading()">
              @if (loading()) { <span class="spinner"></span> Реєстрація... }
              @else { ✨ Зареєструватись як {{ regData.role }}  }
            </button>
          </form>
        }

        <p class="login-hint">Brewery ERP v1.0 · Craft Brewery Management</p>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: 100vh;
      display: flex; align-items: center; justify-content: center;
      background: var(--bg-base);
      position: relative; overflow: hidden;
      padding: var(--space-lg);
    }
    .bg-orbs { position: absolute; inset: 0; pointer-events: none; }
    .orb { position: absolute; border-radius: 50%; filter: blur(80px); opacity: 0.3; }
    .orb-1 { width:400px;height:400px; background:radial-gradient(circle,var(--amber-700),transparent); top:-100px;left:-100px; }
    .orb-2 { width:300px;height:300px; background:radial-gradient(circle,var(--amber-600),transparent); bottom:-80px;right:-60px; }
    .orb-3 { width:180px;height:180px; background:radial-gradient(circle,rgba(212,134,11,0.5),transparent); top:50%;left:50%;transform:translate(-50%,-50%); }

    .login-card {
      position: relative; z-index: 1;
      width: 100%; max-width: 440px;
      background: var(--bg-card);
      border: 1px solid var(--border-active);
      border-radius: var(--radius-xl);
      padding: var(--space-xl);
      box-shadow: var(--shadow-lg), var(--shadow-amber);
    }

    .login-header { text-align: center; margin-bottom: var(--space-xl); }
    .login-logo {
      font-size: 3.5rem; line-height: 1;
      margin-bottom: var(--space-md);
      filter: drop-shadow(0 0 16px rgba(212,134,11,0.8));
      animation: float 3s ease-in-out infinite;
    }
    @keyframes float { 0%,100% { transform: translateY(0); } 50% { transform: translateY(-8px); } }
    .login-header h1 { font-size: 1.75rem; font-weight: 700; color: var(--amber-400); letter-spacing: -0.02em; }
    .login-header p { color: var(--text-muted); font-size: 0.8rem; margin-top: 4px; }

    .auth-tabs {
      display: flex; gap: 4px;
      background: var(--bg-base);
      padding: 4px; border-radius: var(--radius-md);
      margin-bottom: var(--space-lg);
    }
    .tab-btn {
      flex: 1; padding: 8px; border: none; background: transparent;
      color: var(--text-muted); font-family: inherit; font-size: 0.875rem; font-weight: 500;
      border-radius: var(--radius-sm); cursor: pointer; transition: all var(--transition);
    }
    .tab-btn--active { background: var(--amber-500); color: var(--text-inverse); font-weight: 600; }

    .auth-form { display: flex; flex-direction: column; }

    .error-msg {
      display: flex; gap: 10px; align-items: flex-start;
      background: rgba(231,76,60,0.1);
      border: 1px solid rgba(231,76,60,0.3);
      border-left: 4px solid var(--status-failed);
      border-radius: var(--radius-sm);
      padding: 12px 14px;
      font-size: 0.85rem;
      margin-bottom: var(--space-md);
      color: var(--text-primary);
    }
    .error-msg span { font-size: 1.2rem; flex-shrink: 0; }
    .error-msg strong { color: var(--status-failed); display: block; margin-bottom: 2px; }
    .error-msg p { color: var(--text-secondary); margin: 0; }

    /* Role cards */
    .role-cards { display: flex; flex-direction: column; gap: 8px; }
    .role-card {
      display: flex; align-items: center; gap: 12px;
      padding: 10px 14px;
      background: var(--bg-base);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-md);
      cursor: pointer;
      transition: all var(--transition);
    }
    .role-card:hover { border-color: var(--border-active); }
    .role-card--active {
      border-color: var(--amber-500);
      background: rgba(212,134,11,0.08);
      box-shadow: 0 0 0 2px rgba(212,134,11,0.2);
    }
    .role-card-icon { font-size: 1.4rem; width: 30px; text-align: center; }
    .role-card strong { display: block; font-size: 0.875rem; color: var(--text-primary); }
    .role-card small { font-size: 0.75rem; color: var(--text-muted); }

    .api-hint {
      font-size: 0.72rem; color: var(--text-muted);
      background: var(--bg-base);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-sm);
      padding: 6px 10px;
      margin-bottom: var(--space-md);
    }
    .api-hint code { color: var(--amber-400); }

    .login-hint {
      text-align: center; color: var(--text-muted);
      font-size: 0.72rem; margin-top: var(--space-lg);
    }
  `]
})
export class LoginComponent implements OnInit {
  mode    = signal<'login' | 'register'>('login');
  loading = signal(false);
  error   = signal('');

  loginEmail    = '';
  loginPassword = '';
  regData       = { fullName: '', email: '', password: '', role: 'Admin' };

  roles = [
    { value: 'Admin',     icon: '👑', desc: 'Повний доступ до всіх функцій' },
    { value: 'Brewer',    icon: '🍺', desc: 'Рецепти, партії, виробництво' },
    { value: 'Warehouse', icon: '📦', desc: 'Склад, інгредієнти, накладні' },
  ];

  constructor(private auth: AuthService, private router: Router) {}

  ngOnInit(): void {
    // Якщо вже залогінений — redirect
    if (this.auth.isLoggedIn()) {
      this.router.navigate(['/dashboard']);
    }
  }

  onLogin(): void {
    this.error.set('');
    if (!this.loginEmail || !this.loginPassword) {
      this.error.set('Введіть email та пароль.');
      return;
    }
    this.loading.set(true);
    this.auth.login({ email: this.loginEmail, password: this.loginPassword }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: err => {
        this.loading.set(false);
        if (err.status === 0) {
          this.error.set('Неможливо підключитися до backend сервера. Переконайтесь що він запущений командою dotnet run.');
        } else if (err.status === 401) {
          this.error.set('Невірний email або пароль.');
        } else {
          this.error.set(err.error?.message ?? `Помилка ${err.status}: ${err.statusText}`);
        }
      }
    });
  }

  onRegister(): void {
    this.error.set('');
    if (!this.regData.fullName || !this.regData.email || !this.regData.password) {
      this.error.set("Заповніть всі поля.");
      return;
    }
    if (this.regData.password.length < 6) {
      this.error.set("Пароль має бути не менше 6 символів.");
      return;
    }
    this.loading.set(true);
    this.auth.register(this.regData).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: err => {
        this.loading.set(false);
        if (err.status === 0) {
          this.error.set('Неможливо підключитися до backend сервера. Запустіть .NET backend командою dotnet run.');
        } else {
          this.error.set(err.error?.message ?? `Помилка ${err.status}: ${err.statusText}`);
        }
      }
    });
  }
}
