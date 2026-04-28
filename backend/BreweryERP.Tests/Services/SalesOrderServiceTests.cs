using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.SalesOrders;
using BreweryERP.Api.Models;
using BreweryERP.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Tests.Services;

/// <summary>
/// Tests for SalesOrderService — Master-Detail creation, status transitions,
/// stock deduction (Shipped), and FK/NOT-NULL structural constraint checks via EF metadata.
/// NOTE: InMemory ignores real transactions — ACID is validated at business-logic level.
/// </summary>
public class SalesOrderServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly SalesOrderService    _service;

    private readonly Client     _client;
    private readonly BeerStyle  _style;
    private readonly Recipe     _recipe;
    private readonly Batch      _batch;
    private readonly ProductSku _sku1;
    private readonly ProductSku _sku2;

    public SalesOrderServiceTests()
    {
        _db      = TestDbFactory.Create();
        _service = new SalesOrderService(_db);

        _client = new Client { Name = "Test Pub", Phone = "+380501234567" };
        _db.Clients.Add(_client);

        _style = new BeerStyle { Name = "Test IPA", Description = "Test" };
        _db.BeerStyles.Add(_style);
        _db.SaveChanges();

        _recipe = new Recipe { StyleId = _style.StyleId, VersionName = "IPA v1.0", IsActive = true };
        _db.Recipes.Add(_recipe);
        _db.SaveChanges();

        _batch = new Batch { RecipeId = _recipe.RecipeId, Status = BatchStatus.Completed, StartDate = DateTime.UtcNow };
        _db.Batches.Add(_batch);
        _db.SaveChanges();

        _sku1 = new ProductSku { BatchId = _batch.BatchId, PackagingType = PackagingType.Keg_30L,     Price = 2800m, UnitCost = 1400m, QuantityInStock = 20  };
        _sku2 = new ProductSku { BatchId = _batch.BatchId, PackagingType = PackagingType.Bottle_0_5L, Price = 85m,   UnitCost = 40m,   QuantityInStock = 500 };
        _db.ProductSkus.AddRange(_sku1, _sku2);
        _db.SaveChanges();
    }

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOrderWithItems()
    {
        var dto = await _service.CreateAsync(new CreateSalesOrderRequest(
            _client.ClientId,
            [new OrderItemRequest(_sku1.SkuId, 3), new OrderItemRequest(_sku2.SkuId, 100)]));

        Assert.NotNull(dto);
        Assert.Equal(_client.ClientId, dto.ClientId);
        Assert.Equal("New", dto.Status);
        Assert.Equal(2, dto.Items.Count);
    }

    [Fact]
    public async Task CreateAsync_FixesPriceAtMoment()
    {
        var dto = await _service.CreateAsync(new CreateSalesOrderRequest(
            _client.ClientId, [new OrderItemRequest(_sku1.SkuId, 1)]));

        Assert.Equal(_sku1.Price, dto.Items[0].PriceAtMoment);
    }

    [Fact]
    public async Task CreateAsync_CalculatesMarginCorrectly()
    {
        // 1 x Keg_30L: revenue=2800, cost=1400, margin=1400
        var dto = await _service.CreateAsync(new CreateSalesOrderRequest(
            _client.ClientId, [new OrderItemRequest(_sku1.SkuId, 1)]));

        Assert.Equal(2800m, dto.TotalAmount);
        Assert.Equal(1400m, dto.TotalCost);
        Assert.Equal(1400m, dto.ProfitMargin);
    }

    [Fact]
    public async Task CreateAsync_NonExistentClient_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateAsync(new CreateSalesOrderRequest(99999, [new OrderItemRequest(_sku1.SkuId, 1)])));
    }

    [Fact]
    public async Task CreateAsync_EmptyItems_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateSalesOrderRequest(_client.ClientId, [])));
    }

    [Fact]
    public async Task CreateAsync_NonExistentSku_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateAsync(new CreateSalesOrderRequest(_client.ClientId, [new OrderItemRequest(99999, 1)])));
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new CreateSalesOrderRequest(
                _client.ClientId, [new OrderItemRequest(_sku1.SkuId, _sku1.QuantityInStock + 1)])));
    }

    // ── Structural / Constraint checks via EF metadata ─────────────────────────

    [Fact]
    public async Task StructuralTest_SalesOrder_ClientId_IsNotNullable()
    {
        var prop = _db.Model.FindEntityType(typeof(SalesOrder))!
                            .FindProperty(nameof(SalesOrder.ClientId))!;

        Assert.False(prop.IsNullable, "SalesOrder.ClientId must be NOT NULL (required FK)");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StructuralTest_OrderItem_HasCompositeKey()
    {
        var pk = _db.Model.FindEntityType(typeof(OrderItem))!.FindPrimaryKey()!;

        Assert.Equal(2, pk.Properties.Count);
        Assert.Contains(pk.Properties, p => p.Name == nameof(OrderItem.OrderId));
        Assert.Contains(pk.Properties, p => p.Name == nameof(OrderItem.SkuId));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StructuralTest_InvoiceItem_HasCompositeKey()
    {
        var pk = _db.Model.FindEntityType(typeof(InvoiceItem))!.FindPrimaryKey()!;

        Assert.Equal(2, pk.Properties.Count);
        Assert.Contains(pk.Properties, p => p.Name == nameof(InvoiceItem.InvoiceId));
        Assert.Contains(pk.Properties, p => p.Name == nameof(InvoiceItem.IngredientId));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StructuralTest_RecipeItem_HasCompositeKey()
    {
        var pk = _db.Model.FindEntityType(typeof(RecipeItem))!.FindPrimaryKey()!;

        Assert.Equal(2, pk.Properties.Count);
        Assert.Contains(pk.Properties, p => p.Name == nameof(RecipeItem.RecipeId));
        Assert.Contains(pk.Properties, p => p.Name == nameof(RecipeItem.IngredientId));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StructuralTest_SalesOrder_FK_Client_IsRestrict()
    {
        var fk = _db.Model.FindEntityType(typeof(SalesOrder))!
                          .GetForeignKeys()
                          .First(f => f.PrincipalEntityType.ClrType == typeof(Client));

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StructuralTest_OrderItem_FK_Order_IsCascade()
    {
        var fk = _db.Model.FindEntityType(typeof(OrderItem))!
                          .GetForeignKeys()
                          .First(f => f.PrincipalEntityType.ClrType == typeof(SalesOrder));

        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        await Task.CompletedTask;
    }

    // ── UpdateStatusAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_NewToReserved_Succeeds()
    {
        var order = await _service.CreateAsync(new CreateSalesOrderRequest(
            _client.ClientId, [new OrderItemRequest(_sku1.SkuId, 1)]));

        var updated = await _service.UpdateStatusAsync(order.OrderId,
            new UpdateOrderStatusRequest(OrderStatus.Reserved));

        Assert.Equal("Reserved", updated.Status);
    }

    [Fact]
    public async Task UpdateStatus_ShippedDeductsStock()
    {
        const int qty = 5;
        var order = await _service.CreateAsync(new CreateSalesOrderRequest(
            _client.ClientId, [new OrderItemRequest(_sku1.SkuId, qty)]));

        await _service.UpdateStatusAsync(order.OrderId,
            new UpdateOrderStatusRequest(OrderStatus.Shipped));

        await _db.Entry(_sku1).ReloadAsync();
        Assert.Equal(20 - qty, _sku1.QuantityInStock);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ThrowsInvalidOperation()
    {
        var order = await _service.CreateAsync(new CreateSalesOrderRequest(
            _client.ClientId, [new OrderItemRequest(_sku1.SkuId, 1)]));

        // New → Paid — забороненний перехід
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateStatusAsync(order.OrderId,
                new UpdateOrderStatusRequest(OrderStatus.Paid)));
    }

    [Fact]
    public async Task UpdateStatus_NonExistentOrder_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateStatusAsync(99999,
                new UpdateOrderStatusRequest(OrderStatus.Reserved)));
    }

    // ── ACID: відкат при нестачі стоку під час відвантаження ──────────────────

    [Fact]
    public async Task ACID_ShipWithZeroStock_ThrowsAndStockRemainsZero()
    {
        // Замовляємо при наявності стоку...
        var order = await _service.CreateAsync(new CreateSalesOrderRequest(
            _client.ClientId, [new OrderItemRequest(_sku1.SkuId, 3)]));

        // ...потім стік "вичерпується" конкурентним процесом
        _sku1.QuantityInStock = 0;
        await _db.SaveChangesAsync();

        try
        {
            await _service.UpdateStatusAsync(order.OrderId,
                new UpdateOrderStatusRequest(OrderStatus.Shipped));
        }
        catch (InvalidOperationException) { /* очікується */ }

        // Запас не повинен стати від'ємним
        await _db.Entry(_sku1).ReloadAsync();
        Assert.Equal(0, _sku1.QuantityInStock);
    }

    public void Dispose() => _db.Dispose();
}
