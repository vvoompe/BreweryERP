import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { Client }                    from '../../core/models';

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>👥 Клієнти</h2>
          <p>База клієнтів броварні</p>
        </div>
        <button class="btn btn-primary" id="btn-add-client" (click)="openAdd()">
          ＋ Додати клієнта
        </button>
      </div>

      <div class="search-bar">
        <input type="text" placeholder="🔍 Пошук клієнта..." [(ngModel)]="searchQuery" (ngModelChange)="applyFilter()" id="search-clients">
      </div>

      @if (loading()) {
        <div class="loading"><div class="spinner"></div> Завантаження...</div>
      }

      @if (!loading()) {
        <div class="clients-grid">
          @if (filtered().length === 0) {
            <div class="empty-state" style="grid-column:1/-1;">
              <div class="empty-icon">👥</div>
              <h3>Клієнтів не знайдено</h3>
              <p>Додайте першого клієнта!</p>
            </div>
          }
          @for (c of filtered(); track c.clientId) {
            <div class="client-card card">
              <div class="client-avatar">{{ c.name[0] | uppercase }}</div>
              <div class="client-info">
                <h4>{{ c.name }}</h4>
                <span class="text-muted">{{ c.phone ?? '—' }}</span>
              </div>
              <div class="client-actions">
                <button class="btn btn-ghost btn-icon btn-sm" (click)="edit(c)" title="Редагувати">✏️</button>
                <button class="btn btn-danger btn-icon btn-sm" (click)="remove(c.clientId)" title="Видалити">🗑</button>
              </div>
            </div>
          }
        </div>
      }
    </div>

    <!-- Modal -->
    @if (showModal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal animate-in" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ editing() ? '✏️ Редагувати клієнта' : '➕ Новий клієнт' }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>
          <div class="form-group">
            <label>Назва / Ім'я</label>
            <input type="text" [(ngModel)]="form.name" id="input-clientName" placeholder="ТОВ Пивоманія">
          </div>
          <div class="form-group">
            <label>Телефон</label>
            <input type="tel" [(ngModel)]="form.phone" id="input-clientPhone" placeholder="+380991234567">
          </div>
          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-client" (click)="save()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              {{ editing() ? 'Зберегти' : 'Додати' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .clients-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: var(--space-md);
    }
    .client-card {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 14px 16px;
    }
    .client-avatar {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: linear-gradient(135deg, var(--amber-700), var(--amber-900));
      color: var(--amber-300);
      font-weight: 700;
      font-size: 1rem;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .client-info { flex: 1; min-width: 0; }
    .client-info h4 { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: 0.9rem; }
    .client-info span { font-size: 0.8rem; }
    .client-actions { display: flex; gap: 4px; opacity: 0; transition: opacity var(--transition); }
    .client-card:hover .client-actions { opacity: 1; }
  `]
})
export class ClientsComponent implements OnInit {
  loading    = signal(true);
  showModal  = signal(false);
  saving     = signal(false);
  editing    = signal(false);
  clients    = signal<Client[]>([]);
  filtered   = signal<Client[]>([]);
  searchQuery = '';
  form: Partial<Client> = { name:'', phone:'' };
  private editId = 0;

  constructor(private api: ApiService) {}
  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.getClients().subscribe({ next: c => { this.clients.set(c); this.applyFilter(); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  applyFilter() {
    let list = this.clients();
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(c => c.name.toLowerCase().includes(q) || (c.phone ?? '').includes(q));
    }
    this.filtered.set(list);
  }

  openAdd() { this.editing.set(false); this.form = { name:'', phone:'' }; this.showModal.set(true); }

  edit(c: Client) {
    this.editing.set(true); this.editId = c.clientId;
    this.form = { name: c.name, phone: c.phone ?? '' };
    this.showModal.set(true);
  }

  save() {
    this.saving.set(true);
    const obs: Observable<any> = this.editing() ? this.api.updateClient(this.editId, this.form) : this.api.createClient(this.form);
    obs.subscribe({ next: () => { this.saving.set(false); this.closeModal(); this.load(); }, error: () => this.saving.set(false) });
  }

  remove(id: number) {
    if (!confirm('Видалити клієнта?')) return;
    this.api.deleteClient(id).subscribe(() => this.load());
  }

  closeModal() { this.showModal.set(false); }
}
