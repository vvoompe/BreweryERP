import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule }   from '@angular/common';
import { FormsModule }    from '@angular/forms';
import { ApiService }     from '../../core/api.service';
import {
  ExcelPreviewDto, ExcelRowPreview, ExcelRowCommit,
  ImportLog, ImportResultDto, ColumnInfo, Supplier
} from '../../core/models';

type Step = 'drop' | 'configure' | 'preview' | 'success';
type FileFormat = 'xlsx' | 'csv' | null;

interface MappingForm {
  dataStartRow: number;
  colName:  number;  // 0 = auto
  colType:  number;
  colQty:   number;
  colUnit:  number;
  colExp:   number;
  colPrice: number;
}

@Component({
  selector: 'app-excel-import',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="animate-in">

      <!-- Header -->
      <div class="page-header flex-between">
        <div>
          <h2>📥 Імпорт накладних</h2>
          <p>Завантажте <strong>Excel (.xlsx)</strong> або <strong>CSV (.csv)</strong> від постачальника</p>
        </div>
        <div style="display:flex;gap:10px;">
          <button class="btn btn-ghost" (click)="downloadTemplate()" id="btn-template">
            ⬇️ Шаблон .xlsx
          </button>
          @if (step() !== 'drop') {
            <button class="btn btn-ghost" (click)="resetAll()">↩ Почати заново</button>
          }
        </div>
      </div>

      <!-- Tabs -->
      <div class="tab-bar">
        <button class="tab-btn" [class.tab-btn--active]="activeTab() === 'import'" (click)="activeTab.set('import')">
          📤 Завантаження
        </button>
        <button class="tab-btn" [class.tab-btn--active]="activeTab() === 'logs'" (click)="switchToLogs()">
          📋 Журнал
          @if (logs().length > 0) { <span class="badge badge-inactive" style="margin-left:4px">{{ logs().length }}</span> }
        </button>
      </div>

      @if (activeTab() === 'import') {

        <!-- ══ STEP: Success ══ -->
        @if (step() === 'success' && importResult()) {
          <div class="success-banner animate-in">
            <div class="success-icon">✅</div>
            <div style="flex:1">
              <strong>Імпорт завершено успішно!</strong>
              <div style="margin-top:8px; display:flex; gap:12px; flex-wrap:wrap;">
                <span class="badge badge-active" style="font-size:0.9rem; padding:6px 14px;">
                  📦 {{ importResult()!.importedRows }} позицій імпортовано
                </span>
                @if (importResult()!.newIngredients > 0) {
                  <span class="badge badge-new" style="font-size:0.9rem; padding:6px 14px;">
                    🆕 {{ importResult()!.newIngredients }} нових інгредієнтів
                  </span>
                }
                <span class="badge badge-inactive" style="font-size:0.9rem; padding:6px 14px;">
                  📄 Накладна #{{ importResult()!.invoiceId }}
                </span>
              </div>
              <p style="margin:8px 0 0; color:var(--text-muted); font-size:0.875rem;">
                {{ importResult()!.message }}
              </p>
            </div>
            <button class="btn btn-primary" (click)="resetAll()">Новий імпорт</button>
          </div>
        }

        <!-- ══ STEP: Drop ══ -->
        @if (step() === 'drop') {
          <div class="drop-zone"
               [class.drop-zone--active]="isDragging()"
               (dragover)="$event.preventDefault(); isDragging.set(true)"
               (dragleave)="isDragging.set(false)"
               (drop)="onDrop($event)"
               (click)="fileInput.click()"
               id="drop-zone">
            <input #fileInput type="file" accept=".xlsx,.csv" style="display:none"
                   (change)="onFileSelected($event)">
            <div class="drop-icon">📊</div>
            <h3 class="drop-title">Перетягніть файл сюди</h3>
            <p class="drop-subtitle">або натисніть для вибору</p>
            <div style="display:flex; gap:12px; justify-content:center; margin-top:12px;">
              <span class="format-badge">📗 Excel .xlsx</span>
              <span class="format-badge">📄 CSV .csv</span>
            </div>
          </div>
        }

        <!-- ══ STEP: Configure (налаштування mapping) ══ -->
        @if (step() === 'configure' || step() === 'preview') {

          <!-- File info bar -->
          <div class="file-info-bar">
            <span class="file-format-icon">{{ fileFormatIcon() }}</span>
            <span class="file-name">{{ selectedFile()?.name }}</span>
            <span class="file-size text-muted">{{ fileSizeStr() }}</span>
            <button class="btn btn-ghost btn-sm" (click)="fileInput2.click()">Змінити файл</button>
            <input #fileInput2 type="file" accept=".xlsx,.csv" style="display:none"
                   (change)="onFileSelected($event)">
          </div>

          <!-- Supplier + Doc settings -->
          <div class="card" style="padding:20px; margin-bottom:16px;">
            <h4 class="section-label">① Налаштування накладної</h4>
            <div class="settings-grid">
              <div class="form-group" style="margin:0">
                <label>Постачальник</label>
                <select [(ngModel)]="form.supplierId" id="sel-supplier">
                  @for (s of suppliers(); track s.supplierId) {
                    <option [value]="s.supplierId">{{ s.name }}</option>
                  }
                </select>
              </div>
              <div class="form-group" style="margin:0">
                <label>Номер документу</label>
                <input type="text" [(ngModel)]="form.docNumber" placeholder="НК-2025-001" id="input-docnum">
              </div>
              <div class="form-group" style="margin:0">
                <label>Дата отримання</label>
                <input type="date" [(ngModel)]="form.receiveDate" id="input-date">
              </div>
            </div>
          </div>

          <!-- Mapping config -->
          <div class="card" style="padding:20px; margin-bottom:16px;">
            <div class="mapping-header" (click)="mappingExpanded.set(!mappingExpanded())">
              <h4 class="section-label" style="margin:0; cursor:pointer;">
                ② Налаштування колонок
                <span class="text-muted" style="font-size:0.8rem; font-weight:400;">
                  — вибери яка колонка файлу відповідає якому полю
                </span>
              </h4>
              <span style="font-size:1.2rem; cursor:pointer">{{ mappingExpanded() ? '▲' : '▼' }}</span>
            </div>

            @if (mappingExpanded()) {
              <!-- Вибір рядка початку -->
              <div style="margin-top:16px; display:flex; align-items:center; gap:12px;">
                <label style="white-space:nowrap; font-weight:500;">Дані починаються з рядка:</label>
                <input type="number" [(ngModel)]="mapping.dataStartRow" min="1" max="100"
                       style="width:80px;" id="input-startrow">
                <span class="text-muted" style="font-size:0.8rem;">
                  (за замовчуванням 2 — якщо перший рядок є заголовком)
                </span>
              </div>

              <!-- Column selectors -->
              <div class="col-mapping-grid" style="margin-top:16px;">
                @for (field of mappingFields; track field.key) {
                  <div class="col-mapping-row">
                    <div class="col-label">
                      <span class="col-required">{{ field.required ? '*' : '' }}</span>
                      {{ field.label }}
                    </div>
                    <div style="flex:1">
                      <select [(ngModel)]="mapping[field.key]" [id]="'col-' + field.key">
                        <option [value]="0">🔍 Авто-визначити</option>
                        @for (col of availableColumns(); track col.index) {
                          <option [value]="col.index">
                            {{ col.letter }}{{ col.header ? ' — ' + col.header : '' }}
                          </option>
                        }
                        @if (availableColumns().length === 0) {
                          @for (n of defaultColOptions; track n.v) {
                            <option [value]="n.v">Колонка {{ n.l }}</option>
                          }
                        }
                      </select>
                    </div>
                    @if (suggestedMapping() && getSuggested(field.key) > 0) {
                      <span class="text-muted" style="font-size:0.75rem; white-space:nowrap;">
                        (авто: {{ colLetter(getSuggested(field.key)) }})
                      </span>
                    }
                  </div>
                }
              </div>
            }
          </div>

          <!-- Parse button -->
          <div style="display:flex; justify-content:center; margin-bottom:20px;">
            <button class="btn btn-primary btn-lg" id="btn-parse"
                    [disabled]="isParsing()"
                    (click)="parseFile()">
              @if (isParsing()) { <span class="spinner"></span> }
              {{ step() === 'preview' ? '🔄 Повторити парсинг' : '🔍 Переглянути дані' }}
            </button>
          </div>
        }

        <!-- ══ STEP: Preview ══ -->
        @if (step() === 'preview' && preview()) {
          <div class="preview-section animate-in">

            <!-- Summary bar -->
            <div class="preview-summary">
              <div style="display:flex; gap:12px; flex-wrap:wrap; align-items:center;">
                <span class="badge badge-active" style="font-size:0.875rem; padding:6px 14px;">
                  ✅ {{ preview()!.validCount }} валідних рядків
                </span>
                @if (preview()!.errorCount > 0) {
                  <span class="badge badge-failed" style="font-size:0.875rem; padding:6px 14px;">
                    ❌ {{ preview()!.errorCount }} рядків з помилками
                  </span>
                }
                @if (newCount() > 0) {
                  <span class="badge badge-new" style="font-size:0.875rem; padding:6px 14px;">
                    🆕 {{ newCount() }} нових інгредієнтів
                  </span>
                }
                <span class="text-muted" style="font-size:0.8rem;">
                  Всього рядків: {{ preview()!.rows.length }}
                </span>
              </div>
              <button class="btn btn-primary" id="btn-commit"
                      [disabled]="preview()!.errorCount > 0 || isCommitting()"
                      (click)="commitImport()">
                @if (isCommitting()) { <span class="spinner"></span> }
                ✅ Підтвердити імпорт ({{ preview()!.validCount }} поз.)
              </button>
            </div>

            @if (preview()!.globalErrors.length > 0) {
              <div class="alert-error" style="margin-bottom:16px;">
                @for (e of preview()!.globalErrors; track $index) { <div>⚠ {{ e }}</div> }
              </div>
            }

            @if (preview()!.errorCount > 0) {
              <div class="alert-warn" style="margin-bottom:12px;">
                ⚠ Є рядки з помилками — виправте mapping або start row і натисніть "Повторити парсинг"
              </div>
            }

            <!-- Preview Table -->
            <div class="table-wrapper">
              <table>
                <thead>
                  <tr>
                    <th style="width:60px">Рядок</th>
                    <th>Назва</th>
                    <th>Тип</th>
                    <th style="width:90px">К-сть</th>
                    <th style="width:70px">Од.</th>
                    <th>Дата закінч.</th>
                    <th>Ціна/од</th>
                    <th style="width:130px">Статус</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of preview()!.rows; track row.rowNumber) {
                    <tr [class.row-error]="row.error" [class.row-new]="row.isNew && !row.error">
                      <td class="font-mono text-muted">{{ row.rowNumber }}</td>
                      <td>
                        <strong>{{ row.ingredientName }}</strong>
                        @if (row.isNew && !row.error) {
                          <span class="badge badge-new" style="margin-left:5px; font-size:0.7rem">НОВИЙ</span>
                        }
                      </td>
                      <td>
                        <span class="badge badge-inactive" style="font-size:0.72rem">
                          {{ typeLabel(row.ingredientType) }}
                        </span>
                      </td>
                      <td class="font-mono">{{ row.quantity }}</td>
                      <td class="text-muted">{{ row.unit }}</td>
                      <td class="text-muted" style="font-size:0.8rem">{{ row.expirationDate ?? '—' }}</td>
                      <td class="font-mono">
                        {{ row.unitPrice != null ? (row.unitPrice | number:'1.2-2') + ' ₴' : '—' }}
                      </td>
                      <td>
                        @if (row.error) {
                          <span class="badge badge-failed" style="font-size:0.7rem" [title]="row.error">❌ Помилка</span>
                        } @else if (row.isNew) {
                          <span class="badge badge-new" style="font-size:0.7rem">🆕 Автостворення</span>
                        } @else {
                          <span class="badge badge-active" style="font-size:0.7rem">✅ Знайдено</span>
                        }
                      </td>
                    </tr>
                    @if (row.error) {
                      <tr class="row-error-detail">
                        <td colspan="8">⚠ {{ row.error }}</td>
                      </tr>
                    }
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      }

      <!-- ══ LOGS TAB ══ -->
      @if (activeTab() === 'logs') {
        @if (logsLoading()) {
          <div class="loading"><div class="spinner"></div> Завантаження...</div>
        } @else if (logs().length === 0) {
          <div class="empty-state">
            <div class="empty-icon">📋</div>
            <h3>Журнал порожній</h3>
            <p>Після першого імпорту сюди з'являться записи</p>
          </div>
        } @else {
          <div class="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>#</th><th>Файл</th><th>Дата</th><th>Користувач</th>
                  <th>Позицій</th><th>Накладна</th><th>Статус</th>
                </tr>
              </thead>
              <tbody>
                @for (log of logs(); track log.importId) {
                  <tr>
                    <td class="font-mono text-muted">{{ log.importId }}</td>
                    <td>{{ fileIcon(log.fileName) }} {{ log.fileName }}</td>
                    <td class="text-muted" style="font-size:0.8rem">{{ log.importedAt | date:'dd.MM.yyyy HH:mm' }}</td>
                    <td class="text-muted">{{ log.importedBy }}</td>
                    <td class="font-mono">{{ log.rowCount }}</td>
                    <td>
                      @if (log.invoiceId) { <span class="badge badge-active">#{{ log.invoiceId }}</span> }
                      @else { <span class="text-muted">—</span> }
                    </td>
                    <td>
                      @if (log.status === 'Success') {
                        <span class="badge badge-active">✅ Успіх</span>
                      } @else if (log.status === 'Failed') {
                        <span class="badge badge-failed" [title]="log.error ?? ''">❌ Помилка</span>
                      } @else {
                        <span class="badge badge-inactive">⏳ {{ log.status }}</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .tab-bar { display:flex; gap:4px; margin-bottom:24px; border-bottom:1px solid var(--border-color); padding-bottom:0; }
    .tab-btn { background:none; border:none; color:var(--text-muted); cursor:pointer; padding:10px 18px; font-size:0.9rem; border-bottom:2px solid transparent; margin-bottom:-1px; transition:all var(--transition); }
    .tab-btn:hover { color:var(--text-primary); }
    .tab-btn--active { color:var(--amber-400); border-bottom-color:var(--amber-500); font-weight:600; }

    .drop-zone {
      border: 2px dashed var(--border-color); border-radius:var(--radius-lg);
      padding: 60px 40px; text-align:center; cursor:pointer;
      transition: all var(--transition); background: var(--bg-card);
    }
    .drop-zone:hover, .drop-zone--active {
      border-color:var(--amber-500); background:rgba(212,134,11,0.05);
    }
    .drop-icon { font-size:3rem; margin-bottom:12px; }
    .drop-title { margin:0; font-size:1.1rem; }
    .drop-subtitle { color:var(--text-muted); margin:6px 0 0; }
    .format-badge { background:var(--bg-hover); border:1px solid var(--border-color); border-radius:var(--radius); padding:4px 12px; font-size:0.8rem; color:var(--text-muted); }

    .file-info-bar {
      display:flex; align-items:center; gap:12px; background:var(--bg-card);
      border:1px solid var(--border-color); border-radius:var(--radius);
      padding:10px 16px; margin-bottom:16px; font-size:0.875rem;
    }
    .file-format-icon { font-size:1.4rem; }
    .file-name { font-weight:500; color:var(--text-primary); }
    .file-size { font-size:0.8rem; }

    .section-label { font-size:0.85rem; text-transform:uppercase; letter-spacing:.04em; color:var(--text-secondary); margin:0 0 8px; }
    .settings-grid { display:grid; grid-template-columns:1fr 1fr 1fr; gap:16px; margin-top:12px; }

    .mapping-header { display:flex; justify-content:space-between; align-items:center; }
    .col-mapping-grid { display:flex; flex-direction:column; gap:10px; }
    .col-mapping-row { display:flex; align-items:center; gap:12px; }
    .col-label { min-width:170px; font-size:0.875rem; font-weight:500; }
    .col-required { color:var(--status-failed); font-weight:700; margin-right:3px; }

    .btn-lg { padding:12px 28px; font-size:1rem; }

    .preview-section { margin-top:4px; }
    .preview-summary {
      display:flex; align-items:center; justify-content:space-between;
      margin-bottom:16px; padding-bottom:16px; border-bottom:1px solid var(--border-color); flex-wrap:wrap; gap:12px;
    }

    .alert-error { background:rgba(239,68,68,0.08); border:1px solid var(--status-failed); border-radius:var(--radius); padding:10px 14px; color:var(--status-failed); font-size:0.875rem; }
    .alert-warn  { background:rgba(212,134,11,0.08); border:1px solid var(--amber-500); border-radius:var(--radius); padding:10px 14px; color:var(--amber-400); font-size:0.875rem; }

    .row-error td { background:rgba(239,68,68,0.04); }
    .row-error-detail td { background:rgba(239,68,68,0.08); padding:4px 16px !important; font-size:0.8rem; color:var(--status-failed); }
    .row-new td { background:rgba(34,197,94,0.04); }

    .success-banner {
      display:flex; align-items:flex-start; gap:20px;
      background:rgba(34,197,94,0.08); border:1px solid var(--status-completed);
      border-radius:var(--radius-lg); padding:24px 28px; margin-bottom:24px;
    }
    .success-icon { font-size:2.5rem; line-height:1; }
    .success-banner strong { font-size:1.05rem; }
  `]
})
export class ExcelImportComponent implements OnInit {
  // ── State ──────────────────────────────────────────────────────────────────
  step        = signal<Step>('drop');
  activeTab   = signal<'import' | 'logs'>('import');
  isDragging  = signal(false);
  isParsing   = signal(false);
  isCommitting = signal(false);
  logsLoading = signal(false);
  mappingExpanded = signal(true);

  suppliers    = signal<Supplier[]>([]);
  logs         = signal<ImportLog[]>([]);
  preview      = signal<ExcelPreviewDto | null>(null);
  importResult = signal<ImportResultDto | null>(null);
  selectedFile = signal<File | null>(null);

  // ── Form ───────────────────────────────────────────────────────────────────
  form = { supplierId: 0, docNumber: '', receiveDate: new Date().toISOString().split('T')[0] };

  mapping: MappingForm & Record<string, number> = {
    dataStartRow: 2,
    colName: 0, colType: 0, colQty: 0,
    colUnit: 0, colExp: 0,  colPrice: 0
  };

  // ── Column mapping field definitions ───────────────────────────────────────
  readonly mappingFields = [
    { key: 'colName',  label: 'Назва інгредієнта',  required: true  },
    { key: 'colType',  label: 'Тип',                required: true  },
    { key: 'colQty',   label: 'Кількість',           required: true  },
    { key: 'colUnit',  label: 'Одиниця виміру',      required: true  },
    { key: 'colExp',   label: 'Дата закінчення',     required: false },
    { key: 'colPrice', label: 'Ціна за одиницю',     required: false },
  ];

  // A–Z options shown when file not yet parsed
  readonly defaultColOptions = Array.from({length: 10}, (_, i) => ({
    v: i + 1, l: String.fromCharCode(65 + i)
  }));

  // ── Computed ───────────────────────────────────────────────────────────────
  fileFormat   = signal<FileFormat>(null);
  fileFormatIcon = computed(() => this.fileFormat() === 'xlsx' ? '📗' : '📄');
  fileSizeStr  = computed(() => {
    const f = this.selectedFile();
    if (!f) return '';
    if (f.size < 1024) return `${f.size} Б`;
    if (f.size < 1024 * 1024) return `${(f.size / 1024).toFixed(1)} КБ`;
    return `${(f.size / 1024 / 1024).toFixed(2)} МБ`;
  });

  newCount     = computed(() => this.preview()?.rows.filter(r => r.isNew && !r.error).length ?? 0);
  availableColumns = computed(() => this.preview()?.columns ?? []);
  suggestedMapping = computed(() => this.preview()?.suggestedMapping ?? null);

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getSuppliers().subscribe(s => {
      this.suppliers.set(s);
      if (s.length) this.form.supplierId = s[0].supplierId;
    });
  }

  // ── File handling ──────────────────────────────────────────────────────────
  onDrop(e: DragEvent) {
    e.preventDefault();
    this.isDragging.set(false);
    const file = e.dataTransfer?.files[0];
    if (file) this.handleFile(file);
  }

  onFileSelected(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (file) this.handleFile(file);
  }

  handleFile(file: File) {
    const ext = file.name.split('.').pop()?.toLowerCase();
    if (ext !== 'xlsx' && ext !== 'csv') {
      alert('Дозволені формати: .xlsx, .csv');
      return;
    }
    this.selectedFile.set(file);
    this.fileFormat.set(ext as FileFormat);
    this.preview.set(null);
    this.step.set('configure');

    // Застосовуємо дефолтний mapping залежно від формату
    this.mapping.dataStartRow = 2;
    this.mapping.colName = 0;
    this.mapping.colType = 0;
    this.mapping.colQty  = 0;
    this.mapping.colUnit = 0;
    this.mapping.colExp  = 0;
    this.mapping.colPrice = 0;
  }

  // ── Parse ──────────────────────────────────────────────────────────────────
  parseFile() {
    const file = this.selectedFile();
    if (!file) return;
    this.isParsing.set(true);

    const m = this.mapping;
    this.api.importPreview(file, m.dataStartRow, m.colName, m.colType, m.colQty, m.colUnit, m.colExp, m.colPrice)
      .subscribe({
        next: dto => {
          this.preview.set(dto);
          this.step.set('preview');
          this.isParsing.set(false);

          // Якщо є авто-детекція — підставляємо в форму (лише якщо = 0)
          const s = dto.suggestedMapping;
          if (s) {
            if (!m.colName  && s.colName)  m.colName  = s.colName;
            if (!m.colType  && s.colType)  m.colType  = s.colType;
            if (!m.colQty   && s.colQuantity) m.colQty = s.colQuantity;
            if (!m.colUnit  && s.colUnit)  m.colUnit  = s.colUnit;
            if (!m.colExp   && s.colExpiration) m.colExp = s.colExpiration;
            if (!m.colPrice && s.colUnitPrice)  m.colPrice = s.colUnitPrice;
          }
        },
        error: err => {
          alert(err?.error?.message ?? 'Помилка парсингу файлу.');
          this.isParsing.set(false);
        }
      });
  }

  // ── Commit ─────────────────────────────────────────────────────────────────
  commitImport() {
    const p = this.preview();
    if (!p || p.errorCount > 0) return;
    this.isCommitting.set(true);

    const rows: ExcelRowCommit[] = p.rows
      .filter(r => !r.error)
      .map(r => ({
        ingredientName: r.ingredientName,
        ingredientType: r.ingredientType,
        quantity:       r.quantity,
        unit:           r.unit,
        unitPrice:      r.unitPrice,
        expirationDate: r.expirationDate ? r.expirationDate : null,
        ingredientId:   r.ingredientId
      }));

    const req = {
      supplierId:  this.form.supplierId,
      docNumber:   this.form.docNumber || `IMP-${Date.now()}`,
      receiveDate: this.form.receiveDate || null,
      rows
    };

    this.api.importCommit(req, this.selectedFile()?.name ?? 'import').subscribe({
      next: result => {
        this.importResult.set(result as ImportResultDto);
        this.step.set('success');
        this.isCommitting.set(false);
      },
      error: err => {
        alert(err?.error?.message ?? 'Помилка збереження.');
        this.isCommitting.set(false);
      }
    });
  }

  // ── Utils ──────────────────────────────────────────────────────────────────
  downloadTemplate() {
    this.api.importTemplate().subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const a   = document.createElement('a');
      a.href = url; a.download = 'supply_invoice_template.xlsx';
      a.click(); URL.revokeObjectURL(url);
    });
  }

  switchToLogs() {
    this.activeTab.set('logs');
    this.logsLoading.set(true);
    this.api.getImportLogs().subscribe({
      next:  l => { this.logs.set(l); this.logsLoading.set(false); },
      error: () => this.logsLoading.set(false)
    });
  }

  resetAll() {
    this.step.set('drop'); this.preview.set(null);
    this.selectedFile.set(null); this.importResult.set(null);
    this.fileFormat.set(null);
  }

  getSuggested(key: string): number {
    const s = this.suggestedMapping();
    if (!s) return 0;
    const map: Record<string,number> = {
      colName:  s.colName,  colType:  s.colType,
      colQty:   s.colQuantity, colUnit: s.colUnit,
      colExp:   s.colExpiration, colPrice: s.colUnitPrice
    };
    return map[key] ?? 0;
  }

  colLetter(n: number): string {
    if (n <= 0) return '?';
    let s = '';
    while (n > 0) { s = String.fromCharCode(64 + ((n - 1) % 26 + 1)) + s; n = Math.floor((n - 1) / 26); }
    return s;
  }

  typeLabel(t: string): string {
    const m: Record<string,string> = {
      Malt:'🌾 Солод', Hop:'🟢 Хміль', Yeast:'🔬 Дріжджі',
      Additive:'⚗️ Добавка', Water:'💧 Вода'
    };
    return m[t] ?? t;
  }

  fileIcon(name: string): string {
    return name.endsWith('.csv') ? '📄' : '📗';
  }
}
