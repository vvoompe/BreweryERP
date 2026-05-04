import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { ApiService }                from '../../core/api.service';
import { SalesOrder, Client, ProductSku, OrderStatus, OrderItemDto } from '../../core/models';

interface OrderItemForm { skuId: number; quantity: number; price: number; packaging: string; }

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>🛒 Замовлення</h2>
          <p>Замовлення клієнтів та управління продажами</p>
        </div>
        <button class="btn btn-primary" id="btn-add-order" (click)="openAdd()">
          ＋ Нове замовлення
        </button>
      </div>

      <div class="search-bar">
        <input type="text" placeholder="🔍 Пошук клієнта..." [(ngModel)]="searchQuery"
               (ngModelChange)="applyFilter()" id="search-orders">
        <select [(ngModel)]="statusFilter" (ngModelChange)="applyFilter()"
                id="filter-order-status" style="width:auto; min-width:140px;">
          <option value="all">Всі статуси</option>
          <option value="New">Нове</option>
          <option value="Reserved">Зарезервовано</option>
          <option value="Shipped">Відправлено</option>
          <option value="Paid">Оплачено</option>
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
                <th>Клієнт</th>
                <th>Дата</th>
                <th>Статус</th>
                <th>Позицій</th>
                <th>Сума</th>
                <th>Маржа</th>
                <th>Дії</th>
              </tr>
            </thead>
            <tbody>
              @if (filtered().length === 0) {
                <tr><td colspan="7">
                  <div class="empty-state">
                    <div class="empty-icon">🛒</div>
                    <h3>Замовлень не знайдено</h3>
                  </div>
                </td></tr>
              }
              @for (o of filtered(); track o.orderId) {
                <tr class="order-row" (click)="openDetail(o)" style="cursor:pointer;">
                  <td class="text-muted font-mono">{{ o.orderId }}</td>
                  <td><strong>{{ o.clientName ?? 'Клієнт #' + o.clientId }}</strong></td>
                  <td>{{ o.orderDate | date:'dd.MM.yyyy' }}</td>
                  <td>
                    <span class="badge" [ngClass]="orderBadge(o.status)">{{ orderLabel(o.status) }}</span>
                  </td>
                  <td><span class="badge badge-inactive">{{ (o.itemCount ?? o.items?.length ?? 0) }} SKU</span></td>
                  <td class="font-mono">{{ (o.totalAmount ?? 0) | number:'1.2-2' }} ₴</td>
                  <td class="font-mono" [ngClass]="{'text-danger': (o.profitMargin ?? 0) <= 0, 'text-success': (o.profitMargin ?? 0) > 0}">
                    {{ (o.profitMargin ?? 0) | number:'1.2-2' }} ₴
                  </td>
                  <td>
                    <div class="row-actions" (click)="$event.stopPropagation()">
                      <button class="btn btn-ghost btn-icon btn-sm"
                              title="Переглянути деталі" (click)="openDetail(o)">👁️</button>
                      <button class="btn btn-ghost btn-icon btn-sm"
                              title="Змінити статус" (click)="openStatus(o)">✏️</button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>

    <!-- ─── Модалка: деталі замовлення ───────────────────────────── -->
    @if (showDetailModal()) {
      <div class="modal-backdrop" (click)="showDetailModal.set(false)">
        <div class="modal animate-in" style="max-width:640px" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div>
              <h3>📋 Замовлення #{{ detailOrder()?.orderId }}</h3>
              <p style="margin:0; font-size:0.85rem; color:var(--text-muted);">
                {{ detailOrder()?.clientName }} &nbsp;·&nbsp;
                {{ detailOrder()?.orderDate | date:'dd.MM.yyyy' }} &nbsp;·&nbsp;
                <span class="badge" [ngClass]="orderBadge(detailOrder()?.status ?? 'New')">
                  {{ orderLabel(detailOrder()?.status ?? 'New') }}
                </span>
              </p>
            </div>
            <button class="btn btn-ghost btn-icon" (click)="showDetailModal.set(false)">✕</button>
          </div>

          @if (detailLoading()) {
            <div class="loading" style="padding:2rem"><div class="spinner"></div> Завантаження позицій...</div>
          } @else {
            @if ((detailOrder()?.items ?? []).length === 0) {
              <div class="empty-state" style="padding:2rem">
                <div class="empty-icon">📦</div>
                <p>Позиції не знайдено</p>
              </div>
            } @else {
              <div style="padding:0 1.5rem;">
                <table style="width:100%">
                  <thead>
                    <tr>
                      <th>SKU #</th>
                      <th>Пиво</th>
                      <th>Пакування</th>
                      <th style="text-align:right">К-сть</th>
                      <th style="text-align:right">Ціна</th>
                      <th style="text-align:right">Сума</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (item of detailOrder()?.items ?? []; track item.skuId) {
                      <tr>
                        <td class="font-mono text-muted">SKU#{{ item.skuId }}</td>
                        <td><strong>{{ item.beerName }}</strong></td>
                        <td>
                          <span class="badge badge-inactive">{{ packLabel(item.packagingType ?? '') }}</span>
                        </td>
                        <td class="font-mono" style="text-align:right">{{ item.quantity }}</td>
                        <td class="font-mono" style="text-align:right">{{ item.priceAtMoment | number:'1.2-2' }} ₴</td>
                        <td class="font-mono" style="text-align:right; font-weight:600;">
                          {{ item.lineTotal | number:'1.2-2' }} ₴
                        </td>
                      </tr>
                    }
                  </tbody>
                  <tfoot>
                    <tr style="border-top:2px solid var(--border);">
                      <td colspan="4" style="text-align:right; font-weight:600; padding-top:0.75rem;">Разом сума:</td>
                      <td class="font-mono" style="text-align:right; font-weight:700; color:var(--accent); padding-top:0.75rem;">
                        {{ detailOrder()?.totalAmount | number:'1.2-2' }} ₴
                      </td>
                    </tr>
                    <tr>
                      <td colspan="4" style="text-align:right; font-weight:600; font-size: 0.85rem; color:var(--text-muted);">Собівартість:</td>
                      <td class="font-mono" style="text-align:right; font-weight:500; font-size: 0.85rem; color:var(--text-muted);">
                        {{ detailOrder()?.totalCost | number:'1.2-2' }} ₴
                      </td>
                    </tr>
                    <tr>
                      <td colspan="4" style="text-align:right; font-weight:600; font-size: 0.85rem; color:var(--status-completed);">Прибуток (Маржа):</td>
                      <td class="font-mono" style="text-align:right; font-weight:600; font-size: 0.85rem; color:var(--status-completed);">
                        {{ detailOrder()?.profitMargin | number:'1.2-2' }} ₴ ({{ detailOrder()?.profitMarginPercent }}%)
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            }

            <div class="modal-footer" style="margin-top:1rem;">
              <button class="btn btn-ghost" (click)="showDetailModal.set(false)">Закрити</button>
              <button class="btn btn-primary" (click)="openStatusFromDetail()">
                ✏️ Змінити статус
              </button>
            </div>
          }
        </div>
      </div>
    }

    <!-- ─── Модалка: нове замовлення ─────────────────────────────── -->
    @if (showModal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal animate-in" style="max-width:560px" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>➕ Нове замовлення</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>

          <div class="form-group">
            <label>Клієнт</label>
            <select [(ngModel)]="form.clientId" id="input-ordClientId">
              @for (c of clients(); track c.clientId) {
                <option [value]="c.clientId">{{ c.name }}</option>
              }
            </select>
          </div>

          <div class="form-group">
            <label>Позиції замовлення</label>
            @for (item of formItems; let i = $index; track i) {
              <div style="display:flex; gap:8px; align-items:center; margin-bottom:8px;">
                <select [(ngModel)]="item.skuId" (ngModelChange)="onSkuChange(item)"
                        style="flex:1;" [id]="'sku-sel-'+i">
                  <option [value]="0" disabled>-- оберіть SKU --</option>
                  @for (s of skus(); track s.skuId) {
                    <option [value]="s.skuId">
                      SKU#{{ s.skuId }} | {{ s.batchInfo }} | {{ packLabel(s.packagingType) }} | {{ s.price }} ₴ | Залишок: {{ s.quantityInStock }}
                    </option>
                  }
                </select>
                <input type="number" [(ngModel)]="item.quantity" min="1"
                       [max]="skuStock(item.skuId)" style="width:80px;"
                       [id]="'sku-qty-'+i" placeholder="К-сть">
                <button class="btn btn-danger btn-icon btn-sm" (click)="removeItem(i)">✕</button>
              </div>
            }
            <button class="btn btn-ghost btn-sm" (click)="addItem()"
                    id="btn-add-item" style="margin-top:4px;">＋ Додати позицію</button>
          </div>

          @if (formError()) {
            <div style="color:var(--danger); font-size:0.85rem; margin-bottom:8px;">
              ⚠️ {{ formError() }}
            </div>
          }

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-order" (click)="save()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              Створити
            </button>
          </div>
        </div>
      </div>
    }

    <!-- ─── Модалка: зміна статусу ────────────────────────────────── -->
    @if (showStatusModal()) {
      <div class="modal-backdrop" (click)="showStatusModal.set(false)">
        <div class="modal animate-in" style="max-width:380px" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>🔄 Змінити статус</h3>
            <button class="btn btn-ghost btn-icon" (click)="showStatusModal.set(false)">✕</button>
          </div>
          <div class="form-group">
            <label>Поточний: <strong>{{ orderLabel(statusOrder()?.status ?? 'New') }}</strong></label>
          </div>
          <div class="form-group">
            <label>Новий статус</label>
            <select [(ngModel)]="newStatus" id="input-new-status">
              <option value="Reserved">Зарезервовано</option>
              <option value="Shipped">Відправлено</option>
              <option value="Paid">Оплачено</option>
            </select>
          </div>
          @if (formError()) {
            <div style="color:var(--danger); font-size:0.85rem; padding: 0 1.5rem 0.5rem;">
              ⚠️ {{ formError() }}
            </div>
          }
          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="showStatusModal.set(false)">Скасувати</button>
            <button class="btn btn-primary" (click)="saveStatus()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              Зберегти
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class OrdersComponent implements OnInit {
  // ── Signals ──────────────────────────────────────────────
  loading         = signal(true);
  showModal       = signal(false);
  showStatusModal = signal(false);
  showDetailModal = signal(false);
  detailLoading   = signal(false);
  saving          = signal(false);
  formError       = signal('');
  orders          = signal<SalesOrder[]>([]);
  clients         = signal<Client[]>([]);
  skus            = signal<ProductSku[]>([]);
  filtered        = signal<SalesOrder[]>([]);
  statusOrder     = signal<SalesOrder | null>(null);
  detailOrder     = signal<SalesOrder | null>(null);

  // ── State ─────────────────────────────────────────────────
  searchQuery      = '';
  statusFilter     = 'all';
  newStatus: OrderStatus = 'Reserved';
  form: { clientId: number } = { clientId: 0 };
  formItems: OrderItemForm[] = [];

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getClients().subscribe(c => {
      this.clients.set(c);
      if (c.length) this.form.clientId = c[0].clientId;
    });
    this.api.getSkus().subscribe(s => this.skus.set(s));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.getOrders().subscribe({
      next: o  => { this.orders.set(o); this.applyFilter(); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  applyFilter() {
    let list = this.orders();
    if (this.statusFilter !== 'all') list = list.filter(o => o.status === this.statusFilter);
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(o => (o.clientName ?? '').toLowerCase().includes(q));
    }
    this.filtered.set(list);
  }

  // ── View Detail ───────────────────────────────────────────
  openDetail(o: SalesOrder) {
    // Set basic info immediately so modal appears right away
    this.detailOrder.set({ ...o });
    this.showDetailModal.set(true);
    this.detailLoading.set(true);

    this.api.getOrder(o.orderId).subscribe({
      next: full => { this.detailOrder.set(full); this.detailLoading.set(false); },
      error: ()  => this.detailLoading.set(false)
    });
  }

  openStatusFromDetail() {
    const o = this.detailOrder();
    if (!o) return;
    this.showDetailModal.set(false);
    this.openStatus(o);
  }

  // ── Create Order ──────────────────────────────────────────
  openAdd() {
    this.form      = { clientId: this.clients()[0]?.clientId ?? 0 };
    this.formItems = [{ skuId: 0, quantity: 1, price: 0, packaging: '' }];
    this.formError.set('');
    this.showModal.set(true);
  }

  addItem()             { this.formItems.push({ skuId: 0, quantity: 1, price: 0, packaging: '' }); }
  removeItem(i: number) { this.formItems.splice(i, 1); }

  onSkuChange(item: OrderItemForm) {
    const s = this.skus().find(x => x.skuId === Number(item.skuId));
    if (s) { item.price = s.price; item.packaging = s.packagingType; }
  }

  skuStock(skuId: number): number {
    return this.skus().find(s => s.skuId === Number(skuId))?.quantityInStock ?? 9999;
  }

  save() {
    this.formError.set('');
    const validItems = this.formItems.filter(i => Number(i.skuId) > 0 && i.quantity > 0);
    if (!this.form.clientId)      { this.formError.set('Оберіть клієнта.'); return; }
    if (validItems.length === 0)  { this.formError.set('Додайте хоча б одну позицію з вибраним SKU.'); return; }

    this.saving.set(true);
    const payload = {
      clientId: Number(this.form.clientId),
      items: validItems.map(i => ({ skuId: Number(i.skuId), quantity: Number(i.quantity) }))
    };
    this.api.createOrder(payload).subscribe({
      next: () => { this.saving.set(false); this.closeModal(); this.load(); },
      error: (err: any) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'Помилка при створенні замовлення.');
      }
    });
  }

  // ── Change Status ─────────────────────────────────────────
  openStatus(o: SalesOrder) {
    this.statusOrder.set(o);
    const next: Record<OrderStatus, OrderStatus> =
      { New: 'Reserved', Reserved: 'Shipped', Shipped: 'Paid', Paid: 'Paid' };
    this.newStatus = next[o.status];
    this.showStatusModal.set(true);
  }

  saveStatus() {
    const o = this.statusOrder();
    if (!o) return;
    this.saving.set(true);
    this.formError.set('');
    this.api.updateOrderStatus(o.orderId, this.newStatus).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.showStatusModal.set(false);
        // Оновлюємо замовлення в локальному списку без повного reload
        this.orders.update(list => list.map(x => x.orderId === updated.orderId ? { ...x, status: updated.status } : x));
        this.applyFilter();
      },
      error: (err: any) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'Помилка при зміні статусу замовлення.');
      }
    });
  }

  closeModal() { this.showModal.set(false); }

  // ── Helpers ───────────────────────────────────────────────
  packLabel(p: string): string {
    const m: Record<string, string> = { Keg_30L: 'Кег 30L', Keg_50L: 'Кег 50L', Bottle_0_5L: 'Пляшка 0.5L' };
    return m[p] ?? p;
  }

  orderBadge(s: OrderStatus | string): string {
    const m: Record<string, string> = { New: 'badge-new', Reserved: 'badge-reserved', Shipped: 'badge-shipped', Paid: 'badge-paid' };
    return m[s] ?? 'badge-inactive';
  }

  orderLabel(s: OrderStatus | string): string {
    const m: Record<string, string> = { New: 'Нове', Reserved: 'Зарезервовано', Shipped: 'Відправлено', Paid: 'Оплачено' };
    return m[s] ?? s;
  }
}
