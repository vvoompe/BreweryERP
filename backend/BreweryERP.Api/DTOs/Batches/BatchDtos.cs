using BreweryERP.Api.Models;

namespace BreweryERP.Api.DTOs.Batches;

// ── Requests ──────────────────────────────────────────────────────────────────

/// <summary>
/// При створенні Batch автоматично списується сировина зі складу
/// (Brewer role business logic у BatchService).
/// </summary>
public record CreateBatchRequest(
    int RecipeId,
    string Status,
    DateTime StartDate,
    decimal? ActualAbv,
    int? ActualSrm);

public record UpdateBatchRequest(
    int RecipeId,
    string Status,
    DateTime StartDate,
    decimal? ActualAbv,
    int? ActualSrm);

public record UpdateBatchStatusRequest(BatchStatus Status, decimal? ActualAbv, int? ActualSrm);

// ── Responses ─────────────────────────────────────────────────────────────────

public record BatchDto(
    int      BatchId,
    int      RecipeId,
    string   RecipeName,
    string   StyleName,
    string   Status,
    DateTime StartDate,
    decimal? ActualAbv,
    int?     ActualSrm,
    decimal  EstimatedCost);

public record BatchStockWriteoffDto(
    int     IngredientId,
    string  IngredientName,
    decimal AmountWrittenOff,
    decimal StockAfter);
