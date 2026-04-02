// ──────────────────────────────────────────────────────────────────────────────
// BREWERY ERP — TypeScript Models (відповідають C# Entities + DTOs)
// ──────────────────────────────────────────────────────────────────────────────

// ---------- Enums ----------
export type IngredientType = 'Malt' | 'Hop' | 'Yeast' | 'Additive' | 'Water';
export type BatchStatus    = 'Brewing' | 'Fermenting' | 'Completed' | 'Failed';
export type PackagingType  = 'Keg_30L' | 'Keg_50L' | 'Bottle_0_5L';
export type OrderStatus    = 'New' | 'Reserved' | 'Shipped' | 'Paid';

// ---------- Auth ----------
export interface LoginRequest  { email: string; password: string; }
export interface RegisterRequest { email: string; password: string; fullName: string; role: string; }
export interface AuthResponse  {
  token:     string;
  email:     string;
  fullName:  string;
  roles:     string[];   // масив ролей з бекенду
  role:      string;     // перша роль (зручне поле)
  expiresAt: string;
}

// ---------- Staff (Users) ----------
export interface StaffDto {
  id:       string;
  email:    string;
  fullName: string;
  role:     string;
  isLocked: boolean;
}

// ---------- BeerStyle ----------
export interface BeerStyle {
  styleId:      number;
  name:         string;
  description?: string | null;
  minAbv?:      number | null;
  maxAbv?:      number | null;
  minIbu?:      number | null;
  maxIbu?:      number | null;
  minSrm?:      number | null;
  maxSrm?:      number | null;
  // deprecated fields (backwards compat)
  targetSrm?:   number | null;
  targetAbv?:   number | null;
}

// ---------- Ingredient ----------
export interface Ingredient {
  ingredientId: number;
  name:         string;
  type:         IngredientType;
  totalStock:   number;
  unit:         string;
}

// ---------- Supplier ----------
export interface Supplier {
  supplierId: number;
  name:       string;
  edrpou?:    string | null;
}

// ---------- SupplyInvoice ----------
export interface InvoiceItemDto {
  ingredientId:    number;
  ingredientName?: string;
  quantity:        number;
  expirationDate?: string | null;
}

export interface SupplyInvoice {
  invoiceId:   number;
  supplierId:  number;
  supplierName?: string;
  docNumber:   string;
  receiveDate: string;
  items:       InvoiceItemDto[];
}

// ---------- Recipe ----------
export interface RecipeItemDto {
  ingredientId:   number;
  ingredientName?: string;
  amount:         number;
}

export interface Recipe {
  recipeId:    number;
  styleId:     number;
  styleName?:  string;
  versionName: string;
  isActive:    boolean;
  items:       RecipeItemDto[];
}

// ---------- Batch ----------
export interface Batch {
  batchId:   number;
  recipeId:  number;
  recipeName?: string;
  status:    BatchStatus;
  startDate: string;
  actualAbv?: number | null;
  actualSrm?: number | null;
}

// ---------- ProductSku ----------
export interface ProductSku {
  skuId:           number;
  batchId:         number;
  batchInfo?:      string;
  packagingType:   PackagingType;
  price:           number;
  quantityInStock: number;
}

// ---------- Client ----------
export interface Client {
  clientId: number;
  name:     string;
  phone?:   string | null;
}

// ---------- SalesOrder ----------
export interface OrderItemDto {
  skuId:         number;
  skuInfo?:      string;
  quantity:      number;
  priceAtMoment: number;
}

export interface SalesOrder {
  orderId:   number;
  clientId:  number;
  clientName?: string;
  orderDate: string;
  status:    OrderStatus;
  items:     OrderItemDto[];
  totalAmount?: number;
}

// ---------- Dashboard stats (computed on frontend) ----------
export interface DashboardStats {
  totalBatches:       number;
  activeBatches:      number;
  totalOrders:        number;
  pendingOrders:      number;
  totalIngredients:   number;
  lowStockCount:      number;
  totalRecipes:       number;
  activeRecipes:      number;
}
