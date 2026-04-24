using Microsoft.AspNetCore.Identity;

namespace BreweryERP.Api.Models;

// ──────────────────────────────────────────────────────────────────────────────
// ENUMS (відповідають MySQL ENUM-полям)
// ──────────────────────────────────────────────────────────────────────────────

public enum IngredientType
{
    Malt, Hop, Yeast, Additive, Water
}

public enum BatchStatus
{
    Brewing, Fermenting, Completed, Failed
}

public enum PackagingType
{
    Keg_30L, Keg_50L, Bottle_0_5L   // 'Bottle_0.5L' у БД через HasConversion
}

public enum OrderStatus
{
    New, Reserved, Shipped, Paid
}

// ──────────────────────────────────────────────────────────────────────────────
// IDENTITY USER
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Розширений користувач додатку (ASP.NET Core Identity).</summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────────────────────────
// БЛОК 1: ДОВІДНИКИ
// ──────────────────────────────────────────────────────────────────────────────

public class BeerStyle
{
    public int      StyleId     { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string?  Description { get; set; }

    // BJCP параметри (діапазони)
    public decimal? MinAbv { get; set; }
    public decimal? MaxAbv { get; set; }
    public int?     MinIbu { get; set; }
    public int?     MaxIbu { get; set; }
    public int?     MinSrm { get; set; }
    public int?     MaxSrm { get; set; }

    // Deprecated: залишаємо для сумісності
    public int?     TargetSrm { get; set; }
    public decimal? TargetAbv { get; set; }

    // Navigation
    public ICollection<Recipe> Recipes { get; set; } = [];
}

public class Ingredient
{
    public int            IngredientId { get; set; }
    public string         Name         { get; set; } = string.Empty;
    public IngredientType Type         { get; set; }
    public decimal        TotalStock   { get; set; }
    public string         Unit         { get; set; } = "kg";
    public decimal        AverageCost  { get; set; } // ★ NEW: Середня вартість за одиницю

    // Navigation (зворотні зв'язки для проміжних таблиць)
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = [];
    public ICollection<RecipeItem>  RecipeItems  { get; set; } = [];
}

// ──────────────────────────────────────────────────────────────────────────────
// БЛОК 2: ЗАКУПІВЛІ
// ──────────────────────────────────────────────────────────────────────────────

public class Supplier
{
    public int     SupplierId { get; set; }
    public string  Name       { get; set; } = string.Empty;
    public string? Edrpou     { get; set; }

    // Navigation
    public ICollection<SupplyInvoice> Invoices { get; set; } = [];
}

/// <summary>Master-таблиця накладної.</summary>
public class SupplyInvoice
{
    public int      InvoiceId   { get; set; }
    public int      SupplierId  { get; set; }
    public string   DocNumber   { get; set; } = string.Empty;
    public DateTime ReceiveDate { get; set; }

    // Navigation
    public Supplier            Supplier { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = [];
}

/// <summary>
/// Detail-рядок накладної.
/// ★ Composite Key: (InvoiceId, IngredientId) — налаштовано у Fluent API.
/// </summary>
public class InvoiceItem
{
    public int      InvoiceId      { get; set; }
    public int      IngredientId   { get; set; }
    public decimal  Quantity       { get; set; }
    public decimal? UnitPrice      { get; set; }  // ціна за одиницю (необов'.язкова)
    public DateOnly? ExpirationDate { get; set; }

    // Navigation
    public SupplyInvoice Invoice    { get; set; } = null!;
    public Ingredient    Ingredient { get; set; } = null!;
}

// ──────────────────────────────────────────────────────────────────────────────
// БЛОК 3: РЕЦЕПТУРА ТА ВИРОБНИЦТВО
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Рецепт з підтримкою ПАТЕРНУ "ВЕРСІОНУВАННЯ".
/// Поле IsActive = false означає застарілу версію.
/// </summary>
public class Recipe
{
    public int    RecipeId    { get; set; }
    public int    StyleId     { get; set; }
    public string VersionName { get; set; } = string.Empty;
    public bool   IsActive    { get; set; } = true;

    // Navigation
    public BeerStyle           Style   { get; set; } = null!;
    public ICollection<RecipeItem> Items   { get; set; } = [];
    public ICollection<Batch>  Batches { get; set; } = [];
}

/// <summary>
/// Рядок рецепту (M:M через проміжну таблицю).
/// ★ Composite Key: (RecipeId, IngredientId).
/// </summary>
public class RecipeItem
{
    public int     RecipeId     { get; set; }
    public int     IngredientId { get; set; }
    public decimal Amount       { get; set; }

    // Navigation
    public Recipe     Recipe     { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}

public class Batch
{
    public int         BatchId   { get; set; }
    public int         RecipeId  { get; set; }
    public BatchStatus Status    { get; set; } = BatchStatus.Brewing;
    public DateTime    StartDate { get; set; }
    public decimal?    ActualAbv { get; set; }
    public int?        ActualSrm { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal     EstimatedCost { get; set; } // ★ NEW: Собівартість партії

    // Navigation
    public Recipe                   Recipe      { get; set; } = null!;
    public ICollection<ProductSku>  ProductSkus { get; set; } = [];
}

// ──────────────────────────────────────────────────────────────────────────────
// БЛОК 4: СКЛАД ГОТОВОЇ ПРОДУКЦІЇ ТА ПРОДАЖІ
// ──────────────────────────────────────────────────────────────────────────────

public class ProductSku
{
    public int           SkuId           { get; set; }
    public int           BatchId         { get; set; }
    public PackagingType PackagingType   { get; set; }
    public decimal       Price           { get; set; }
    public decimal       UnitCost        { get; set; } // ★ NEW: Собівартість за 1 шт
    public int           QuantityInStock { get; set; }

    // Navigation
    public Batch                   Batch      { get; set; } = null!;
    public ICollection<OrderItem>  OrderItems { get; set; } = [];
}

public class Client
{
    public int     ClientId { get; set; }
    public string  Name     { get; set; } = string.Empty;
    public string? Phone    { get; set; }

    // Navigation
    public ICollection<SalesOrder> Orders { get; set; } = [];
}

/// <summary>Master-таблиця замовлення.</summary>
public class SalesOrder
{
    public int         OrderId   { get; set; }
    public int         ClientId  { get; set; }
    public DateTime    OrderDate { get; set; }
    public OrderStatus Status    { get; set; } = OrderStatus.New;

    // Navigation
    public Client                  Client { get; set; } = null!;
    public ICollection<OrderItem>  Items  { get; set; } = [];
}

/// <summary>
/// Detail-рядок замовлення.
/// ★ Composite Key: (OrderId, SkuId).
/// </summary>
public class OrderItem
{
    public int     OrderId       { get; set; }
    public int     SkuId         { get; set; }
    public int     Quantity      { get; set; }
    public decimal PriceAtMoment { get; set; }

    // Navigation
    public SalesOrder Order      { get; set; } = null!;
    public ProductSku ProductSku { get; set; } = null!;
}
// ──────────────────────────────────────────────────────────────────────────────
// БЛОК 5: ЖУРНАЛ ІМПОРТУ Excel
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Журнал завантажень Excel-файлів з накладними.
/// Створюється автоматично при кожному імпорті.
/// </summary>
public class ImportLog
{
    public int      ImportId   { get; set; }
    public string   FileName   { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string   ImportedBy { get; set; } = string.Empty; // email користувача
    public int?     InvoiceId  { get; set; }                 // null якщо імпорт провалився
    public string   Status     { get; set; } = string.Empty; // "Success" | "Failed"
    public string?  Error      { get; set; }
    public int      RowCount   { get; set; }

    // Navigation
    public SupplyInvoice? Invoice { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// БЛОК 6: ЖУРНАЛ АКТИВНОСТІ
// ──────────────────────────────────────────────────────────────────────────────

public class ActivityLog
{
    public int      LogId      { get; set; }
    public string   Action     { get; set; } = string.Empty;
    public string   EntityName { get; set; } = string.Empty;
    public int      EntityId   { get; set; }
    public string?  Details    { get; set; }
    public DateTime Timestamp  { get; set; } = DateTime.UtcNow;
    public string   UserName   { get; set; } = string.Empty;
}
