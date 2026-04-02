using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.SupplyInvoices;
using BreweryERP.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Services;

/// <summary>
/// Реалізує патерн "Головний-підлеглий" для закупівель.
/// Бізнес-логіка ролі Warehouse: при кожному збереженні INVOICE_ITEM
/// автоматично збільшується Ingredient.TotalStock.
/// </summary>
public class SupplyInvoiceService : ISupplyInvoiceService
{
    private readonly ApplicationDbContext _db;

    public SupplyInvoiceService(ApplicationDbContext db) => _db = db;

    // ── GET ALL ───────────────────────────────────────────────────────────────
    public async Task<IEnumerable<SupplyInvoiceListDto>> GetAllAsync()
    {
        return await _db.SupplyInvoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Include(i => i.Items)
            .OrderByDescending(i => i.ReceiveDate)
            .Select(i => new SupplyInvoiceListDto(
                i.InvoiceId,
                i.Supplier.Name,
                i.DocNumber,
                i.ReceiveDate,
                i.Items.Count))
            .ToListAsync();
    }

    // ── GET BY ID ─────────────────────────────────────────────────────────────
    public async Task<SupplyInvoiceDto?> GetByIdAsync(int id)
    {
        var invoice = await _db.SupplyInvoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Include(i => i.Items)
                .ThenInclude(ii => ii.Ingredient)
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        return invoice is null ? null : MapToDto(invoice);
    }

    // ── CREATE (Master-Detail + Stock increment) ──────────────────────────────
    public async Task<SupplyInvoiceDto> CreateAsync(CreateSupplyInvoiceRequest request)
    {
        // Валідація: постачальник існує
        var supplierExists = await _db.Suppliers.AnyAsync(s => s.SupplierId == request.SupplierId);
        if (!supplierExists)
            throw new KeyNotFoundException($"Supplier {request.SupplierId} not found.");

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Invoice must contain at least one item.");

        // Збираємо всі ID інгредієнтів для bulk-завантаження
        var ingredientIds = request.Items.Select(i => i.IngredientId).Distinct().ToList();
        var ingredients = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.IngredientId))
            .ToDictionaryAsync(i => i.IngredientId);

        // Валідація: всі інгредієнти знайдені
        var notFound = ingredientIds.Except(ingredients.Keys).ToList();
        if (notFound.Count > 0)
            throw new KeyNotFoundException($"Ingredients not found: {string.Join(", ", notFound)}");

        // ════ ТРАНЗАКЦІЯ: атомарне збереження Master + Details ════
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Створення Master-документа
            var invoice = new SupplyInvoice
            {
                SupplierId  = request.SupplierId,
                DocNumber   = request.DocNumber,
                ReceiveDate = request.ReceiveDate ?? DateTime.UtcNow
            };
            _db.SupplyInvoices.Add(invoice);
            await _db.SaveChangesAsync(); // Отримуємо InvoiceId

            // 2. Додавання Detail-рядків + збільшення TotalStock (роль Warehouse)
            foreach (var itemReq in request.Items)
            {
                var ingredient = ingredients[itemReq.IngredientId];

                // ★ Бізнес-логіка: TotalStock += Quantity
                ingredient.TotalStock += itemReq.Quantity;

                var item = new InvoiceItem
                {
                    InvoiceId      = invoice.InvoiceId,
                    IngredientId   = itemReq.IngredientId,
                    Quantity       = itemReq.Quantity,
                    ExpirationDate = itemReq.ExpirationDate
                };
                _db.InvoiceItems.Add(item);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Повертаємо повний DTO з навігаційними властивостями
            return await GetByIdAsync(invoice.InvoiceId)
                ?? throw new InvalidOperationException("Failed to reload invoice after creation.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── DELETE ────────────────────────────────────────────────────────────────
    public async Task DeleteAsync(int id)
    {
        var invoice = await _db.SupplyInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.InvoiceId == id)
            ?? throw new KeyNotFoundException($"Invoice {id} not found.");

        // Примітка: ON DELETE CASCADE для Items налаштовано у Fluent API,
        // але тут явно завантажуємо Items щоб EF відстежив зворотне оновлення TotalStock.
        // У production варто розглянути окремий "сторно"-ендпоінт.
        _db.SupplyInvoices.Remove(invoice);
        await _db.SaveChangesAsync();
    }

    // ── MAPPING ───────────────────────────────────────────────────────────────
    private static SupplyInvoiceDto MapToDto(SupplyInvoice i) => new(
        i.InvoiceId,
        i.SupplierId,
        i.Supplier.Name,
        i.DocNumber,
        i.ReceiveDate,
        i.Items.Select(ii => new InvoiceItemDto(
            ii.IngredientId,
            ii.Ingredient.Name,
            ii.Quantity,
            ii.Ingredient.Unit,
            ii.ExpirationDate)).ToList());
}
