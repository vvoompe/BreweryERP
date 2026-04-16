import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { Observable }                from 'rxjs';
import { ApiService }                from '../../core/api.service';
import { BeerStyle }                 from '../../core/models';

@Component({
  selector: 'app-beer-styles',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">
      <div class="page-header flex-between">
        <div>
          <h2>🍻 Стилі пива</h2>
          <p>Класифікація пивних стилів (BJCP)</p>
        </div>
        <button class="btn btn-primary" id="btn-add-style" (click)="openAdd()">
          ＋ Новий стиль
        </button>
      </div>

      <div class="search-bar">
        <input type="text"
          placeholder="🔍 Пошук стилю..."
          [(ngModel)]="searchQuery"
          (ngModelChange)="applyFilter()"
          id="search-styles">
      </div>

      @if (loading()) {
        <div class="loading"><div class="spinner"></div> Завантаження...</div>
      }

      @if (!loading()) {
        <!-- Card grid layout -->
        <div class="styles-grid">
          @if (filtered().length === 0) {
            <div class="empty-state" style="grid-column:1/-1;">
              <div class="empty-icon">🍻</div>
              <h3>Стилів не знайдено</h3>
              <p>Додайте перший пивний стиль!</p>
            </div>
          }
          @for (s of filtered(); track s.styleId) {
            <div class="style-card card">
              <div class="style-header">
                <div class="style-number">{{ s.styleId }}</div>
                <div class="style-actions">
                  <button class="btn btn-ghost btn-icon btn-sm" (click)="edit(s)" title="Редагувати">✏️</button>
                  <button class="btn btn-danger btn-icon btn-sm" (click)="remove(s.styleId)" title="Видалити">🗑</button>
                </div>
              </div>
              <div class="style-body">
                <h4>{{ s.name }}</h4>
                @if (s.description) {
                  <p class="style-desc">{{ s.description }}</p>
                }
              </div>
              <div class="style-footer">
                @if (s.minAbv != null || s.maxAbv != null) {
                  <div class="style-param">
                    <span class="param-label">ABV</span>
                    <span class="param-value text-amber">{{ s.minAbv ?? '?' }}–{{ s.maxAbv ?? '?' }}%</span>
                  </div>
                }
                @if (s.minIbu != null || s.maxIbu != null) {
                  <div class="style-param">
                    <span class="param-label">IBU</span>
                    <span class="param-value">{{ s.minIbu ?? '?' }}–{{ s.maxIbu ?? '?' }}</span>
                  </div>
                }
                @if (s.minSrm != null || s.maxSrm != null) {
                  <div class="style-param">
                    <span class="param-label">SRM</span>
                    <div class="srm-swatch" [style.background]="srmToHex(((s.minSrm ?? 0) + (s.maxSrm ?? 0)) / 2)">
                      {{ s.minSrm ?? '?' }}–{{ s.maxSrm ?? '?' }}
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>

    <!-- Modal -->
    @if (showModal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal animate-in" style="max-width:520px;" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ editing() ? '✏️ Редагувати стиль' : '➕ Новий стиль пива' }}</h3>
            <button class="btn btn-ghost btn-icon" (click)="closeModal()">✕</button>
          </div>

          <div class="form-group">
            <label>Назва</label>
            <input type="text" [(ngModel)]="form.name" id="input-styleName" placeholder="American IPA">
          </div>
          <div class="form-group">
            <label>Опис (необов'язково)</label>
            <textarea [(ngModel)]="form.description" id="input-styleDesc" rows="3"
              placeholder="Хмелеве, гіркувате, ароматне..." style="resize:vertical;"></textarea>
          </div>

          <div style="display:grid; grid-template-columns:1fr 1fr; gap:12px;">
            <div class="form-group">
              <label>ABV мін. (%)</label>
              <input type="number" step="0.1" min="0" max="30" [(ngModel)]="form.minAbv" id="input-minAbv" placeholder="4.5">
            </div>
            <div class="form-group">
              <label>ABV макс. (%)</label>
              <input type="number" step="0.1" min="0" max="30" [(ngModel)]="form.maxAbv" id="input-maxAbv" placeholder="7.5">
            </div>
            <div class="form-group">
              <label>IBU мін.</label>
              <input type="number" step="1" min="0" max="200" [(ngModel)]="form.minIbu" id="input-minIbu" placeholder="40">
            </div>
            <div class="form-group">
              <label>IBU макс.</label>
              <input type="number" step="1" min="0" max="200" [(ngModel)]="form.maxIbu" id="input-maxIbu" placeholder="70">
            </div>
            <div class="form-group">
              <label>SRM мін. (колір)</label>
              <input type="number" step="1" min="1" max="40" [(ngModel)]="form.minSrm" id="input-minSrm" placeholder="6">
            </div>
            <div class="form-group">
              <label>SRM макс.</label>
              <input type="number" step="1" min="1" max="40" [(ngModel)]="form.maxSrm" id="input-maxSrm" placeholder="14">
            </div>
          </div>

          <!-- SRM preview -->
          @if (form.minSrm || form.maxSrm) {
            <div class="srm-preview">
              <span class="text-muted" style="font-size:.75rem;">Колір SRM:</span>
              <div class="srm-gradient"
                [style.background]="'linear-gradient(to right, ' + srmToHex(form.minSrm ?? 1) + ', ' + srmToHex(form.maxSrm ?? form.minSrm ?? 1) + ')'">
              </div>
            </div>
          }

          <div class="modal-footer">
            <button class="btn btn-ghost" (click)="closeModal()">Скасувати</button>
            <button class="btn btn-primary" id="btn-save-style" (click)="save()" [disabled]="saving() || !form.name">
              @if (saving()) { <span class="spinner"></span> }
              {{ editing() ? 'Зберегти' : 'Додати стиль' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .styles-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: var(--space-md);
    }

    .style-card {
      display: flex;
      flex-direction: column;
      padding: 0;
      overflow: hidden;
      transition: transform var(--transition), box-shadow var(--transition);
    }
    .style-card:hover {
      transform: translateY(-2px);
      box-shadow: var(--shadow-lg);
    }

    .style-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 14px;
      border-bottom: 1px solid var(--border-color);
      background: rgba(212,134,11,0.05);
    }
    .style-number {
      font-size: 0.7rem;
      color: var(--text-muted);
      font-family: var(--font-mono);
      background: var(--bg-base);
      border: 1px solid var(--border-color);
      padding: 2px 7px;
      border-radius: var(--radius-full);
    }
    .style-actions { display: flex; gap: 2px; }

    .style-body { padding: 12px 14px; flex: 1; }
    .style-body h4 { font-size: 0.95rem; margin-bottom: 4px; }
    .style-desc {
      font-size: 0.78rem;
      color: var(--text-muted);
      line-height: 1.4;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .style-footer {
      display: flex;
      gap: 8px;
      padding: 10px 14px;
      border-top: 1px solid var(--border-color);
      flex-wrap: wrap;
    }
    .style-param {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 0.75rem;
    }
    .param-label {
      color: var(--text-muted);
      font-weight: 500;
      font-size: 0.65rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .param-value { font-weight: 600; font-family: var(--font-mono); }

    .srm-swatch {
      width: 40px; height: 16px;
      border-radius: 3px;
      font-size: 0.65rem;
      display: flex; align-items: center; justify-content: center;
      color: rgba(0,0,0,0.7);
    }

    .srm-preview {
      display: flex; align-items: center; gap: 10px;
      margin-bottom: var(--space-md);
    }
    .srm-gradient {
      height: 20px; flex: 1;
      border-radius: var(--radius-sm);
      border: 1px solid var(--border-color);
    }

    textarea { font-family: inherit; }
  `]
})
export class BeerStylesComponent implements OnInit {
  loading   = signal(true);
  showModal = signal(false);
  saving    = signal(false);
  editing   = signal(false);
  styles    = signal<BeerStyle[]>([]);
  filtered  = signal<BeerStyle[]>([]);
  searchQuery = '';
  form: Partial<BeerStyle> = { name: '', description: '', minAbv: undefined, maxAbv: undefined, minIbu: undefined, maxIbu: undefined, minSrm: undefined, maxSrm: undefined };
  private editId = 0;

  constructor(private api: ApiService) {}
  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.getStyles().subscribe({
      next: s => { this.styles.set(s); this.applyFilter(); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  applyFilter() {
    let list = this.styles();
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(s => s.name.toLowerCase().includes(q) || (s.description ?? '').toLowerCase().includes(q));
    }
    this.filtered.set(list);
  }

  openAdd() {
    this.editing.set(false);
    this.form = { name: '', description: '', minAbv: undefined, maxAbv: undefined, minIbu: undefined, maxIbu: undefined, minSrm: undefined, maxSrm: undefined };
    this.showModal.set(true);
  }

  edit(s: BeerStyle) {
    this.editing.set(true);
    this.editId = s.styleId;
    this.form = {
      name: s.name,
      description: s.description ?? '',
      minAbv: s.minAbv ?? undefined,
      maxAbv: s.maxAbv ?? undefined,
      minIbu: s.minIbu ?? undefined,
      maxIbu: s.maxIbu ?? undefined,
      minSrm: s.minSrm ?? undefined,
      maxSrm: s.maxSrm ?? undefined,
    };
    this.showModal.set(true);
  }

  save() {
    this.saving.set(true);
    const obs: Observable<any> = this.editing()
      ? this.api.updateStyle(this.editId, this.form)
      : this.api.createStyle(this.form);
    obs.subscribe({
      next: () => { this.saving.set(false); this.closeModal(); this.load(); },
      error: () => this.saving.set(false)
    });
  }

  remove(id: number) {
    if (!confirm('Видалити цей стиль? Пов\'язані рецепти залишаться.')) return;
    this.api.deleteStyle(id).subscribe(() => this.load());
  }

  closeModal() { this.showModal.set(false); }

  /** Повертає HEX-колір для значення SRM (1–40) */
  srmToHex(srm: number): string {
    const colors: Record<number, string> = {
      1:'#FFE699', 2:'#FFD878', 3:'#FFCA5A', 4:'#FFBF42', 5:'#FBB123',
      6:'#F8A600', 7:'#F39C00', 8:'#EA8F00', 9:'#E58500', 10:'#DE7C00',
      12:'#D47500', 14:'#CF6900', 16:'#CB5D00', 18:'#C15300', 20:'#BE4A00',
      24:'#B54100', 28:'#AE3B00', 32:'#A03200', 36:'#922B00', 40:'#8D1400',
    };
    // Знаходимо найближчий
    const keys = Object.keys(colors).map(Number);
    const closest = keys.reduce((a, b) => Math.abs(b - srm) < Math.abs(a - srm) ? b : a);
    return colors[closest];
  }

}

