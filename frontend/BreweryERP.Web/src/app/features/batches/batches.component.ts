import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { Batch, BatchStatus, Recipe } from '../../core/models';

@Component({
  selector: 'app-batches',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>🍺 Партії</h2>
          <p>Виробничі партії пива та їх статуси</p>
        </div>
        <button class="btn btn-primary" id="btn-add-batch" (click)="openAdd()">
          ＋ Нова партія
        </button>
      </div>

      <!-- Filter by status -->
      <div class="search-bar">
        <input type="text" placeholder="🔍 Пошук..." [(ngModel)]="searchQuery" (ngModelChange)="applyFilter()" id="search-batches">
        <select [(ngModel)]="statusFilter" (ngModelChange)="applyFilter()" id="filter-status" style="width:auto; min-width:150px;">
          <option value="all">Всі статуси</option>
          <option value="Brewing">Бродіння</option>
          <option value="Fermenting">Ферментація</option>
          <option value="Completed">Завершено</option>
          <option value="Failed">Невдала</option>
        </select>
      </div>

      <!-- Status counters -->
      <div class="status-counters">
        @for (s of statuses; track s.key) {
          <div class="counter-chip" [class.counter-chip--active]="statusFilter === s.key" (click)="setFilter(s.key)">
            <span class="badge" [ngClass]="s.badge">{{ s.label }}</span>
            <span class="counter-count">{{ countByStatus(s.key) }}</span>
          </div>
        }
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
                <th>Рецепт</th>
                <th>Дата початку</th>
                <th>Статус</th>
                <th>ABV</th>
                <th>SRM</th>
                <th>Дії</th>
              </tr>
            </thead>
            <tbody>
              @if (filtered().length === 0) {
                <tr><td colspan="7">
                  <div class="empty-state">
                    <div class="empty-icon">🍺</div>
                    <h3>Партій не знайдено</h3>
                  </div>
                </td></tr>
              }
              @for (b of filtered(); track b.batchId) {
                <tr>
                  <td class="text-muted font-mono">{{ b.batchId }}</td>
                  <td><strong>{{ b.recipeName ?? 'Рецепт #' + b.recipeId }}</strong></td>
                  <td>{{ b.startDate | date:'dd.MM.yyyy' }}</td>
                  <td>
                    <span class="badge" [ngClass]="batchBadge(b.status)">{{ b.status }}</span>
                  </td>
                  <td>
                    @if (b.actualAbv) { <span class="font-mono text-amber">{{ b.actualAbv }}%</span> }
                    @else { <span class="text-muted">—</span> }
                  </td>
                  <td>
                    @if (b.actualSrm) { <span class="font-mono">{{ b.actualSrm }}</span> }
                    @else { <span class="text-muted">—</span> }
                  </td>
                  <td>
                    <div class="row-actions">
                      <button class="btn btn-ghost btn-icon btn-sm" (click)="edit(b)">✏️</button>
                      <button class="btn btn-danger btn-icon btn-sm" (click)="remove(b.batchId)">🗑</button>
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
            <h3>{{ editing() ? '✏️ Редагувати партію' : '➕ Нова партія' }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>

          <div class="form-group">
            <label>Рецепт</label>
            <select [(ngModel)]="form.recipeId" id="input-recipeId">
              @for (r of recipes(); track r.recipeId) {
                <option [value]="r.recipeId">{{ r.versionName }}</option>
              }
            </select>
          </div>
          <div class="form-group">
            <label>Статус</label>
            <select [(ngModel)]="form.status" id="input-batchStatus">
              <option value="Brewing">Бродіння</option>
              <option value="Fermenting">Ферментація</option>
              <option value="Completed">Завершено</option>
              <option value="Failed">Невдала</option>
            </select>
          </div>
          <div class="form-group">
            <label>Дата початку</label>
            <input type="date" [(ngModel)]="form.startDate" id="input-startDate">
          </div>
          <div style="display:grid; grid-template-columns:1fr 1fr; gap:12px;">
            <div class="form-group">
              <label>ABV (%)</label>
              <input type="number" step="0.1" [(ngModel)]="form.actualAbv" id="input-actualAbv" placeholder="5.5">
            </div>
            <div class="form-group">
              <label>SRM</label>
              <input type="number" [(ngModel)]="form.actualSrm" id="input-actualSrm" placeholder="14">
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-batch" (click)="save()" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              {{ editing() ? 'Зберегти' : 'Створити' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .status-counters {
      display: flex;
      gap: 8px;
      margin-bottom: 20px;
      flex-wrap: wrap;
    }
    .counter-chip {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 12px;
      background: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-full);
      cursor: pointer;
      transition: all var(--transition);
    }
    .counter-chip:hover { border-color: var(--border-active); }
    .counter-chip--active { border-color: var(--amber-500); background: rgba(212,134,11,0.08); }
    .counter-count { font-size: 0.875rem; font-weight: 700; color: var(--text-primary); }
  `]
})
export class BatchesComponent implements OnInit {
  loading     = signal(true);
  showModal   = signal(false);
  saving      = signal(false);
  editing     = signal(false);
  batches     = signal<Batch[]>([]);
  recipes     = signal<Recipe[]>([]);
  filtered    = signal<Batch[]>([]);
  searchQuery  = '';
  statusFilter = 'all';
  form: Partial<Batch> = { recipeId: 0, status: 'Brewing', startDate: new Date().toISOString().split('T')[0] };
  private editId = 0;

  statuses = [
    { key:'all',        label:'Всі',         badge:'badge-inactive' },
    { key:'Brewing',    label:'Бродіння',    badge:'badge-brewing' },
    { key:'Fermenting', label:'Ферментація', badge:'badge-fermenting' },
    { key:'Completed',  label:'Завершено',   badge:'badge-completed' },
    { key:'Failed',     label:'Невдала',     badge:'badge-failed' },
  ];

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getRecipes().subscribe(r => { this.recipes.set(r); if (r.length) this.form.recipeId = r[0].recipeId; });
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.getBatches().subscribe({ next: b => { this.batches.set(b); this.applyFilter(); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  applyFilter() {
    let list = this.batches();
    if (this.statusFilter !== 'all') list = list.filter(b => b.status === this.statusFilter);
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(b => (b.recipeName ?? '').toLowerCase().includes(q));
    }
    this.filtered.set(list);
  }

  setFilter(key: string) { this.statusFilter = key; this.applyFilter(); }
  countByStatus(key: string) { return key === 'all' ? this.batches().length : this.batches().filter(b => b.status === key).length; }

  openAdd() {
    this.editing.set(false);
    this.form = { recipeId: this.recipes()[0]?.recipeId ?? 0, status: 'Brewing', startDate: new Date().toISOString().split('T')[0] };
    this.showModal.set(true);
  }

  edit(b: Batch) {
    this.editing.set(true);
    this.editId = b.batchId;
    this.form = { recipeId: b.recipeId, status: b.status, startDate: b.startDate?.split('T')[0] ?? '', actualAbv: b.actualAbv ?? undefined, actualSrm: b.actualSrm ?? undefined };
    this.showModal.set(true);
  }

  save() {
    this.saving.set(true);
    const obs: Observable<any> = this.editing() ? this.api.updateBatch(this.editId, this.form) : this.api.createBatch(this.form);
    obs.subscribe({ next: () => { this.saving.set(false); this.closeModal(); this.load(); }, error: () => this.saving.set(false) });
  }

  remove(id: number) {
    if (!confirm('Видалити цю партію?')) return;
    this.api.deleteBatch(id).subscribe(() => this.load());
  }

  closeModal() { this.showModal.set(false); }

  batchBadge(s: BatchStatus): string {
    return { Brewing:'badge-brewing', Fermenting:'badge-fermenting', Completed:'badge-completed', Failed:'badge-failed' }[s] ?? 'badge-inactive';
  }
}
