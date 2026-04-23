import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { Recipe, BeerStyle }         from '../../core/models';

@Component({
  selector: 'app-recipes',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>📋 Рецепти</h2>
          <p>Управління рецептурами та версіонування</p>
        </div>
        <button class="btn btn-primary" id="btn-add-recipe" (click)="openAdd()">
          ＋ Новий рецепт
        </button>
      </div>

      <!-- Search & Filter -->
      <div class="search-bar">
        <input
          id="search-recipes"
          type="text"
          placeholder="🔍 Пошук рецепту..."
          [(ngModel)]="searchQuery"
          (ngModelChange)="applyFilter()">
        <select [(ngModel)]="filterActive" (ngModelChange)="applyFilter()" id="filter-active" style="width:auto; min-width:140px;">
          <option value="all">Всі версії</option>
          <option value="active">Активні</option>
          <option value="inactive">Архівні</option>
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
                <th>#</th>
                <th>Назва версії</th>
                <th>Стиль пива</th>
                <th>Інгредієнти</th>
                <th>Статус</th>
                <th>Дії</th>
              </tr>
            </thead>
            <tbody>
              @if (filtered().length === 0) {
                <tr>
                  <td colspan="6">
                    <div class="empty-state">
                      <div class="empty-icon">📋</div>
                      <h3>Немає рецептів</h3>
                      <p>Додайте перший рецепт для початку</p>
                    </div>
                  </td>
                </tr>
              }
              @for (r of filtered(); track r.recipeId) {
                <tr>
                  <td class="text-muted font-mono">{{ r.recipeId }}</td>
                  <td>
                    <strong>{{ r.versionName }}</strong>
                  </td>
                  <td>{{ r.styleName ?? 'Стиль #' + r.styleId }}</td>
                  <td>
                    <span class="badge badge-inactive">{{ r.items ? r.items.length : $any(r).itemCount }} інгр.</span>
                  </td>
                  <td>
                    <span class="badge" [class.badge-active]="r.isActive" [class.badge-inactive]="!r.isActive">
                      {{ r.isActive ? 'Активний' : 'Архів' }}
                    </span>
                  </td>
                  <td>
                    <div class="row-actions">
                      <button class="btn btn-ghost btn-icon btn-sm" (click)="edit(r)" title="Редагувати">✏️</button>
                      <button class="btn btn-danger btn-icon btn-sm" (click)="remove(r.recipeId)" title="Видалити">🗑</button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>

    <!-- Add / Edit Modal -->
    @if (showModal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ editing() ? '✏️ Редагувати рецепт' : '➕ Новий рецепт' }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>

          <div class="form-group">
            <label>Назва версії</label>
            <input type="text" [(ngModel)]="form.versionName" placeholder="IPA v2.1" id="input-versionName">
          </div>
          <div class="form-group">
            <label>Стиль пива</label>
            <select [(ngModel)]="form.styleId" id="input-styleId">
              @for (s of styles(); track s.styleId) {
                <option [value]="s.styleId">{{ s.name }}</option>
              }
            </select>
          </div>
          <div class="form-group">
            <label>
              <input type="checkbox" [(ngModel)]="form.isActive" style="width:auto; margin-right:6px;">
              Активна версія
            </label>
          </div>

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-recipe" (click)="save()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              {{ editing() ? 'Зберегти' : 'Створити' }}
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class RecipesComponent implements OnInit {
  loading    = signal(true);
  showModal  = signal(false);
  saving     = signal(false);
  editing    = signal(false);
  recipes    = signal<Recipe[]>([]);
  styles     = signal<BeerStyle[]>([]);
  filtered   = signal<Recipe[]>([]);
  searchQuery = '';
  filterActive = 'all';
  form: Partial<Recipe> & { isActive: boolean } = { versionName:'', styleId: 0, isActive: true };
  private editId = 0;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getStyles().subscribe(s => this.styles.set(s));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.getRecipes().subscribe({
      next: r => { this.recipes.set(r); this.applyFilter(); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  applyFilter() {
    let list = this.recipes();
    if (this.filterActive === 'active')   list = list.filter(r => r.isActive);
    if (this.filterActive === 'inactive') list = list.filter(r => !r.isActive);
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(r => r.versionName.toLowerCase().includes(q));
    }
    this.filtered.set(list);
  }

  openAdd() {
    this.editing.set(false);
    this.form = { versionName:'', styleId: this.styles()[0]?.styleId ?? 0, isActive: true };
    this.showModal.set(true);
  }

  edit(r: Recipe) {
    this.editing.set(true);
    this.editId = r.recipeId;
    this.form = { versionName: r.versionName, styleId: r.styleId, isActive: r.isActive };
    this.showModal.set(true);
  }

  save() {
    this.saving.set(true);
    const obs: Observable<any> = this.editing()
      ? this.api.updateRecipe(this.editId, this.form)
      : this.api.createRecipe(this.form);
    obs.subscribe({ next: () => { this.saving.set(false); this.closeModal(); this.load(); }, error: () => this.saving.set(false) });
  }

  remove(id: number) {
    if (!confirm('Видалити цей рецепт?')) return;
    this.api.deleteRecipe(id).subscribe(() => this.load());
  }

  closeModal() { this.showModal.set(false); }
}
