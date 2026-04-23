using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Data;

/// <summary>
/// Головний контекст бази даних. Успадковує IdentityDbContext для інтеграції
/// ASP.NET Core Identity (таблиці Users, Roles тощо).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // ──────────────────────────────────────────────
    // DbSet-и — по одному на кожну таблицю
    // EF Core ініціалізує їх через reflection, тому 'required' усуває CS8618
    // ──────────────────────────────────────────────
    public required DbSet<BeerStyle> BeerStyles { get; set; }
    public required DbSet<Ingredient> Ingredients { get; set; }
    public required DbSet<Supplier> Suppliers { get; set; }
    public required DbSet<SupplyInvoice> SupplyInvoices { get; set; }
    public required DbSet<InvoiceItem> InvoiceItems { get; set; }
    public required DbSet<Recipe> Recipes { get; set; }
    public required DbSet<RecipeItem> RecipeItems { get; set; }
    public required DbSet<Batch> Batches { get; set; }
    public required DbSet<ProductSku> ProductSkus { get; set; }
    public required DbSet<Client> Clients { get; set; }
    public required DbSet<SalesOrder> SalesOrders { get; set; }
    public required DbSet<OrderItem> OrderItems { get; set; }
    public required DbSet<ImportLog> ImportLogs { get; set; }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Обов'язково викликаємо базовий метод — він налаштовує таблиці Identity
        base.OnModelCreating(modelBuilder);

        // ══════════════════════════════════════════════════════════════════════
        // БЛОК 1: ДОВІДНИКИ
        // ══════════════════════════════════════════════════════════════════════

        // ── BeerStyle ──────────────────────────────────────────────────────
        modelBuilder.Entity<BeerStyle>(e =>
        {
            e.ToTable("beer_styles");
            e.HasKey(x => x.StyleId);
            e.Property(x => x.StyleId).HasColumnName("style_id").ValueGeneratedOnAdd();
            e.Property(x => x.Name)
             .HasColumnName("name")
             .HasMaxLength(100)
             .IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.TargetSrm).HasColumnName("target_srm");
            e.Property(x => x.TargetAbv)
             .HasColumnName("target_abv")
             .HasColumnType("decimal(4,2)");
        });

        // ── Ingredient ────────────────────────────────────────────────────
        modelBuilder.Entity<Ingredient>(e =>
        {
            e.ToTable("ingredients");
            e.HasKey(x => x.IngredientId);
            e.Property(x => x.IngredientId).HasColumnName("ingredient_id").ValueGeneratedOnAdd();
            e.Property(x => x.Name)
             .HasColumnName("name")
             .HasMaxLength(150)
             .IsRequired();
            // ENUM → рядок у БД, конвертація через HasConversion
            e.Property(x => x.Type)
             .HasColumnName("type")
             .HasConversion<string>()
             .IsRequired();
            e.Property(x => x.TotalStock)
             .HasColumnName("total_stock")
             .HasColumnType("decimal(10,3)")
             .HasDefaultValue(0m);
            e.Property(x => x.Unit)
             .HasColumnName("unit")
             .HasMaxLength(10)
             .HasDefaultValue("kg");
        });

        // ══════════════════════════════════════════════════════════════════════
        // БЛОК 2: ЗАКУПІВЛІ (Master-Detail — SupplyInvoice → InvoiceItems)
        // ══════════════════════════════════════════════════════════════════════

        // ── Supplier ──────────────────────────────────────────────────────
        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(x => x.SupplierId);
            e.Property(x => x.SupplierId).HasColumnName("supplier_id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            e.Property(x => x.Edrpou).HasColumnName("edrpou").HasMaxLength(10);
            e.HasIndex(x => x.Edrpou).IsUnique();
        });

        // ── SupplyInvoice (Master) ────────────────────────────────────────
        modelBuilder.Entity<SupplyInvoice>(e =>
        {
            e.ToTable("supply_invoices");
            e.HasKey(x => x.InvoiceId);
            e.Property(x => x.InvoiceId).HasColumnName("invoice_id").ValueGeneratedOnAdd();
            e.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
            e.Property(x => x.DocNumber).HasColumnName("doc_number").HasMaxLength(50).IsRequired();
            e.Property(x => x.ReceiveDate)
             .HasColumnName("receive_date")
             .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 1:M  Supplier → SupplyInvoices
            e.HasOne(x => x.Supplier)
             .WithMany(s => s.Invoices)
             .HasForeignKey(x => x.SupplierId)
             .OnDelete(DeleteBehavior.Restrict);

            // 1:M  SupplyInvoice → InvoiceItems  (Master → Detail)
            e.HasMany(x => x.Items)
             .WithOne(i => i.Invoice)
             .HasForeignKey(i => i.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── InvoiceItem (Detail) — ПАТЕРН "СКЛАДЕНИЙ КЛЮЧ" ───────────────
        modelBuilder.Entity<InvoiceItem>(e =>
        {
            e.ToTable("invoice_items");

            // ★ Composite Primary Key (invoice_id, ingredient_id)
            e.HasKey(x => new { x.InvoiceId, x.IngredientId });

            e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            e.Property(x => x.IngredientId).HasColumnName("ingredient_id");
            e.Property(x => x.Quantity)
             .HasColumnName("quantity")
             .HasColumnType("decimal(10,3)")
             .IsRequired();
            e.Property(x => x.UnitPrice)
             .HasColumnName("unit_price")
             .HasColumnType("decimal(10,2)");
            e.Property(x => x.ExpirationDate).HasColumnName("expiration_date");

            // FK → Ingredient (без Cascade, щоб не видаляти сировину разом з рядком)
            e.HasOne(x => x.Ingredient)
             .WithMany(i => i.InvoiceItems)
             .HasForeignKey(x => x.IngredientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ══════════════════════════════════════════════════════════════════════
        // БЛОК 3: РЕЦЕПТУРА ТА ВИРОБНИЦТВО
        // ══════════════════════════════════════════════════════════════════════

        // ── Recipe — ПАТЕРН "ВЕРСІОНУВАННЯ" (поле IsActive) ───────────────
        modelBuilder.Entity<Recipe>(e =>
        {
            e.ToTable("recipes");
            e.HasKey(x => x.RecipeId);
            e.Property(x => x.RecipeId).HasColumnName("recipe_id").ValueGeneratedOnAdd();
            e.Property(x => x.StyleId).HasColumnName("style_id").IsRequired();
            e.Property(x => x.VersionName)
             .HasColumnName("version_name")
             .HasMaxLength(100)
             .IsRequired();
            // Поле версіонування — за замовчуванням true (активний рецепт)
            e.Property(x => x.IsActive)
             .HasColumnName("is_active")
             .HasDefaultValue(true);

            // 1:M  BeerStyle → Recipes
            e.HasOne(x => x.Style)
             .WithMany(s => s.Recipes)
             .HasForeignKey(x => x.StyleId)
             .OnDelete(DeleteBehavior.Restrict);

            // 1:M  Recipe → RecipeItems
            e.HasMany(x => x.Items)
             .WithOne(i => i.Recipe)
             .HasForeignKey(i => i.RecipeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RecipeItem (M:M через проміжну) — ПАТЕРН "СКЛАДЕНИЙ КЛЮЧ" ─────
        modelBuilder.Entity<RecipeItem>(e =>
        {
            e.ToTable("recipe_items");

            // ★ Composite Primary Key (recipe_id, ingredient_id)
            e.HasKey(x => new { x.RecipeId, x.IngredientId });

            e.Property(x => x.RecipeId).HasColumnName("recipe_id");
            e.Property(x => x.IngredientId).HasColumnName("ingredient_id");
            e.Property(x => x.Amount)
             .HasColumnName("amount")
             .HasColumnType("decimal(8,3)")
             .IsRequired();

            // FK → Ingredient
            e.HasOne(x => x.Ingredient)
             .WithMany(i => i.RecipeItems)
             .HasForeignKey(x => x.IngredientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Batch ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Batch>(e =>
        {
            e.ToTable("batches");
            e.HasKey(x => x.BatchId);
            e.Property(x => x.BatchId).HasColumnName("batch_id").ValueGeneratedOnAdd();
            e.Property(x => x.RecipeId).HasColumnName("recipe_id").IsRequired();
            e.Property(x => x.Status)
             .HasColumnName("status")
             .HasConversion<string>()
             .HasDefaultValue(BatchStatus.Brewing);
            e.Property(x => x.StartDate)
             .HasColumnName("start_date")
             .HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.ActualAbv)
             .HasColumnName("actual_abv")
             .HasColumnType("decimal(4,2)");
            e.Property(x => x.ActualSrm).HasColumnName("actual_srm");

            // 1:M  Recipe → Batches
            e.HasOne(x => x.Recipe)
             .WithMany(r => r.Batches)
             .HasForeignKey(x => x.RecipeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ══════════════════════════════════════════════════════════════════════
        // БЛОК 4: СКЛАД ГОТОВОЇ ПРОДУКЦІЇ ТА ПРОДАЖІ
        // ══════════════════════════════════════════════════════════════════════

        // ── ProductSku ────────────────────────────────────────────────────
        modelBuilder.Entity<ProductSku>(e =>
        {
            e.ToTable("product_skus");
            e.HasKey(x => x.SkuId);
            e.Property(x => x.SkuId).HasColumnName("sku_id").ValueGeneratedOnAdd();
            e.Property(x => x.BatchId).HasColumnName("batch_id").IsRequired();
            e.Property(x => x.PackagingType)
             .HasColumnName("packaging_type")
             .HasConversion<string>()
             .IsRequired();
            e.Property(x => x.Price)
             .HasColumnName("price")
             .HasColumnType("decimal(10,2)")
             .IsRequired();
            e.Property(x => x.QuantityInStock)
             .HasColumnName("quantity_in_stock")
             .HasDefaultValue(0);

            // 1:M  Batch → ProductSkus
            e.HasOne(x => x.Batch)
             .WithMany(b => b.ProductSkus)
             .HasForeignKey(x => x.BatchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Client ────────────────────────────────────────────────────────
        modelBuilder.Entity<Client>(e =>
        {
            e.ToTable("clients");
            e.HasKey(x => x.ClientId);
            e.Property(x => x.ClientId).HasColumnName("client_id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        });

        // ── SalesOrder (Master) ───────────────────────────────────────────
        modelBuilder.Entity<SalesOrder>(e =>
        {
            e.ToTable("sales_orders");
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).HasColumnName("order_id").ValueGeneratedOnAdd();
            e.Property(x => x.ClientId).HasColumnName("client_id").IsRequired();
            e.Property(x => x.OrderDate)
             .HasColumnName("order_date")
             .HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Status)
             .HasColumnName("status")
             .HasConversion<string>()
             .HasDefaultValue(OrderStatus.New);

            // 1:M  Client → SalesOrders
            e.HasOne(x => x.Client)
             .WithMany(c => c.Orders)
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict);

            // 1:M  SalesOrder → OrderItems  (Master → Detail)
            e.HasMany(x => x.Items)
             .WithOne(i => i.Order)
             .HasForeignKey(i => i.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── OrderItem (Detail) — ПАТЕРН "СКЛАДЕНИЙ КЛЮЧ" ─────────────────
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");

            // ★ Composite Primary Key (order_id, sku_id)
            e.HasKey(x => new { x.OrderId, x.SkuId });

            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.SkuId).HasColumnName("sku_id");
            e.Property(x => x.Quantity)
             .HasColumnName("quantity")
             .IsRequired();
            e.Property(x => x.PriceAtMoment)
             .HasColumnName("price_at_moment")
             .HasColumnType("decimal(10,2)")
             .IsRequired();

            // FK → ProductSku
            e.HasOne(x => x.ProductSku)
             .WithMany(s => s.OrderItems)
             .HasForeignKey(x => x.SkuId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ImportLog ─────────────────────────────────────────────────
        modelBuilder.Entity<ImportLog>(e =>
        {
            e.ToTable("import_logs");
            e.HasKey(x => x.ImportId);
            e.Property(x => x.ImportId).HasColumnName("import_id").ValueGeneratedOnAdd();
            e.Property(x => x.FileName  ).HasColumnName("file_name"  ).HasMaxLength(255).IsRequired();
            e.Property(x => x.ImportedAt).HasColumnName("imported_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.ImportedBy).HasColumnName("imported_by").HasMaxLength(255).IsRequired();
            e.Property(x => x.InvoiceId ).HasColumnName("invoice_id");
            e.Property(x => x.Status    ).HasColumnName("status"    ).HasMaxLength(20).IsRequired();
            e.Property(x => x.Error     ).HasColumnName("error");
            e.Property(x => x.RowCount  ).HasColumnName("row_count");

            // FK → SupplyInvoice (nullable — може бути null якщо імпорт провалився)
            e.HasOne(x => x.Invoice)
             .WithMany()
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
