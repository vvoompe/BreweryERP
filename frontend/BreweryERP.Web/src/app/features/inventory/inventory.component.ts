import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { Ingredient, IngredientType } from '../../core/models';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>🌾 Інгредієнти</h2>
          <p>Склад сировини та контроль запасів</p>
        </div>
        <button class="btn btn-primary" id="btn-add-ingredient" (click)="openAdd()">
          ＋ Додати інгредієнт
        </button>
      </div>

      <div class="search-bar">
        <input type="text" placeholder="🔍 Пошук інгредієнту..." [(ngModel)]="searchQuery" (ngModelChange)="applyFilter()" id="search-ingredients">
        <select [(ngModel)]="typeFilter" (ngModelChange)="applyFilter()" id="filter-type" style="width:auto; min-width:130px;">
          <option value="all">Всі типи</option>
          <option value="Malt">Солод</option>
          <option value="Hop">Хміль</option>
          <option value="Yeast">Дріжджі</option>
          <option value="Additive">Добавка</option>
          <option value="Water">Вода</option>
        </select>
        <label class="flex-center" style="gap:6px; cursor:pointer; white-space:nowrap;">
          <input type="checkbox" [(ngModel)]="showLowOnly" (ngModelChange)="applyFilter()" style="width:auto;">
          <span style="font-size:0.85rem; color: var(--status-failed);">⚠️ Тільки низький запас</span>
        </label>
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
                <th>Назва</th>
                <th>Тип</th>
                <th>Запас</th>
                <th>Одиниця</th>
                <th>Дії</th>
              </tr>
            </thead>
            <tbody>
              @if (filtered().length === 0) {
                <tr><td colspan="6">
                  <div class="empty-state">
                    <div class="empty-icon">🌾</div>
                    <h3>Інгредієнтів не знайдено</h3>
                  </div>
                </td></tr>
              }
              @for (i of filtered(); track i.ingredientId) {
                <tr>
                  <td class="text-muted font-mono">{{ i.ingredientId }}</td>
                  <td><strong>{{ i.name }}</strong></td>
                  <td>
                    <span class="badge badge-inactive">{{ typeLabel(i.type) }}</span>
                  </td>
                  <td>
                    <div class="stock-bar-wrap">
                      <div class="stock-bar" [style.width.%]="stockPercent(i.totalStock)" [class.stock-bar--low]="i.totalStock < 10"></div>
                      <span [class.text-danger]="i.totalStock < 10" [class.text-success]="i.totalStock >= 10">
                        {{ i.totalStock }}
                      </span>
                    </div>
                  </td>
                  <td class="text-muted">{{ i.unit }}</td>
                  <td>
                    <div class="row-actions">
                      <button class="btn btn-ghost btn-icon btn-sm" (click)="edit(i)">✏️</button>
                      <button class="btn btn-danger btn-icon btn-sm" (click)="remove(i.ingredientId)">🗑</button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>

    <!-- Modal -->
    @if (showModal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ editing() ? '✏️ Редагувати інгредієнт' : '➕ Новий інгредієнт' }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>

          <div class="form-group">
            <label>Назва</label>
            <input type="text" [(ngModel)]="form.name" id="input-ingName" placeholder="Pilsner Malt">
          </div>
          <div style="display:grid; grid-template-columns:1fr 1fr; gap:12px;">
            <div class="form-group">
              <label>Тип</label>
              <select [(ngModel)]="form.type" id="input-ingType">
                <option value="Malt">Солод (Malt)</option>
                <option value="Hop">Хміль (Hop)</option>
                <option value="Yeast">Дріжджі (Yeast)</option>
                <option value="Additive">Добавка (Additive)</option>
                <option value="Water">Вода (Water)</option>
              </select>
            </div>
            <div class="form-group">
              <label>Одиниця</label>
              <select [(ngModel)]="form.unit" id="input-ingUnit">
                <option value="kg">kg</option>
                <option value="g">g</option>
                <option value="L">L</option>
                <option value="mL">mL</option>
                <option value="pcs">pcs</option>
              </select>
            </div>
          </div>
          <div class="form-group">
            <label>Кількість на складі</label>
            <input type="number" step="0.01" [(ngModel)]="form.totalStock" id="input-stock" placeholder="0.00">
          </div>

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-ingredient" (click)="save()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              {{ editing() ? 'Зберегти' : 'Додати' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .stock-bar-wrap {
      display: flex;
      align-items: center;
      gap: 8px;
      position: relative;
    }
    .stock-bar {
      height: 6px;
      width: 60px;
      background: var(--status-completed);
      border-radius: 3px;
      min-width: 4px;
      max-width: 60px;
      transition: width 0.4s ease;
    }
    .stock-bar--low { background: var(--status-failed); }
  `]
})
export class InventoryComponent implements OnInit {
  loading     = signal(true);
  showModal   = signal(false);
  saving      = signal(false);
  editing     = signal(false);
  ingredients = signal<Ingredient[]>([]);
  filtered    = signal<Ingredient[]>([]);
  searchQuery  = '';
  typeFilter   = 'all';
  showLowOnly  = false;
  form: Partial<Ingredient> = { name:'', type:'Malt', unit:'kg', totalStock: 0 };
  private editId = 0;

  constructor(private api: ApiService) {}
  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.getIngredients().subscribe({ next: i => { this.ingredients.set(i); this.applyFilter(); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  applyFilter() {
    let list = this.ingredients();
    if (this.typeFilter !== 'all') list = list.filter(i => i.type === this.typeFilter);
    if (this.showLowOnly) list = list.filter(i => i.totalStock < 10);
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(i => i.name.toLowerCase().includes(q));
    }
    this.filtered.set(list);
  }

  openAdd() {
    this.editing.set(false);
    this.form = { name:'', type:'Malt', unit:'kg', totalStock: 0 };
    this.showModal.set(true);
  }

  edit(i: Ingredient) {
    this.editing.set(true);
    this.editId = i.ingredientId;
    this.form = { name: i.name, type: i.type, unit: i.unit, totalStock: i.totalStock };
    this.showModal.set(true);
  }

  save() {
    this.saving.set(true);
    const obs: Observable<any> = this.editing() ? this.api.updateIngredient(this.editId, this.form) : this.api.createIngredient(this.form);
    obs.subscribe({ next: () => { this.saving.set(false); this.closeModal(); this.load(); }, error: () => this.saving.set(false) });
  }

  remove(id: number) {
    if (!confirm('Видалити цей інгредієнт?')) return;
    this.api.deleteIngredient(id).subscribe(() => this.load());
  }

  closeModal() { this.showModal.set(false); }

  stockPercent(stock: number): number { return Math.min(100, (stock / 100) * 100); }

  typeLabel(type: IngredientType): string {
    const map: Record<IngredientType,string> = {
      Malt:'🌾 Солод',
      Hop:'🟢 Хміль',
      Yeast:'🔬 Дріжджі',
      Additive:'⚗️ Добавка',
      Water:'💧 Вода'
    };
    return map[type] ?? type;
  }
}
