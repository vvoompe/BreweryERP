import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { RouterLink }                from '@angular/router';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { Ingredient, IngredientType, SupplyInvoice } from '../../core/models';

type Tab = 'ingredients' | 'invoices';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>🌾 Склад</h2>
          <p>Інгредієнти та накладні постачань</p>
        </div>
        <div style="display:flex; gap:10px;">
          <a routerLink="/import" class="btn btn-ghost" id="btn-goto-import">
            📥 Імпортувати з Excel
          </a>
          @if (activeTab() === 'ingredients') {
            <button class="btn btn-primary" id="btn-add-ingredient" (click)="openAdd()">
              ＋ Додати інгредієнт
            </button>
          }
        </div>
      </div>

      <!-- Вкладки -->
      <div class="tabs" style="display:flex; gap:4px; margin-bottom:20px; border-bottom: 2px solid var(--border); padding-bottom:0;">
        <button
          class="tab-btn"
          [class.tab-btn--active]="activeTab() === 'ingredients'"
          (click)="setTab('ingredients')"
          style="padding: 8px 20px; border:none; background:none; cursor:pointer; font-size:0.95rem; border-bottom: 3px solid transparent; margin-bottom:-2px; transition: all 0.2s;"
          [style.border-bottom-color]="activeTab() === 'ingredients' ? 'var(--amber-500)' : 'transparent'"
          [style.color]="activeTab() === 'ingredients' ? 'var(--amber-500)' : 'var(--text-muted)'">
          🌾 Інгредієнти
          @if (ingredients().length) {
            <span class="badge" style="margin-left:6px; background: var(--surface-2); color: var(--text-muted); font-size:0.75rem; padding:1px 7px; border-radius:10px;">
              {{ ingredients().length }}
            </span>
          }
        </button>
        <button
          class="tab-btn"
          [class.tab-btn--active]="activeTab() === 'invoices'"
          (click)="setTab('invoices')"
          style="padding: 8px 20px; border:none; background:none; cursor:pointer; font-size:0.95rem; border-bottom: 3px solid transparent; margin-bottom:-2px; transition: all 0.2s;"
          [style.border-bottom-color]="activeTab() === 'invoices' ? 'var(--amber-500)' : 'transparent'"
          [style.color]="activeTab() === 'invoices' ? 'var(--amber-500)' : 'var(--text-muted)'">
          📦 Накладні
          @if (invoices().length) {
            <span class="badge" style="margin-left:6px; background: var(--surface-2); color: var(--text-muted); font-size:0.75rem; padding:1px 7px; border-radius:10px;">
              {{ invoices().length }}
            </span>
          }
        </button>
      </div>

      <!-- ════════════════ ВКЛАДКА: ІНГРЕДІЄНТИ ════════════════ -->
      @if (activeTab() === 'ingredients') {
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
      }

      <!-- ════════════════ ВКЛАДКА: НАКЛАДНІ ════════════════ -->
      @if (activeTab() === 'invoices') {
        <div class="search-bar">
          <input type="text" placeholder="🔍 Пошук за номером або постачальником..." [(ngModel)]="invoiceSearch" (ngModelChange)="applyInvoiceFilter()" id="search-invoices">
        </div>

        @if (invoicesLoading()) {
          <div class="loading"><div class="spinner"></div> Завантаження накладних...</div>
        }

        @if (!invoicesLoading()) {
          <div class="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Постачальник</th>
                  <th>№ Документа</th>
                  <th>Дата отримання</th>
                  <th>Позицій</th>
                  <th>Дії</th>
                </tr>
              </thead>
              <tbody>
                @if (filteredInvoices().length === 0) {
                  <tr><td colspan="6">
                    <div class="empty-state">
                      <div class="empty-icon">📦</div>
                      <h3>Накладних не знайдено</h3>
                      <p class="text-muted">Імпортуйте накладну через "📥 Імпортувати з Excel"</p>
                    </div>
                  </td></tr>
                }
                @for (inv of filteredInvoices(); track inv.invoiceId) {
                  <tr>
                    <td class="text-muted font-mono">{{ inv.invoiceId }}</td>
                    <td><strong>{{ inv.supplierName }}</strong></td>
                    <td class="font-mono">{{ inv.docNumber }}</td>
                    <td>{{ inv.receiveDate | date:'dd.MM.yyyy' }}</td>
                    <td>
                      <span class="badge badge-inactive">
                        {{ inv.itemCount ?? inv.items.length }} шт.
                      </span>
                    </td>
                    <td>
                      <button class="btn btn-ghost btn-sm" (click)="viewInvoiceDetails(inv.invoiceId)">
                        🔍 Деталі
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </div>

    <!-- Modal: Додати/Редагувати інгредієнт -->
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

    <!-- Modal: Деталі накладної -->
    @if (showDetailsModal()) {
      <div class="modal-backdrop" (click)="closeDetailsModal()">
        <div class="modal modal-lg animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>📋 Накладна: {{ selectedInvoice()?.docNumber }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeDetailsModal()">✕</button>
          </div>

          <div class="modal-body" style="max-height: 60vh; overflow-y: auto;">
            @if (detailsLoading()) {
              <div class="loading"><div class="spinner"></div> Завантаження...</div>
            } @else if (selectedInvoice()) {
              <div style="margin-bottom: 14px; padding: 10px 14px; background: var(--surface-2); border-radius: 8px; font-size: 0.88rem; color: var(--text-muted); display:flex; gap:20px; flex-wrap:wrap;">
                <span>🏭 Постачальник: <strong style="color:var(--text);">{{ selectedInvoice()!.supplierName }}</strong></span>
                <span>📅 Дата: <strong style="color:var(--text);">{{ selectedInvoice()!.receiveDate | date:'dd.MM.yyyy' }}</strong></span>
                <span>📦 Позицій: <strong style="color:var(--text);">{{ selectedInvoice()!.items.length }}</strong></span>
              </div>
              <table>
                <thead>
                  <tr>
                    <th>Інгредієнт</th>
                    <th>Кількість</th>
                    <th>Ціна за од.</th>
                    <th>Сума</th>
                    <th>Термін придатності</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of selectedInvoice()!.items; track item.ingredientId) {
                    <tr>
                      <td><strong>{{ item.ingredientName }}</strong></td>
                      <td>{{ item.quantity }} {{ item.unit ?? '' }}</td>
                      <td>{{ item.unitPrice != null ? (item.unitPrice | number:'1.2-2') + ' грн' : '—' }}</td>
                      <td>
                        @if (item.unitPrice != null) {
                          <span style="color: var(--status-completed); font-weight:600;">
                            {{ (item.quantity * item.unitPrice) | number:'1.2-2' }} грн
                          </span>
                        } @else { <span class="text-muted">—</span> }
                      </td>
                      <td>{{ item.expirationDate ? (item.expirationDate | date:'dd.MM.yyyy') : '—' }}</td>
                    </tr>
                  }
                </tbody>
                <!-- Підсумок -->
                @if (invoiceTotal(selectedInvoice()!) > 0) {
                  <tfoot>
                    <tr style="border-top: 2px solid var(--border);">
                      <td colspan="3" style="text-align:right; font-weight:600; padding-top:8px;">Загальна сума:</td>
                      <td style="font-weight:700; color: var(--status-completed); padding-top:8px;">
                        {{ invoiceTotal(selectedInvoice()!) | number:'1.2-2' }} грн
                      </td>
                      <td></td>
                    </tr>
                  </tfoot>
                }
              </table>
            }
          </div>

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeDetailsModal()">Закрити</button>
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
  // ── Стан вкладок ────────────────────────────────────────────────────────────
  activeTab = signal<Tab>('ingredients');

  // ── Інгредієнти ─────────────────────────────────────────────────────────────
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

  // ── Накладні ─────────────────────────────────────────────────────────────────
  invoicesLoading  = signal(false);
  invoices         = signal<SupplyInvoice[]>([]);
  filteredInvoices = signal<SupplyInvoice[]>([]);
  invoiceSearch    = '';

  // ── Деталі накладної (модалка) ───────────────────────────────────────────────
  showDetailsModal = signal(false);
  selectedInvoice  = signal<SupplyInvoice | null>(null);
  detailsLoading   = signal(false);

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadIngredients();
    this.loadInvoices();
  }

  // ── Перемикання вкладок ──────────────────────────────────────────────────────
  setTab(tab: Tab) { this.activeTab.set(tab); }

  // ── Інгредієнти ─────────────────────────────────────────────────────────────
  loadIngredients() {
    this.loading.set(true);
    this.api.getIngredients().subscribe({
      next: i => { this.ingredients.set(i); this.applyFilter(); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
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
    const obs: Observable<any> = this.editing()
      ? this.api.updateIngredient(this.editId, this.form)
      : this.api.createIngredient(this.form);
    obs.subscribe({
      next: () => { this.saving.set(false); this.closeModal(); this.loadIngredients(); },
      error: () => this.saving.set(false)
    });
  }

  remove(id: number) {
    if (!confirm('Видалити цей інгредієнт?')) return;
    this.api.deleteIngredient(id).subscribe(() => this.loadIngredients());
  }

  closeModal() { this.showModal.set(false); }

  stockPercent(stock: number): number { return Math.min(100, (stock / 100) * 100); }

  typeLabel(type: IngredientType): string {
    const map: Record<IngredientType, string> = {
      Malt: '🌾 Солод',
      Hop: '🟢 Хміль',
      Yeast: '🔬 Дріжджі',
      Additive: '⚗️ Добавка',
      Water: '💧 Вода'
    };
    return map[type] ?? type;
  }

  // ── Накладні ─────────────────────────────────────────────────────────────────
  loadInvoices() {
    this.invoicesLoading.set(true);
    this.api.getInvoices().subscribe({
      next: list => { this.invoices.set(list); this.applyInvoiceFilter(); this.invoicesLoading.set(false); },
      error: () => this.invoicesLoading.set(false)
    });
  }

  applyInvoiceFilter() {
    let list = this.invoices();
    if (this.invoiceSearch.trim()) {
      const q = this.invoiceSearch.toLowerCase();
      list = list.filter(inv =>
        inv.docNumber.toLowerCase().includes(q) ||
        (inv.supplierName ?? '').toLowerCase().includes(q)
      );
    }
    this.filteredInvoices.set(list);
  }

  viewInvoiceDetails(invoiceId: number) {
    this.showDetailsModal.set(true);
    this.detailsLoading.set(true);
    this.selectedInvoice.set(null);
    this.api.getInvoiceById(invoiceId).subscribe({
      next: inv => { this.selectedInvoice.set(inv); this.detailsLoading.set(false); },
      error: () => this.detailsLoading.set(false)
    });
  }

  closeDetailsModal() {
    this.showDetailsModal.set(false);
    this.selectedInvoice.set(null);
  }

  invoiceTotal(inv: SupplyInvoice): number {
    return inv.items.reduce((sum, item) => sum + (item.unitPrice ?? 0) * item.quantity, 0);
  }
}