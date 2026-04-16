import { Injectable, signal, computed } from '@angular/core';
import { HttpClient }                   from '@angular/common/http';
import { Router }                       from '@angular/router';
import { tap }                          from 'rxjs/operators';
import { Observable }                   from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest } from './models';

const TOKEN_KEY = 'brewery_token';
const USER_KEY  = 'brewery_user';
const BASE      = 'http://localhost:5000/api';

@Injectable({ providedIn: 'root' })
export class AuthService {

  private _user = signal<AuthResponse | null>(this._loadUser());

  readonly user        = this._user.asReadonly();
  readonly isLoggedIn  = computed(() => !!this._user());
  // Підтримує як новий формат (role) так і старий (roles[0])
  readonly currentRole = computed(() => {
    const u = this._user();
    return u?.role ?? u?.roles?.[0] ?? '';
  });
  readonly isAdmin     = computed(() => this.currentRole() === 'Admin');
  readonly userName    = computed(() => this._user()?.fullName ?? this._user()?.email ?? '');

  constructor(private http: HttpClient, private router: Router) {}

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${BASE}/auth/login`, body).pipe(
      tap(res => this._saveSession(res))
    );
  }

  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${BASE}/auth/register`, body).pipe(
      tap(res => this._saveSession(res))
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._user.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private _saveSession(res: AuthResponse): void {
    // Нормалізуємо — гарантуємо наявність поля role
    const normalized: AuthResponse = {
      ...res,
      role: res.role ?? res.roles?.[0] ?? '',
    };
    localStorage.setItem(TOKEN_KEY, normalized.token);
    localStorage.setItem(USER_KEY, JSON.stringify(normalized));
    this._user.set(normalized);
  }

  private _loadUser(): AuthResponse | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as AuthResponse;

      // Перевірка мінімальних полів — якщо формат застарілий, чистимо
      if (!parsed?.token || (!parsed.role && !parsed.roles?.length)) {
        localStorage.removeItem(USER_KEY);
        localStorage.removeItem(TOKEN_KEY);
        return null;
      }

      // ★ Перевірка терміну дії токена
      if (parsed.expiresAt && new Date(parsed.expiresAt) <= new Date()) {
        console.info('[AuthService] Token expired — clearing session');
        localStorage.removeItem(USER_KEY);
        localStorage.removeItem(TOKEN_KEY);
        return null;
      }

      // Нормалізуємо role якщо стара версія
      if (!parsed.role && parsed.roles?.length) {
        parsed.role = parsed.roles[0];
      }
      return parsed;
    } catch {
      localStorage.removeItem(USER_KEY);
      localStorage.removeItem(TOKEN_KEY);
      return null;
    }
  }
}
