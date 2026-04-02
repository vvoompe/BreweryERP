import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { AuthService }               from '../../core/auth.service';
import { StaffDto }                  from '../../core/models';

@Component({
  selector: 'app-staff',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>👤 Персонал</h2>
          <p>Управління акаунтами співробітників броварні</p>
        </div>
        <!-- Тільки Admin може реєструвати нових -->
        @if (auth.isAdmin()) {
          <button class="btn btn-primary" id="btn-add-staff" (click)="openAdd()">
            ＋ Новий співробітник
          </button>
        }
      </div>

      <!-- Тільки Admin бачить цю сторінку -->
      @if (!auth.isAdmin()) {
        <div class="empty-state">
          <div class="empty-icon">🔒</div>
          <h3>Доступ обмежено</h3>
          <p>Тільки адміністратор може переглядати список персоналу</p>
        </div>
      }

      @if (auth.isAdmin()) {
        <div class="search-bar">
          <input type="text" placeholder="🔍 Пошук за ім'ям або email..." [(ngModel)]="searchQuery" (ngModelChange)="applyFilter()" id="search-staff">
          <select [(ngModel)]="roleFilter" (ngModelChange)="applyFilter()" id="filter-role" style="width:auto; min-width:140px;">
            <option value="all">Всі ролі</option>
            <option value="Admin">Admin</option>
            <option value="Brewer">Brewer</option>
            <option value="Warehouse">Warehouse</option>
          </select>
        </div>

        @if (loading()) {
          <div class="loading"><div class="spinner"></div> Завантаження...</div>
        }

        @if (!loading()) {
          <div class="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Співробітник</th>
                  <th>Email</th>
                  <th>Роль</th>
                  <th>Статус</th>
                  <th>Дії</th>
                </tr>
              </thead>
              <tbody>
                @if (filtered().length === 0) {
                  <tr><td colspan="5">
                    <div class="empty-state">
                      <div class="empty-icon">👤</div>
                      <h3>Нікого не знайдено</h3>
                    </div>
                  </td></tr>
                }
                @for (s of filtered(); track s.id) {
                  <tr [class.row-self]="s.email === auth.user()?.email">
                    <td>
                      <div class="staff-cell">
                        <div class="staff-avatar" [ngClass]="'avatar-' + s.role.toLowerCase()">
                          {{ s.fullName[0] | uppercase }}
                        </div>
                        <div>
                          <strong>{{ s.fullName }}</strong>
                          @if (s.email === auth.user()?.email) {
                            <span class="badge badge-active" style="margin-left:6px; font-size:0.6rem;">Ви</span>
                          }
                        </div>
                      </div>
                    </td>
                    <td class="text-muted">{{ s.email }}</td>
                    <td>
                      <!-- Inline role change select (тільки не для себе) -->
                      @if (s.email !== auth.user()?.email) {
                        <select
                          class="role-select"
                          [ngModel]="s.role"
                          (ngModelChange)="changeRole(s, $event)"
                          [id]="'role-select-' + s.id">
                          <option value="Admin">Admin</option>
                          <option value="Brewer">Brewer</option>
                          <option value="Warehouse">Warehouse</option>
                        </select>
                      } @else {
                        <span class="badge" [ngClass]="roleBadge(s.role)">{{ s.role }}</span>
                      }
                    </td>
                    <td>
                      <span class="badge" [class.badge-active]="!s.isLocked" [class.badge-failed]="s.isLocked">
                        {{ s.isLocked ? '🔒 Заблокований' : '✅ Активний' }}
                      </span>
                    </td>
                    <td>
                      <div class="row-actions" [style.opacity]="s.email !== auth.user()?.email ? null : '0'">
                        @if (s.email !== auth.user()?.email) {
                          <button
                            class="btn btn-ghost btn-sm"
                            [id]="'btn-lock-' + s.id"
                            (click)="toggleLock(s)"
                            [title]="s.isLocked ? 'Розблокувати' : 'Заблокувати'">
                            {{ s.isLocked ? '🔓' : '🔒' }}
                          </button>
                          <button
                            class="btn btn-danger btn-icon btn-sm"
                            [id]="'btn-del-' + s.id"
                            (click)="remove(s)"
                            title="Видалити">
                            🗑
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </div>

    <!-- Modal: реєстрація нового співробітника -->
    @if (showModal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>➕ Новий співробітник</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>

          @if (regError()) {
            <div class="error-msg animate-in" style="margin-bottom:12px;">
              ⚠️ {{ regError() }}
            </div>
          }

          <div class="form-group">
            <label for="reg-fullname">Повне ім'я</label>
            <input type="text" [(ngModel)]="regForm.fullName" id="reg-fullname" placeholder="Іван Пивовар">
          </div>
          <div class="form-group">
            <label for="reg-email">Email</label>
            <input type="email" [(ngModel)]="regForm.email" id="reg-email" placeholder="ivan@brewery.ua">
          </div>
          <div class="form-group">
            <label for="reg-password">Пароль (мін. 6 символів)</label>
            <input type="password" [(ngModel)]="regForm.password" id="reg-password" placeholder="••••••••">
          </div>
          <div class="form-group">
            <label for="reg-role">Роль</label>
            <select [(ngModel)]="regForm.role" id="reg-role">
              <option value="Admin">Admin — повний доступ</option>
              <option value="Brewer">Brewer — рецепти та партії</option>
              <option value="Warehouse">Warehouse — склад та закупівлі</option>
            </select>
          </div>

          <div class="role-hint">
            <div class="role-hint-item">
              <span class="badge badge-active">Admin</span>
              <span>Повний доступ до всіх функцій</span>
            </div>
            <div class="role-hint-item">
              <span class="badge badge-brewing">Brewer</span>
              <span>Керування рецептами, партіями</span>
            </div>
            <div class="role-hint-item">
              <span class="badge badge-reserved">Warehouse</span>
              <span>Склад, інгредієнти, накладні</span>
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-staff" (click)="register()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              ✨ Зареєструвати
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .staff-cell {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .staff-avatar {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 0.875rem;
      flex-shrink: 0;
    }
    .avatar-admin    { background: rgba(212,134,11,0.25); color: var(--amber-400); }
    .avatar-brewer   { background: rgba(59,143,212,0.2);  color: var(--status-brewing); }
    .avatar-warehouse{ background: rgba(155,89,182,0.2);  color: var(--status-fermenting); }

    .role-select {
      background: var(--bg-base);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-sm);
      color: var(--text-primary);
      font-family: inherit;
      font-size: 0.8rem;
      padding: 4px 8px;
      outline: none;
      cursor: pointer;
      transition: border-color var(--transition);
    }
    .role-select:focus { border-color: var(--amber-500); }

    .badge-failed { background: rgba(231,76,60,0.15); color: var(--status-failed); }

    .row-self { background: rgba(212,134,11,0.04); }

    .error-msg {
      background: rgba(231,76,60,0.1);
      border: 1px solid rgba(231,76,60,0.3);
      border-radius: var(--radius-sm);
      color: var(--status-failed);
      padding: 10px 14px;
      font-size: 0.85rem;
    }

    .role-hint {
      background: rgba(212,134,11,0.05);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-sm);
      padding: 12px;
      margin-bottom: var(--space-md);
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .role-hint-item {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 0.8rem;
      color: var(--text-secondary);
    }
  `]
})
export class StaffComponent implements OnInit {
  loading   = signal(true);
  showModal = signal(false);
  saving    = signal(false);
  regError  = signal('');
  staff     = signal<StaffDto[]>([]);
  filtered  = signal<StaffDto[]>([]);
  searchQuery = '';
  roleFilter  = 'all';

  regForm = { fullName: '', email: '', password: '', role: 'Brewer' };

  constructor(public auth: AuthService, private api: ApiService) {}

  ngOnInit() {
    if (this.auth.isAdmin()) this.load();
    else this.loading.set(false);
  }

  load() {
    this.loading.set(true);
    this.api.getStaff().subscribe({
      next: s => { this.staff.set(s); this.applyFilter(); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  applyFilter() {
    let list = this.staff();
    if (this.roleFilter !== 'all') list = list.filter(s => s.role === this.roleFilter);
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(s => s.fullName.toLowerCase().includes(q) || s.email.toLowerCase().includes(q));
    }
    this.filtered.set(list);
  }

  openAdd() {
    this.regForm = { fullName: '', email: '', password: '', role: 'Brewer' };
    this.regError.set('');
    this.showModal.set(true);
  }

  register() {
    this.saving.set(true);
    this.regError.set('');
    this.api.registerStaff(this.regForm).subscribe({
      next: () => { this.saving.set(false); this.closeModal(); this.load(); },
      error: err => {
        this.saving.set(false);
        this.regError.set(err.error?.message ?? 'Помилка реєстрації');
      }
    });
  }

  changeRole(staff: StaffDto, newRole: string) {
    this.api.updateStaffRole(staff.id, newRole).subscribe({
      next: () => {
        // Оновити локально без повного reload
        this.staff.update(list =>
          list.map(s => s.id === staff.id ? { ...s, role: newRole } : s)
        );
        this.applyFilter();
      }
    });
  }

  toggleLock(staff: StaffDto) {
    const action = staff.isLocked ? 'розблокувати' : 'заблокувати';
    if (!confirm(`${action.charAt(0).toUpperCase() + action.slice(1)} ${staff.fullName}?`)) return;
    this.api.toggleStaffLock(staff.id).subscribe({
      next: () => {
        this.staff.update(list =>
          list.map(s => s.id === staff.id ? { ...s, isLocked: !s.isLocked } : s)
        );
        this.applyFilter();
      }
    });
  }

  remove(staff: StaffDto) {
    if (!confirm(`Видалити акаунт ${staff.fullName}? Цю дію не можна скасувати.`)) return;
    this.api.deleteStaff(staff.id).subscribe({ next: () => this.load() });
  }

  closeModal() { this.showModal.set(false); }

  roleBadge(role: string): string {
    return { Admin: 'badge-active', Brewer: 'badge-brewing', Warehouse: 'badge-reserved' }[role] ?? 'badge-inactive';
  }
}
