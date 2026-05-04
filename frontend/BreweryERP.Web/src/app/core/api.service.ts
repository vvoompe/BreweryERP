import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  BeerStyle, Ingredient, Supplier, SupplyInvoice,
  Recipe, Batch, ProductSku, Client, SalesOrder,
  StaffDto, AuthResponse, RegisterRequest,
  ExcelPreviewDto, ExcelImportRequest, ImportLog,
  ImportResultDto, CreateSalesOrderRequest
} from './models';

// ─── Змінити якщо backend запущений на іншому порту ─────────────────────────
const BASE = 'http://localhost:5000/api';

@Injectable({ providedIn: 'root' })
export class ApiService {

  constructor(private http: HttpClient) {}

  // ── BeerStyles ─────────────────────────────────────────────────────────────
  getStyles():                    Observable<BeerStyle[]>  { return this.http.get<BeerStyle[]>(`${BASE}/beerstyles`); }
  createStyle(b: Partial<BeerStyle>): Observable<BeerStyle>{ return this.http.post<BeerStyle>(`${BASE}/beerstyles`, b); }
  updateStyle(id: number, b: Partial<BeerStyle>): Observable<void> { return this.http.put<void>(`${BASE}/beerstyles/${id}`, b); }
  deleteStyle(id: number):        Observable<void>         { return this.http.delete<void>(`${BASE}/beerstyles/${id}`); }

  // ── Ingredients ────────────────────────────────────────────────────────────
  getIngredients():               Observable<Ingredient[]> { return this.http.get<Ingredient[]>(`${BASE}/ingredients`); }
  createIngredient(i: Partial<Ingredient>): Observable<Ingredient> { return this.http.post<Ingredient>(`${BASE}/ingredients`, i); }
  updateIngredient(id: number, i: Partial<Ingredient>): Observable<void> { return this.http.put<void>(`${BASE}/ingredients/${id}`, i); }
  deleteIngredient(id: number):   Observable<void>         { return this.http.delete<void>(`${BASE}/ingredients/${id}`); }

  // ── Suppliers ──────────────────────────────────────────────────────────────
  getSuppliers(): Observable<Supplier[]> { return this.http.get<Supplier[]>(`${BASE}/suppliers`); }
  getSupplier(id: number): Observable<Supplier> { return this.http.get<Supplier>(`${BASE}/suppliers/${id}`); }
  createSupplier(s: Partial<Supplier>): Observable<Supplier> { return this.http.post<Supplier>(`${BASE}/suppliers`, s); }
  updateSupplier(id: number, s: Partial<Supplier>): Observable<Supplier> { return this.http.put<Supplier>(`${BASE}/suppliers/${id}`, s); }
  deleteSupplier(id: number): Observable<void> { return this.http.delete<void>(`${BASE}/suppliers/${id}`); }
  getSupplierInvoices(id: number): Observable<SupplyInvoice[]> { return this.http.get<SupplyInvoice[]>(`${BASE}/suppliers/${id}/invoices`); }

  // ── SupplyInvoices ─────────────────────────────────────────────────────────
  getInvoices():                  Observable<SupplyInvoice[]>  { return this.http.get<SupplyInvoice[]>(`${BASE}/supplyinvoices`); }
  getInvoiceById(id: number):     Observable<SupplyInvoice>    { return this.http.get<SupplyInvoice>(`${BASE}/supplyinvoices/${id}`); }
  createInvoice(inv: Partial<SupplyInvoice>): Observable<SupplyInvoice> { return this.http.post<SupplyInvoice>(`${BASE}/supplyinvoices`, inv); }
  deleteInvoice(id: number):      Observable<void>             { return this.http.delete<void>(`${BASE}/supplyinvoices/${id}`); }

  // ── Recipes ────────────────────────────────────────────────────────────────
  getRecipes():                   Observable<Recipe[]>     { return this.http.get<Recipe[]>(`${BASE}/recipes`); }
  createRecipe(r: Partial<Recipe>): Observable<Recipe>     { return this.http.post<Recipe>(`${BASE}/recipes`, r); }
  updateRecipe(id: number, r: Partial<Recipe>): Observable<void> { return this.http.put<void>(`${BASE}/recipes/${id}`, r); }
  deleteRecipe(id: number):       Observable<void>         { return this.http.delete<void>(`${BASE}/recipes/${id}`); }

  // ── Batches ────────────────────────────────────────────────────────────────
  getBatches():                   Observable<Batch[]>      { return this.http.get<Batch[]>(`${BASE}/batches`); }
  createBatch(b: Partial<Batch>): Observable<Batch>        { return this.http.post<Batch>(`${BASE}/batches`, b); }
  updateBatch(id: number, b: Partial<Batch>): Observable<void> { return this.http.put<void>(`${BASE}/batches/${id}`, b); }
  deleteBatch(id: number):        Observable<void>         { return this.http.delete<void>(`${BASE}/batches/${id}`); }

  // ── ProductSkus ────────────────────────────────────────────────────────────
  getSkus():                      Observable<ProductSku[]> { return this.http.get<ProductSku[]>(`${BASE}/productskus`); }
  createSku(s: Partial<ProductSku>): Observable<ProductSku> { return this.http.post<ProductSku>(`${BASE}/productskus`, s); }
  updateSku(id: number, s: Partial<ProductSku>): Observable<void> { return this.http.put<void>(`${BASE}/productskus/${id}`, s); }
  deleteSku(id: number):          Observable<void>         { return this.http.delete<void>(`${BASE}/productskus/${id}`); }

  // ── Activity Logs ──────────────────────────────────────────────────────────
  getActivityLogs(count: number = 20): Observable<any[]> {
    return this.http.get<any[]>(`${BASE}/activitylogs?count=${count}`);
  }

  // ── Clients ────────────────────────────────────────────────────────────────
  getClients():                   Observable<Client[]>     { return this.http.get<Client[]>(`${BASE}/clients`); }
  createClient(c: Partial<Client>): Observable<Client>     { return this.http.post<Client>(`${BASE}/clients`, c); }
  updateClient(id: number, c: Partial<Client>): Observable<void> { return this.http.put<void>(`${BASE}/clients/${id}`, c); }
  deleteClient(id: number):       Observable<void>         { return this.http.delete<void>(`${BASE}/clients/${id}`); }

  // ── SalesOrders ────────────────────────────────────────────────────────────
  getOrders():                                Observable<SalesOrder[]>  { return this.http.get<SalesOrder[]>(`${BASE}/salesorders`); }
  getOrder(id: number):                       Observable<SalesOrder>    { return this.http.get<SalesOrder>(`${BASE}/salesorders/${id}`); }
  createOrder(o: CreateSalesOrderRequest):    Observable<SalesOrder>    { return this.http.post<SalesOrder>(`${BASE}/salesorders`, o); }
  updateOrderStatus(id: number, status: string): Observable<SalesOrder> { return this.http.patch<SalesOrder>(`${BASE}/salesorders/${id}/status`, { status }); }

  // ── Staff / Users (Admin only) ─────────────────────────────────────────────
  getStaff():                                Observable<StaffDto[]>   { return this.http.get<StaffDto[]>(`${BASE}/users`); }
  registerStaff(r: RegisterRequest):         Observable<AuthResponse> { return this.http.post<AuthResponse>(`${BASE}/auth/register`, r); }
  updateStaffRole(id: string, role: string): Observable<void>         { return this.http.put<void>(`${BASE}/users/${id}/role`, { role }); }
  toggleStaffLock(id: string):              Observable<void>          { return this.http.put<void>(`${BASE}/users/${id}/lock`, {}); }
  deleteStaff(id: string):                  Observable<void>          { return this.http.delete<void>(`${BASE}/users/${id}`); }

  // ── Excel Import ───────────────────────────────────────────────────────────
  importPreview(
    file: File,
    dataStartRow = 2,
    colName  = 0, colType  = 0, colQty   = 0,
    colUnit  = 0, colExp   = 0, colPrice = 0
  ): Observable<ExcelPreviewDto> {
    const fd = new FormData();
    fd.append('file', file);
    const params = new URLSearchParams({
      dataStartRow: String(dataStartRow),
      colName:  String(colName),  colType:  String(colType),
      colQty:   String(colQty),   colUnit:  String(colUnit),
      colExp:   String(colExp),   colPrice: String(colPrice)
    });
    return this.http.post<ExcelPreviewDto>(`${BASE}/import/preview?${params}`, fd);
  }
  importCommit(req: ExcelImportRequest, fileName: string): Observable<ImportResultDto> {
    return this.http.post<ImportResultDto>(
      `${BASE}/import/commit?fileName=${encodeURIComponent(fileName)}`, req);
  }
  importTemplate(): Observable<Blob> {
    return this.http.get(`${BASE}/import/template`, { responseType: 'blob' });
  }
  getImportLogs(): Observable<ImportLog[]> {
    return this.http.get<ImportLog[]>(`${BASE}/import/logs`);
  }
}