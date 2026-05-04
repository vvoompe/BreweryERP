import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { Supplier, SupplyInvoice }   from '../../core/models';

@Component({
  selector: 'app-suppliers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>🏭 Постачальники</h2>
          <p>Управління постачальниками сировини</p>
        </div>
        <button class="btn btn-primary" id="btn-add-supplier" (click)="openAdd()">
          ＋ Додати постачальника
        </button>
      </div>

      <div class="search-bar">
        <input type="text" placeholder="🔍 Пошук постачальника..." [(ngModel)]="searchQuery" (ngModelChange)="applyFilter()" id="search-suppliers">
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
                <th>ЄДРПОУ</th>
                <th>Дії</th>
              </tr>
            </thead>
            <tbody>
              @if (filtered().length === 0) {
                <tr><td colspan="4">
                  <div class="empty-state">
                    <div class="empty-icon">🏭</div>
                    <h3>Постачальників не знайдено</h3>
                  </div>
                </td></tr>
              }
              @for (s of filtered(); track s.supplierId) {
                <tr>
                  <td class="text-muted font-mono">{{ s.supplierId }}</td>
                  <td>
                    <div class="flex-center gap-sm">
                      <div class="supplier-dot"></div>
                      <strong>{{ s.name }}</strong>
                    </div>
                  </td>
                  <td class="font-mono text-muted">{{ s.edrpou ?? '—' }}</td>
                  <td>
                    <div class="row-actions">
                      <button class="btn btn-ghost btn-sm" (click)="viewInvoices(s)" title="Історія постачань">📦 Історія</button>
                      <button class="btn btn-ghost btn-icon btn-sm" (click)="edit(s)">✏️</button>
                      <button class="btn btn-danger btn-icon btn-sm" (click)="remove(s.supplierId)">🗑</button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>

    <!-- Modal додавання/редагування постачальника -->
    @if (showModal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ editing() ? '✏️ Редагувати постачальника' : '➕ Новий постачальник' }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>
          <div class="form-group">
            <label>Назва компанії</label>
            <input type="text" [(ngModel)]="form.name" id="input-suppName" placeholder="ТОВ Солод Плюс">
          </div>
          <div class="form-group">
            <label>ЄДРПОУ (необов'язково)</label>
            <input type="text" [(ngModel)]="form.edrpou" id="input-suppEdrpou" placeholder="12345678" maxlength="8">
          </div>
          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-supplier" (click)="save()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              {{ editing() ? 'Зберегти' : 'Додати' }}
            </button>
          </div>
        </div>
      </div>
    }

    <!-- Модалка: Історія постачань постачальника -->
    @if (showInvoicesModal()) {
      <div class="modal-backdrop" (click)="closeInvoicesModal()">
        <div class="modal modal-lg animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>📦 Історія постачань: {{ selectedSupplier()?.name }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeInvoicesModal()">✕</button>
          </div>

          <div class="modal-body" style="max-height: 60vh; overflow-y: auto;">
            @if (invoicesLoading()) {
              <div class="loading"><div class="spinner"></div> Завантаження...</div>
            } @else if (supplierInvoices().length === 0) {
              <div class="empty-state">
                <div class="empty-icon">📦</div>
                <h3>Постачань не знайдено</h3>
                <p class="text-muted">Цей постачальник ще не надсилав накладних</p>
              </div>
            } @else {
              <table>
                <thead>
                  <tr>
                    <th>№ Документа</th>
                    <th>Дата отримання</th>
                    <th>Позицій</th>
                    <th>Дії</th>
                  </tr>
                </thead>
                <tbody>
                  @for (inv of supplierInvoices(); track inv.invoiceId) {
                    <tr>
                      <td class="font-mono"><strong>{{ inv.docNumber }}</strong></td>
                      <td>{{ inv.receiveDate | date:'dd.MM.yyyy' }}</td>
                      <!-- ВИПРАВЛЕНО: читаємо itemCount (є в списку), а не items.length (якого немає в списку) -->
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
            }
          </div>

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeInvoicesModal()">Закрити</button>
          </div>
        </div>
      </div>
    }

    <!-- Модалка: Деталі конкретної накладної -->
    @if (showDetailsModal()) {
      <div class="modal-backdrop" (click)="closeDetailsModal()">
        <div class="modal modal-lg animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>📋 Деталі накладної: {{ selectedInvoice()?.docNumber }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeDetailsModal()">✕</button>
          </div>

          <div class="modal-body" style="max-height: 60vh; overflow-y: auto;">
            @if (detailsLoading()) {
              <div class="loading"><div class="spinner"></div> Завантаження...</div>
            } @else if (selectedInvoice()) {
              <div style="margin-bottom: 12px; color: var(--text-muted); font-size: 0.85rem;">
                Постачальник: <strong>{{ selectedInvoice()!.supplierName }}</strong> &nbsp;|&nbsp;
                Дата: <strong>{{ selectedInvoice()!.receiveDate | date:'dd.MM.yyyy' }}</strong>
              </div>
              <table>
                <thead>
                  <tr>
                    <th>Інгредієнт</th>
                    <th>Кількість</th>
                    <th>Ціна за од.</th>
                    <th>Термін придатності</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of selectedInvoice()!.items; track item.ingredientId) {
                    <tr>
                      <td><strong>{{ item.ingredientName }}</strong></td>
                      <td>{{ item.quantity }} {{ item.unit ?? '' }}</td>
                      <td>{{ item.unitPrice != null ? (item.unitPrice | number:'1.2-2') + ' грн' : '—' }}</td>
                      <td>{{ item.expirationDate ? (item.expirationDate | date:'dd.MM.yyyy') : '—' }}</td>
                    </tr>
                  }
                </tbody>
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
    .supplier-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--amber-500);
      flex-shrink: 0;
    }
  `]
})
export class SuppliersComponent implements OnInit {
  loading    = signal(true);
  showModal  = signal(false);
  saving     = signal(false);
  editing    = signal(false);
  suppliers  = signal<Supplier[]>([]);
  filtered   = signal<Supplier[]>([]);
  searchQuery = '';
  form: Partial<Supplier> = { name:'', edrpou:'' };
  private editId = 0;

  // Модалка "Історія постачань"
  showInvoicesModal  = signal(false);
  selectedSupplier   = signal<Supplier | null>(null);
  supplierInvoices   = signal<SupplyInvoice[]>([]);
  invoicesLoading    = signal(false);

  // Модалка "Деталі накладної"
  showDetailsModal   = signal(false);
  selectedInvoice    = signal<SupplyInvoice | null>(null);
  detailsLoading     = signal(false);

  constructor(private api: ApiService) {}
  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.getSuppliers().subscribe({
      next: s => { this.suppliers.set(s); this.applyFilter(); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  applyFilter() {
    let list = this.suppliers();
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(s => s.name.toLowerCase().includes(q) || (s.edrpou ?? '').includes(q));
    }
    this.filtered.set(list);
  }

  openAdd() { this.editing.set(false); this.form = { name:'', edrpou:'' }; this.showModal.set(true); }

  edit(s: Supplier) {
    this.editing.set(true); this.editId = s.supplierId;
    this.form = { name: s.name, edrpou: s.edrpou ?? '' };
    this.showModal.set(true);
  }

  save() {
    this.saving.set(true);
    const obs: Observable<any> = this.editing()
      ? this.api.updateSupplier(this.editId, this.form)
      : this.api.createSupplier(this.form);
    obs.subscribe({
      next: () => { this.saving.set(false); this.closeModal(); this.load(); },
      error: () => this.saving.set(false)
    });
  }

  remove(id: number) {
    if (!confirm('Видалити постачальника?')) return;
    this.api.deleteSupplier(id).subscribe(() => this.load());
  }

  closeModal() { this.showModal.set(false); }

  // ── Відкрити список накладних постачальника ────────────────────────────────
  viewInvoices(s: Supplier) {
    this.selectedSupplier.set(s);
    this.showInvoicesModal.set(true);
    this.invoicesLoading.set(true);
    this.api.getSupplierInvoices(s.supplierId).subscribe({
      next: (invoices) => {
        this.supplierInvoices.set(invoices);
        this.invoicesLoading.set(false);
      },
      error: () => this.invoicesLoading.set(false)
    });
  }

  closeInvoicesModal() {
    this.showInvoicesModal.set(false);
    this.selectedSupplier.set(null);
    this.supplierInvoices.set([]);
  }

  // ── Відкрити деталі конкретної накладної ──────────────────────────────────
  viewInvoiceDetails(invoiceId: number) {
    this.showDetailsModal.set(true);
    this.detailsLoading.set(true);
    this.selectedInvoice.set(null);
    this.api.getInvoiceById(invoiceId).subscribe({
      next: (inv) => {
        this.selectedInvoice.set(inv);
        this.detailsLoading.set(false);
      },
      error: () => this.detailsLoading.set(false)
    });
  }

  closeDetailsModal() {
    this.showDetailsModal.set(false);
    this.selectedInvoice.set(null);
  }
}