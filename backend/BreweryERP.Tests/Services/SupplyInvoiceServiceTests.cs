using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.SupplyInvoices;
using BreweryERP.Api.Models;
using BreweryERP.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Tests.Services;

/// <summary>
/// Integration-style tests for SupplyInvoiceService.
/// Uses EF Core InMemory — no MySQL needed.
/// Перевіряє ключову бізнес-логіку: TotalStock increment/decrement.
/// </summary>
public class SupplyInvoiceServiceTests : IDisposable
{
    private readonly ApplicationDbContext       _db;
    private readonly SupplyInvoiceService       _service;
    private readonly Supplier                   _supplier;
    private readonly Ingredient                 _ing1;
    private readonly Ingredient                 _ing2;

    public SupplyInvoiceServiceTests()
    {
        _db      = TestDbFactory.Create();
        _service = new SupplyInvoiceService(_db);

        // Seed: постачальник + два інгредієнти
        _supplier = new Supplier { Name = "Test Supplier" };
        _ing1     = new Ingredient { Name = "Pilsner Malt", Type = IngredientType.Malt,  Unit = "kg", TotalStock = 0 };
        _ing2     = new Ingredient { Name = "Cascade Hops", Type = IngredientType.Hop,   Unit = "kg", TotalStock = 0 };

        _db.Suppliers.Add(_supplier);
        _db.Ingredients.AddRange(_ing1, _ing2);
        _db.SaveChanges();
    }

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesInvoice()
    {
        var request = new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-001", DateTime.UtcNow,
            [new InvoiceItemRequest(_ing1.IngredientId, 500m, 25.50m, null)]);

        var dto = await _service.CreateAsync(request);

        Assert.NotNull(dto);
        Assert.Equal("НК-001", dto.DocNumber);
        Assert.Single(dto.Items);
        Assert.Equal(500m, dto.Items[0].Quantity);
    }

    [Fact]
    public async Task CreateAsync_IncrementsTotalStock()
    {
        var request = new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-002", DateTime.UtcNow,
            [
                new InvoiceItemRequest(_ing1.IngredientId, 300m, null, null),
                new InvoiceItemRequest(_ing2.IngredientId, 50m,  null, null),
            ]);

        await _service.CreateAsync(request);

        // Перезавантажуємо з БД
        await _db.Entry(_ing1).ReloadAsync();
        await _db.Entry(_ing2).ReloadAsync();

        Assert.Equal(300m, _ing1.TotalStock);
        Assert.Equal(50m,  _ing2.TotalStock);
    }

    [Fact]
    public async Task CreateAsync_MultipleInvoices_AccumulatesStock()
    {
        // Перший завіз
        await _service.CreateAsync(new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-003", DateTime.UtcNow,
            [new InvoiceItemRequest(_ing1.IngredientId, 100m, null, null)]));

        // Другий завіз
        await _service.CreateAsync(new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-004", DateTime.UtcNow,
            [new InvoiceItemRequest(_ing1.IngredientId, 200m, null, null)]));

        await _db.Entry(_ing1).ReloadAsync();
        Assert.Equal(300m, _ing1.TotalStock); // 100 + 200
    }

    [Fact]
    public async Task CreateAsync_NonExistentSupplier_ThrowsKeyNotFoundException()
    {
        var request = new CreateSupplyInvoiceRequest(
            99999, "НК-XXX", DateTime.UtcNow,
            [new InvoiceItemRequest(_ing1.IngredientId, 100m, null, null)]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_EmptyItems_ThrowsArgumentException()
    {
        var request = new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-YYY", DateTime.UtcNow, []);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_NonExistentIngredient_ThrowsKeyNotFoundException()
    {
        var request = new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-ZZZ", DateTime.UtcNow,
            [new InvoiceItemRequest(99999, 100m, null, null)]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CreateAsync(request));
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RollsBackTotalStock()
    {
        // Створюємо накладну (додає 500 до TotalStock)
        var dto = await _service.CreateAsync(new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-DEL-001", DateTime.UtcNow,
            [new InvoiceItemRequest(_ing1.IngredientId, 500m, null, null)]));

        await _db.Entry(_ing1).ReloadAsync();
        Assert.Equal(500m, _ing1.TotalStock); // підтверджуємо

        // Видаляємо — TotalStock має повернутись до 0
        await _service.DeleteAsync(dto.InvoiceId);

        await _db.Entry(_ing1).ReloadAsync();
        Assert.Equal(0m, _ing1.TotalStock);
    }

    [Fact]
    public async Task DeleteAsync_RemovesInvoiceFromDb()
    {
        var dto = await _service.CreateAsync(new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-DEL-002", DateTime.UtcNow,
            [new InvoiceItemRequest(_ing1.IngredientId, 100m, null, null)]));

        await _service.DeleteAsync(dto.InvoiceId);

        var found = await _db.SupplyInvoices.FindAsync(dto.InvoiceId);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentInvoice_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteAsync(99999));
    }

    [Fact]
    public async Task DeleteAsync_MultiItemInvoice_RollsBackAllItems()
    {
        var dto = await _service.CreateAsync(new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-DEL-MULTI", DateTime.UtcNow,
            [
                new InvoiceItemRequest(_ing1.IngredientId, 300m, null, null),
                new InvoiceItemRequest(_ing2.IngredientId, 75m,  null, null),
            ]));

        await _service.DeleteAsync(dto.InvoiceId);

        await _db.Entry(_ing1).ReloadAsync();
        await _db.Entry(_ing2).ReloadAsync();

        Assert.Equal(0m, _ing1.TotalStock);
        Assert.Equal(0m, _ing2.TotalStock);
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectDto()
    {
        var dto = await _service.CreateAsync(new CreateSupplyInvoiceRequest(
            _supplier.SupplierId, "НК-GET", DateTime.UtcNow,
            [new InvoiceItemRequest(_ing1.IngredientId, 100m, 20m, new DateOnly(2025, 6, 30))]));

        var fetched = await _service.GetByIdAsync(dto.InvoiceId);

        Assert.NotNull(fetched);
        Assert.Equal("НК-GET", fetched!.DocNumber);
        Assert.Equal(_supplier.Name, fetched.SupplierName);
        Assert.Single(fetched.Items);
        Assert.Equal(100m, fetched.Items[0].Quantity);
        Assert.Equal(20m,  fetched.Items[0].UnitPrice);
        Assert.Equal(new DateOnly(2025, 6, 30), fetched.Items[0].ExpirationDate);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(99999);
        Assert.Null(result);
    }

    public void Dispose() => _db.Dispose();
}
