import { Component, OnInit, signal } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { RouterLink }                from '@angular/router';
import { ApiService }                from '../../core/api.service';
import { AuthService }               from '../../core/auth.service';
import { DashboardStats }            from '../../core/models';
import { forkJoin }                  from 'rxjs';

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
  stats          = signal<DashboardStats>({ totalBatches:0, activeBatches:0, totalOrders:0, pendingOrders:0, totalIngredients:0, lowStockCount:0, totalRecipes:0, activeRecipes:0 });
  recentBatches  = signal<any[]>([]);
  recentOrders   = signal<any[]>([]);
  lowStockIngredients = signal<any[]>([]);

  constructor(private api: ApiService, public auth: AuthService) {}

  ngOnInit() { this.loadData(); }

  today(): string {
    return new Date().toLocaleDateString('uk-UA', { weekday:'long', year:'numeric', month:'long', day:'numeric' });
  }

  loadData(): void {
    this.loading.set(true);
    forkJoin({
      batches:     this.api.getBatches(),
      orders:      this.api.getOrders(),
      ingredients: this.api.getIngredients(),
      recipes:     this.api.getRecipes()
    }).subscribe({
      next: ({ batches, orders, ingredients, recipes }) => {
        this.stats.set({
          totalBatches:     batches.length,
          activeBatches:    batches.filter(b => b.status === 'Brewing' || b.status === 'Fermenting').length,
          totalOrders:      orders.length,
          pendingOrders:    orders.filter(o => o.status === 'New' || o.status === 'Reserved').length,
          totalIngredients: ingredients.length,
          lowStockCount:    ingredients.filter(i => i.totalStock < 10).length,
          totalRecipes:     recipes.length,
          activeRecipes:    recipes.filter(r => r.isActive).length,
        });
        this.recentBatches.set(batches.slice(-5).reverse());
        this.recentOrders.set(orders.slice(-5).reverse());
        this.lowStockIngredients.set(ingredients.filter(i => i.totalStock < 10));
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
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
}
