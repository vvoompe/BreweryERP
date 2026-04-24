import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { RouterLink }                from '@angular/router';
import { ApiService }                from '../../core/api.service';
import { AuthService }               from '../../core/auth.service';
import { DashboardStats }            from '../../core/models';
import { forkJoin, of }              from 'rxjs';
import { catchError }                from 'rxjs/operators';
import { Chart, registerables }      from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="animate-in">
      <!-- Page Header -->
      <div class="page-header flex-between" style="margin-bottom: 24px;">
        <div>
          <h2>📊 Дашборд</h2>
          <p>Огляд роботи броварні — {{ today() }}</p>
        </div>
        <div class="flex-center gap-sm">
          <span class="badge badge-active">Live</span>
          <button class="btn btn-ghost btn-sm" (click)="loadData()" id="btn-refresh">
            🔄 Оновити
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="loading"><div class="spinner"></div> Завантаження...</div>
      }

      @if (errorMsg()) {
        <div class="alert alert-danger" style="
          background: rgba(239,68,68,0.12);
          border: 1px solid rgba(239,68,68,0.4);
          border-radius: var(--radius);
          padding: 12px 16px;
          color: #f87171;
          margin-bottom: 20px;
          display: flex;
          align-items: center;
          gap: 10px;
        ">
          ⚠️ {{ errorMsg() }}
          <button class="btn btn-ghost btn-sm" style="margin-left:auto;" (click)="loadData()">Спробувати знову</button>
        </div>
      }

      @if (!loading()) {
        <!-- Stats Cards -->
        <div class="stats-grid">
          <div class="card stat-card" routerLink="/batches">
            <div class="stat-icon">🍺</div>
            <div class="stat-label">Всього партій</div>
            <div class="stat-value">{{ stats().totalBatches }}</div>
            <div class="stat-sub">{{ stats().activeBatches }} активних</div>
          </div>

          <div class="card stat-card" routerLink="/orders">
            <div class="stat-icon">🛒</div>
            <div class="stat-label">Замовлення</div>
            <div class="stat-value">{{ stats().totalOrders }}</div>
            <div class="stat-sub">{{ stats().pendingOrders }} в обробці</div>
          </div>

          <div class="card stat-card" routerLink="/inventory">
            <div class="stat-icon">🌾</div>
            <div class="stat-label">Інгредієнти</div>
            <div class="stat-value">{{ stats().totalIngredients }}</div>
            <div class="stat-sub"
              [class.text-danger]="stats().lowStockCount > 0">
              {{ stats().lowStockCount }} з низьким запасом
            </div>
          </div>

          <div class="card stat-card" routerLink="/recipes">
            <div class="stat-icon">📋</div>
            <div class="stat-label">Рецепти</div>
            <div class="stat-value">{{ stats().totalRecipes }}</div>
            <div class="stat-sub">{{ stats().activeRecipes }} активних</div>
          </div>
        </div>

        <!-- Charts Row -->
        <div class="dashboard-panels" style="margin-bottom: var(--space-md);">
          <div class="card panel-card" style="padding: var(--space-md);">
            <h3 style="margin-bottom: 1rem; font-size: 1rem;">📈 Динаміка продажів (останні 7 днів)</h3>
            <div style="position: relative; height: 260px; width: 100%;">
              <canvas id="salesChart"></canvas>
            </div>
          </div>
          <div class="card panel-card" style="padding: var(--space-md);">
            <h3 style="margin-bottom: 1rem; font-size: 1rem;">🌾 Розподіл сировини</h3>
            <div style="position: relative; height: 260px; width: 100%; display: flex; justify-content: center;">
              <canvas id="inventoryChart"></canvas>
            </div>
          </div>
        </div>

        <!-- Quick status panels -->
        <div class="dashboard-panels">

          <!-- Recent Batches -->
          <div class="card panel-card">
            <div class="panel-header flex-between">
              <h3>🍺 Останні партії</h3>
              <a routerLink="/batches" class="text-amber" style="font-size:0.8rem">Всі →</a>
            </div>
            @if (recentBatches().length === 0) {
              <div class="empty-state" style="padding: 24px;">
                <div class="empty-icon">🍺</div>
                <p>Немає партій</p>
              </div>
            }
            @for (b of recentBatches(); track b.batchId) {
              <div class="panel-row">
                <div>
                  <div class="panel-row-title">{{ b.recipeName ?? 'Рецепт #' + b.recipeId }}</div>
                  <div class="panel-row-sub">{{ b.startDate | date:'dd.MM.yyyy' }}</div>
                </div>
                <span class="badge" [ngClass]="batchBadge(b.status)">{{ b.status }}</span>
              </div>
            }
          </div>

          <!-- Recent Orders -->
          <div class="card panel-card">
            <div class="panel-header flex-between">
              <h3>🛒 Останні замовлення</h3>
              <a routerLink="/orders" class="text-amber" style="font-size:0.8rem">Всі →</a>
            </div>
            @if (recentOrders().length === 0) {
              <div class="empty-state" style="padding: 24px;">
                <div class="empty-icon">🛒</div>
                <p>Немає замовлень</p>
              </div>
            }
            @for (o of recentOrders(); track o.orderId) {
              <div class="panel-row">
                <div>
                  <div class="panel-row-title">{{ o.clientName ?? 'Клієнт #' + o.clientId }}</div>
                  <div class="panel-row-sub">{{ o.orderDate | date:'dd.MM.yyyy' }}</div>
                </div>
                <span class="badge" [ngClass]="orderBadge(o.status)">{{ o.status }}</span>
              </div>
            }
          </div>

          <!-- Low-stock Ingredients -->
          <div class="card panel-card">
            <div class="panel-header flex-between">
              <h3>⚠️ Низький запас</h3>
              <a routerLink="/inventory" class="text-amber" style="font-size:0.8rem">Склад →</a>
            </div>
            @if (lowStockIngredients().length === 0) {
              <div style="padding: 16px; color: var(--status-completed); font-size:.875rem; text-align: center;">
                ✅ Всі запаси в нормі
              </div>
            }
            @for (i of lowStockIngredients(); track i.ingredientId) {
              <div class="panel-row">
                <div>
                  <div class="panel-row-title">{{ i.name }}</div>
                  <div class="panel-row-sub">{{ i.type }}</div>
                </div>
                <span class="text-danger" style="font-weight:700;">
                  {{ i.totalStock }} {{ i.unit }}
                </span>
              </div>
            }
          </div>
        </div>

        <!-- Activity Log -->
        <div class="dashboard-panels" style="margin-top: var(--space-md);">
          <!-- Recent Activity -->
          <div class="card panel-card" style="grid-column: span 2;">
            <div class="panel-header flex-between">
              <h3>⏱ Журнал активності (Live)</h3>
            </div>
            @if (activities().length === 0) {
              <div class="empty-state" style="padding: 24px;">
                <p>Немає нових подій</p>
              </div>
            }
            <div style="max-height: 400px; overflow-y: auto;">
              @for (a of activities(); track a.logId) {
                <div class="panel-row" style="border-left: 4px solid var(--accent);">
                  <div style="width: 100%;">
                    <div class="flex-between">
                      <span class="panel-row-title" style="color: var(--text-color);">{{ a.action }}</span>
                      <span class="panel-row-sub">{{ a.timestamp | date:'short' }}</span>
                    </div>
                    <div class="panel-row-sub" style="margin-top: 4px;">{{ a.details }}</div>
                    <div class="panel-row-sub" style="margin-top: 4px; font-weight: 600;">👤 {{ a.userName }}</div>
                  </div>
                </div>
              }
            </div>
          </div>
          
        </div>
      }
    </div>
  `,
  styles: [`
    .stats-grid .card {
      cursor: pointer;
    }
    .stat-sub {
      font-size: 0.78rem;
      color: var(--text-muted);
      margin-top: 2px;
    }
    .dashboard-panels {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: var(--space-md);
    }
    .panel-card { padding: 0; overflow: hidden; }
    .panel-header {
      padding: var(--space-md) var(--space-lg);
      border-bottom: 1px solid var(--border-color);
    }
    .panel-header h3 { font-size: 0.95rem; }
    .panel-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px var(--space-lg);
      border-bottom: 1px solid var(--border-color);
      transition: background var(--transition);
    }
    .panel-row:last-child { border-bottom: none; }
    .panel-row:hover { background: var(--bg-card-hover); }
    .panel-row-title { font-size: 0.875rem; font-weight: 500; }
    .panel-row-sub { font-size: 0.75rem; color: var(--text-muted); margin-top: 1px; }
  `]
})
export class DashboardComponent implements OnInit {
  loading        = signal(true);
  errorMsg       = signal<string | null>(null); // ← НОВИЙ signal для помилок
  stats          = signal<DashboardStats>({ totalBatches:0, activeBatches:0, totalOrders:0, pendingOrders:0, totalIngredients:0, lowStockCount:0, totalRecipes:0, activeRecipes:0 });
  recentBatches  = signal<any[]>([]);
  recentOrders   = signal<any[]>([]);
  lowStockIngredients = signal<any[]>([]);
  activities     = signal<any[]>([]);

  private salesChart: any;
  private inventoryChart: any;

  constructor(private api: ApiService, public auth: AuthService) {}

  ngOnInit() { this.loadData(); }

  today(): string {
    return new Date().toLocaleDateString('uk-UA', { weekday:'long', year:'numeric', month:'long', day:'numeric' });
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMsg.set(null); // скидаємо попередню помилку

    // ✅ ВИПРАВЛЕННЯ: кожен запит обгорнутий у catchError — один збій не вбиває весь дашборд
    forkJoin({
      batches:     this.api.getBatches().pipe(catchError(() => of([]))),
      orders:      this.api.getOrders().pipe(catchError(() => of([]))),
      ingredients: this.api.getIngredients().pipe(catchError(() => of([]))),
      recipes:     this.api.getRecipes().pipe(catchError(() => of([]))),
      activities:  this.api.getActivityLogs(10).pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ batches, orders, ingredients, recipes, activities }) => {
        this.stats.set({
          totalBatches:     batches.length,
          activeBatches:    batches.filter((b: any) => b.status === 'Brewing' || b.status === 'Fermenting').length,
          totalOrders:      orders.length,
          pendingOrders:    orders.filter((o: any) => o.status === 'New' || o.status === 'Reserved').length,
          totalIngredients: ingredients.length,
          lowStockCount:    ingredients.filter((i: any) => i.totalStock < 10).length,
          totalRecipes:     recipes.length,
          activeRecipes:    recipes.filter((r: any) => r.isActive).length,
        });
        this.recentBatches.set(batches.slice(-5).reverse());
        this.recentOrders.set(orders.slice(-5).reverse());
        this.lowStockIngredients.set(ingredients.filter((i: any) => i.totalStock < 10));
        this.activities.set(activities);

        // Якщо всі масиви порожні — можливо бекенд не відповідає, попереджаємо
        if (batches.length === 0 && orders.length === 0 && ingredients.length === 0) {
          this.errorMsg.set('Не вдалося завантажити дані. Перевірте чи запущений бекенд на порту 5000.');
        }

        this.loading.set(false);
        setTimeout(() => this.initCharts(orders, ingredients), 100);
      },
      // ✅ тепер цей error спрацює лише якщо сам forkJoin впав (не повинно, бо catchError вище)
      error: (err) => {
        this.errorMsg.set('Помилка завантаження: ' + (err?.message ?? 'невідома помилка'));
        this.loading.set(false);
      }
    });
  }

  batchBadge(status: string): string {
    const map: Record<string,string> = { Brewing:'badge-brewing', Fermenting:'badge-fermenting', Completed:'badge-completed', Failed:'badge-failed' };
    return map[status] ?? 'badge-inactive';
  }

  orderBadge(status: string): string {
    const map: Record<string,string> = { New:'badge-new', Reserved:'badge-reserved', Shipped:'badge-shipped', Paid:'badge-paid' };
    return map[status] ?? 'badge-inactive';
  }

  initCharts(orders: any[], ingredients: any[]) {
    // 1. Динаміка продажів
    if (this.salesChart) this.salesChart.destroy();
    
    const dates = [...Array(7)].map((_, i) => {
      const d = new Date(); d.setDate(d.getDate() - i);
      return d.toISOString().split('T')[0];
    }).reverse();

    const salesData = dates.map(date => {
      return orders.filter(o => o.orderDate.startsWith(date))
                   .reduce((sum, o) => sum + (o.totalAmount || 0), 0);
    });

    const ctxSales = document.getElementById('salesChart') as HTMLCanvasElement;
    if (ctxSales) {
      this.salesChart = new Chart(ctxSales, {
        type: 'bar',
        data: {
          labels: dates.map(d => d.substring(5).replace('-', '.')),
          datasets: [{
            label: 'Виторг (₴)',
            data: salesData,
            backgroundColor: 'rgba(234, 179, 8, 0.7)',
            borderColor: 'rgba(234, 179, 8, 1)',
            borderWidth: 1,
            borderRadius: 4
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          scales: { y: { beginAtZero: true, grid: { color: 'rgba(255,255,255,0.05)' } }, x: { grid: { display: false } } },
          plugins: { legend: { display: false } }
        }
      });
    }

    // 2. Розподіл сировини
    if (this.inventoryChart) this.inventoryChart.destroy();

    const groupedIngr = ingredients.reduce((acc, i) => {
      acc[i.type] = (acc[i.type] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    const ctxInv = document.getElementById('inventoryChart') as HTMLCanvasElement;
    if (ctxInv) {
      this.inventoryChart = new Chart(ctxInv, {
        type: 'doughnut',
        data: {
          labels: Object.keys(groupedIngr),
          datasets: [{
            data: Object.values(groupedIngr),
            backgroundColor: ['#eab308', '#22c55e', '#3b82f6', '#ec4899', '#94a3b8'],
            borderWidth: 0
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { position: 'right', labels: { color: '#94a3b8', font: { size: 11 } } }
          },
          cutout: '70%'
        }
      });
    }
  }
}