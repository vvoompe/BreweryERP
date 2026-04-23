using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.Batches;
using BreweryERP.Api.Models;
using BreweryERP.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Tests.Services;

/// <summary>
/// Tests for BatchService — stock deduction and status transitions.
/// </summary>
public class BatchServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly BatchService         _service;

    // Seed data
    private readonly BeerStyle   _style;
    private readonly Ingredient  _malt;
    private readonly Ingredient  _hops;
    private readonly Recipe      _recipe;

    public BatchServiceTests()
    {
        _db      = TestDbFactory.Create();
        _service = new BatchService(_db);

        _style = new BeerStyle { Name = "IPA", Description = "Test" };
        _db.BeerStyles.Add(_style);
        _db.SaveChanges();

        _malt = new Ingredient { Name = "Malt",  Type = IngredientType.Malt, Unit = "kg", TotalStock = 1000m };
        _hops = new Ingredient { Name = "Hops",  Type = IngredientType.Hop,  Unit = "g",  TotalStock = 200m  };
        _db.Ingredients.AddRange(_malt, _hops);
        _db.SaveChanges();

        _recipe = new Recipe
        {
            StyleId     = _style.StyleId,
            VersionName = "Test IPA v1.0",
            IsActive    = true,
            Items = new List<RecipeItem>
            {
                new() { IngredientId = _malt.IngredientId, Amount = 200m },
                new() { IngredientId = _hops.IngredientId, Amount = 50m  },
            }
        };
        _db.Recipes.Add(_recipe);
        _db.SaveChanges();
    }

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_DeductsTotalStock()
    {
        var request = new CreateBatchRequest(_recipe.RecipeId);
        await _service.CreateAsync(request);

        await _db.Entry(_malt).ReloadAsync();
        await _db.Entry(_hops).ReloadAsync();

        Assert.Equal(800m, _malt.TotalStock); // 1000 - 200
        Assert.Equal(150m, _hops.TotalStock); // 200  - 50
    }

    [Fact]
    public async Task CreateAsync_ReturnsBatchWithCorrectStatus()
    {
        var request = new CreateBatchRequest(_recipe.RecipeId);
        var (batch, _) = await _service.CreateAsync(request);

        Assert.Equal("Brewing", batch.Status);
        Assert.Equal(_recipe.RecipeId, batch.RecipeId);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCorrectWriteoffs()
    {
        var request = new CreateBatchRequest(_recipe.RecipeId);
        var (_, writeoffs) = await _service.CreateAsync(request);

        Assert.Equal(2, writeoffs.Count);
        Assert.Contains(writeoffs, w => w.IngredientId == _malt.IngredientId && w.AmountWrittenOff == 200m);
        Assert.Contains(writeoffs, w => w.IngredientId == _hops.IngredientId && w.AmountWrittenOff == 50m);
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_ThrowsInvalidOperation()
    {
        // Зменшуємо запас нижче потрібного
        _malt.TotalStock = 10m; // Need 200, have only 10
        await _db.SaveChangesAsync();

        var request = new CreateBatchRequest(_recipe.RecipeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_StockNotChanged()
    {
        _malt.TotalStock = 10m;
        await _db.SaveChangesAsync();

        try { await _service.CreateAsync(new CreateBatchRequest(_recipe.RecipeId)); }
        catch (InvalidOperationException) { /* expected */ }

        // После помилки — stock не змінився (транзакція відкочена)
        await _db.Entry(_malt).ReloadAsync();
        await _db.Entry(_hops).ReloadAsync();

        Assert.Equal(10m,  _malt.TotalStock);  // незмінний
        Assert.Equal(200m, _hops.TotalStock);  // незмінний
    }

    [Fact]
    public async Task CreateAsync_NonExistentRecipe_ThrowsKeyNotFoundException()
    {
        var request = new CreateBatchRequest(99999);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_InactiveRecipe_ThrowsInvalidOperation()
    {
        _recipe.IsActive = false;
        await _db.SaveChangesAsync();

        var request = new CreateBatchRequest(_recipe.RecipeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_EmptyRecipe_ThrowsInvalidOperation()
    {
        var emptyRecipe = new Recipe
        {
            StyleId     = _style.StyleId,
            VersionName = "v1.0",
            IsActive    = true,
            Items       = []
        };
        _db.Recipes.Add(emptyRecipe);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(new CreateBatchRequest(emptyRecipe.RecipeId)));
    }

    // ── UpdateStatusAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_BrewingToFermenting_Succeeds()
    {
        var (batch, _) = await _service.CreateAsync(new CreateBatchRequest(_recipe.RecipeId));

        var updated = await _service.UpdateStatusAsync(batch.BatchId,
            new UpdateBatchStatusRequest(BatchStatus.Fermenting, null, null));

        Assert.Equal("Fermenting", updated.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_FermentingToCompleted_Succeeds()
    {
        var (batch, _) = await _service.CreateAsync(new CreateBatchRequest(_recipe.RecipeId));
        await _service.UpdateStatusAsync(batch.BatchId,
            new UpdateBatchStatusRequest(BatchStatus.Fermenting, null, null));

        var completed = await _service.UpdateStatusAsync(batch.BatchId,
            new UpdateBatchStatusRequest(BatchStatus.Completed, 5.2m, 12));

        Assert.Equal("Completed", completed.Status);
        Assert.Equal(5.2m, completed.ActualAbv);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsInvalidOperation()
    {
        var (batch, _) = await _service.CreateAsync(new CreateBatchRequest(_recipe.RecipeId));

        // Пряме переведення Brewing → Completed — заборонено
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateStatusAsync(batch.BatchId,
                new UpdateBatchStatusRequest(BatchStatus.Completed, null, null)));
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentBatch_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateStatusAsync(99999,
                new UpdateBatchStatusRequest(BatchStatus.Fermenting, null, null)));
    }

    public void Dispose() => _db.Dispose();
}
