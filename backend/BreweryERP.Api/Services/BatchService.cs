using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.Batches;
using BreweryERP.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Services;

/// <summary>
/// Реалізує бізнес-логіку ролі Brewer:
/// при створенні нового Batch автоматично списується сировина зі складу
/// (TotalStock -= Amount) для кожного інгредієнта з рецепту.
/// Перевіряється наявність достатніх залишків до початку транзакції.
/// </summary>
public class BatchService : IBatchService
{
    private readonly ApplicationDbContext _db;

    public BatchService(ApplicationDbContext db) => _db = db;

    // ── GET ALL ───────────────────────────────────────────────────────────────
    public async Task<IEnumerable<BatchDto>> GetAllAsync()
    {
        return await _db.Batches
            .AsNoTracking()
            .Include(b => b.Recipe)
                .ThenInclude(r => r.Style)
            .OrderByDescending(b => b.StartDate)
            .Select(b => new BatchDto(
                b.BatchId,
                b.RecipeId,
                b.Recipe.VersionName,
                b.Recipe.Style.Name,
                b.Status.ToString(),
                b.StartDate,
                b.ActualAbv,
                b.ActualSrm,
                b.EstimatedCost))
            .ToListAsync();
    }

    // ── GET BY ID ─────────────────────────────────────────────────────────────
    public async Task<BatchDto?> GetByIdAsync(int id)
    {
        var batch = await _db.Batches
            .AsNoTracking()
            .Include(b => b.Recipe)
                .ThenInclude(r => r.Style)
            .FirstOrDefaultAsync(b => b.BatchId == id);

        return batch is null ? null : MapToDto(batch);
    }

    // ── CREATE (★ Brewer: списання сировини + транзакція) ────────────────────
    public async Task<(BatchDto Batch, IList<BatchStockWriteoffDto> Writeoffs)> CreateAsync(
        CreateBatchRequest request)
    {
        // Завантажуємо рецепт з усіма Items та Ingredients
        var recipe = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Style)
            .FirstOrDefaultAsync(r => r.RecipeId == request.RecipeId)
            ?? throw new KeyNotFoundException($"Recipe {request.RecipeId} not found.");

        if (!recipe.IsActive)
            throw new InvalidOperationException(
                "Cannot start a batch from an archived recipe version. Use the active version.");

        if (recipe.Items.Count == 0)
            throw new InvalidOperationException("Recipe has no ingredients defined.");

        // ─── PRE-CHECK: перевірка залишків (до транзакції) ─────────────────
        var shortages = recipe.Items
            .Where(ri => ri.Ingredient.TotalStock < ri.Amount)
            .Select(ri => $"{ri.Ingredient.Name}: need {ri.Amount} {ri.Ingredient.Unit}, " +
                          $"have {ri.Ingredient.TotalStock} {ri.Ingredient.Unit}")
            .ToList();

        if (shortages.Count > 0)
            throw new InvalidOperationException(
                "Insufficient stock for the following ingredients:\n" +
                string.Join("\n", shortages));

        // ════ ТРАНЗАКЦІЯ (через ExecutionStrategy — сумісно з EnableRetryOnFailure) ════
        BatchDto? batchResult = null;
        List<BatchStockWriteoffDto>? writeoffsResult = null;

        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Розрахунок собівартості партії
                var estimatedCost = recipe.Items.Sum(ri => ri.Amount * ri.Ingredient.AverageCost);

                // 1. Створення запису Batch
                var batch = new Batch
                {
                    RecipeId  = recipe.RecipeId,
                    Status    = (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<BatchStatus>(request.Status, true, out var parsedStatus)) ? parsedStatus : BatchStatus.Brewing,
                    StartDate = request.StartDate == default ? DateTime.UtcNow : request.StartDate,
                    ActualAbv = request.ActualAbv,
                    ActualSrm = request.ActualSrm,
                    EstimatedCost = estimatedCost
                };
                _db.Batches.Add(batch);
                await _db.SaveChangesAsync(); // Отримуємо BatchId

                // 2. ★ Бізнес-логіка: TotalStock -= Amount для кожного інгредієнта
                var writeoffs = new List<BatchStockWriteoffDto>();
                foreach (var recipeItem in recipe.Items)
                {
                    var ingredient = recipeItem.Ingredient;
                    ingredient.TotalStock -= recipeItem.Amount;

                    writeoffs.Add(new BatchStockWriteoffDto(
                        ingredient.IngredientId,
                        ingredient.Name,
                        recipeItem.Amount,
                        ingredient.TotalStock));
                }

                _db.ActivityLogs.Add(new ActivityLog {
                    Action = "Batch Started",
                    EntityName = "Batch",
                    EntityId = batch.BatchId,
                    Details = $"Started brewing Recipe #{recipe.RecipeId}",
                    UserName = "System"
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                batchResult    = MapToDto(batch, recipe);
                writeoffsResult = writeoffs;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });

        return (batchResult!, writeoffsResult!);
    }

    // ── UPDATE FULL (Brewer, Admin) ───────────────────────────────────────────
    public async Task<BatchDto> UpdateAsync(int batchId, UpdateBatchRequest request)
    {
        var batch = await _db.Batches
            .Include(b => b.Recipe)
                .ThenInclude(r => r.Style)
            .FirstOrDefaultAsync(b => b.BatchId == batchId)
            ?? throw new KeyNotFoundException($"Batch {batchId} not found.");

        batch.RecipeId = request.RecipeId;
        if (Enum.TryParse<BatchStatus>(request.Status, true, out var parsedStatus))
        {
            batch.Status = parsedStatus;
        }
        if (request.StartDate != default)
        {
            batch.StartDate = request.StartDate;
        }
        batch.ActualAbv = request.ActualAbv;
        batch.ActualSrm = request.ActualSrm;

        _db.ActivityLogs.Add(new ActivityLog {
            Action = "Batch Updated",
            EntityName = "Batch",
            EntityId = batch.BatchId,
            Details = $"Batch {batchId} was fully updated",
            UserName = "System"
        });

        await _db.SaveChangesAsync();
        return MapToDto(batch);
    }

    // ── UPDATE STATUS ─────────────────────────────────────────────────────────
    public async Task<BatchDto> UpdateStatusAsync(int batchId, UpdateBatchStatusRequest request)
    {
        var batch = await _db.Batches
            .Include(b => b.Recipe)
                .ThenInclude(r => r.Style)
            .FirstOrDefaultAsync(b => b.BatchId == batchId)
            ?? throw new KeyNotFoundException($"Batch {batchId} not found.");

        // Валідація допустимих переходів статусів
        var allowed = (batch.Status, request.Status) switch
        {
            (BatchStatus.Brewing,    BatchStatus.Fermenting) => true,
            (BatchStatus.Fermenting, BatchStatus.Completed)  => true,
            (BatchStatus.Fermenting, BatchStatus.Failed)     => true,
            (BatchStatus.Brewing,    BatchStatus.Failed)     => true,
            _ => false
        };

        if (!allowed)
            throw new InvalidOperationException(
                $"Invalid status transition: {batch.Status} → {request.Status}");

        batch.Status    = request.Status;
        batch.ActualAbv = request.ActualAbv ?? batch.ActualAbv;
        batch.ActualSrm = request.ActualSrm ?? batch.ActualSrm;

        _db.ActivityLogs.Add(new ActivityLog {
            Action = "Batch Status Changed",
            EntityName = "Batch",
            EntityId = batch.BatchId,
            Details = $"Status changed to {request.Status}",
            UserName = "System"
        });

        await _db.SaveChangesAsync();
        return MapToDto(batch);
    }

    // ── MAPPING ───────────────────────────────────────────────────────────────
    private static BatchDto MapToDto(Batch b) => new(
        b.BatchId, b.RecipeId,
        b.Recipe.VersionName, b.Recipe.Style.Name,
        b.Status.ToString(), b.StartDate,
        b.ActualAbv, b.ActualSrm, b.EstimatedCost);

    private static BatchDto MapToDto(Batch b, Recipe recipe) => new(
        b.BatchId, b.RecipeId,
        recipe.VersionName, recipe.Style.Name,
        b.Status.ToString(), b.StartDate,
        b.ActualAbv, b.ActualSrm, b.EstimatedCost);
}
