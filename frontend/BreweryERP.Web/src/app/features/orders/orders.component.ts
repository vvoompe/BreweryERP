import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { SalesOrder, Client, OrderStatus } from '../../core/models';

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
        <input type="text" placeholder="🔍 Пошук клієнта..." [(ngModel)]="searchQuery" (ngModelChange)="applyFilter()" id="search-orders">
        <select [(ngModel)]="statusFilter" (ngModelChange)="applyFilter()" id="filter-order-status" style="width:auto; min-width:140px;">
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
                <th>Дії</th>
              </tr>
            </thead>
            <tbody>
              @if (filtered().length === 0) {
                <tr><td colspan="6">
                  <div class="empty-state">
                    <div class="empty-icon">🛒</div>
                    <h3>Замовлень не знайдено</h3>
                  </div>
                </td></tr>
              }
              @for (o of filtered(); track o.orderId) {
                <tr>
                  <td class="text-muted font-mono">{{ o.orderId }}</td>
                  <td><strong>{{ o.clientName ?? 'Клієнт #' + o.clientId }}</strong></td>
                  <td>{{ o.orderDate | date:'dd.MM.yyyy' }}</td>
                  <td>
                    <span class="badge" [ngClass]="orderBadge(o.status)">{{ orderLabel(o.status) }}</span>
                  </td>
                  <td>
                    <span class="badge badge-inactive">{{ o.items.length }} SKU</span>
                  </td>
                  <td>
                    <div class="row-actions">
                      <button class="btn btn-ghost btn-icon btn-sm" (click)="edit(o)">✏️</button>
                      <button class="btn btn-danger btn-icon btn-sm" (click)="remove(o.orderId)">🗑</button>
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
            <h3>{{ editing() ? '✏️ Редагувати замовлення' : '➕ Нове замовлення' }}</h3>
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
            <label>Статус</label>
            <select [(ngModel)]="form.status" id="input-ordStatus">
              <option value="New">Нове</option>
              <option value="Reserved">Зарезервовано</option>
              <option value="Shipped">Відправлено</option>
              <option value="Paid">Оплачено</option>
            </select>
          </div>
          <div class="form-group">
            <label>Дата замовлення</label>
            <input type="date" [(ngModel)]="form.orderDate" id="input-orderDate">
          </div>

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-order" (click)="save()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              {{ editing() ? 'Зберегти' : 'Створити' }}
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class OrdersComponent implements OnInit {
  loading     = signal(true);
  showModal   = signal(false);
  saving      = signal(false);
  editing     = signal(false);
  orders      = signal<SalesOrder[]>([]);
  clients     = signal<Client[]>([]);
  filtered    = signal<SalesOrder[]>([]);
  searchQuery  = '';
  statusFilter = 'all';
  form: Partial<SalesOrder> = { clientId: 0, status: 'New', orderDate: new Date().toISOString().split('T')[0] };
  private editId = 0;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getClients().subscribe(c => { this.clients.set(c); if (c.length) this.form.clientId = c[0].clientId; });
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.getOrders().subscribe({ next: o => { this.orders.set(o); this.applyFilter(); this.loading.set(false); }, error: () => this.loading.set(false) });
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

  openAdd() {
    this.editing.set(false);
    this.form = { clientId: this.clients()[0]?.clientId ?? 0, status: 'New', orderDate: new Date().toISOString().split('T')[0] };
    this.showModal.set(true);
  }

  edit(o: SalesOrder) {
    this.editing.set(true);
    this.editId = o.orderId;
    this.form = { clientId: o.clientId, status: o.status, orderDate: o.orderDate.split('T')[0] };
    this.showModal.set(true);
  }

  save() {
    this.saving.set(true);
    const obs: Observable<any> = this.editing() ? this.api.updateOrder(this.editId, this.form) : this.api.createOrder(this.form);
    obs.subscribe({ next: () => { this.saving.set(false); this.closeModal(); this.load(); }, error: () => this.saving.set(false) });
  }

  remove(id: number) {
    if (!confirm('Видалити це замовлення?')) return;
    this.api.deleteOrder(id).subscribe(() => this.load());
  }

  closeModal() { this.showModal.set(false); }

  orderBadge(s: OrderStatus): string {
    return { New:'badge-new', Reserved:'badge-reserved', Shipped:'badge-shipped', Paid:'badge-paid' }[s] ?? 'badge-inactive';
  }

  orderLabel(s: OrderStatus): string {
    return { New:'Нове', Reserved:'Зарезервовано', Shipped:'Відправлено', Paid:'Оплачено' }[s] ?? s;
  }
}
