using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.Recipes;
using BreweryERP.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Services;

/// <summary>
/// Реалізує патерн "Версіонування" для рецептів.
///
/// Правило: якщо для рецепта вже існує хоча б один Batch (варка),
/// то при оновленні рецепт не перезаписується — замість цього:
///   1. Старий рецепт позначається IsActive = false.
///   2. Створюється новий рецепт з новим VersionName та оновленими Items.
/// Якщо Batch ще немає — рецепт оновлюється in-place.
/// </summary>
public class RecipeService : IRecipeService
{
    private readonly ApplicationDbContext _db;

    public RecipeService(ApplicationDbContext db) => _db = db;

    // ── GET ALL ───────────────────────────────────────────────────────────────
    public async Task<IEnumerable<RecipeListDto>> GetAllAsync(bool activeOnly = false)
    {
        var query = _db.Recipes
            .AsNoTracking()
            .Include(r => r.Style)
            .Include(r => r.Items)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(r => r.IsActive);

        return await query
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.StyleId)
            .Select(r => new RecipeListDto(
                r.RecipeId,
                r.Style.Name,
                r.VersionName,
                r.IsActive,
                r.Items.Count))
            .ToListAsync();
    }

    // ── GET BY ID ─────────────────────────────────────────────────────────────
    public async Task<RecipeDto?> GetByIdAsync(int id)
    {
        var recipe = await _db.Recipes
            .AsNoTracking()
            .Include(r => r.Style)
            .Include(r => r.Items)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.RecipeId == id);

        return recipe is null ? null : MapToDto(recipe);
    }

    // ── CREATE ────────────────────────────────────────────────────────────────
    public async Task<RecipeDto> CreateAsync(CreateRecipeRequest request)
    {
        await ValidateItemsAsync(request.Items);

        var recipe = new Recipe
        {
            StyleId     = request.StyleId,
            VersionName = request.VersionName,
            IsActive    = true,
            Items       = request.Items.Select(i => new RecipeItem
            {
                IngredientId = i.IngredientId,
                Amount       = i.Amount
            }).ToList()
        };

        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(recipe.RecipeId)
            ?? throw new InvalidOperationException("Failed to reload recipe.");
    }

    // ── UPDATE (★ ПАТЕРН "ВЕРСІОНУВАННЯ") ────────────────────────────────────
    public async Task<RecipeDto> UpdateAsync(int recipeId, UpdateRecipeRequest request)
    {
        var existing = await _db.Recipes
            .Include(r => r.Items)
            .Include(r => r.Batches)
            .FirstOrDefaultAsync(r => r.RecipeId == recipeId)
            ?? throw new KeyNotFoundException($"Recipe {recipeId} not found.");

        if (!existing.IsActive)
            throw new InvalidOperationException("Cannot update an inactive (archived) recipe version.");

        await ValidateItemsAsync(request.Items);

        // ════ РІШЕННЯ: перевіряємо чи є пов'язані Batches ════
        bool hasBatches = existing.Batches.Count > 0;

        if (!hasBatches)
        {
            // ─── Немає Batches → оновлюємо in-place ───────────────────────
            existing.VersionName = request.NewVersionName;

            // Замінюємо всі Items: видаляємо старі, додаємо нові
            _db.RecipeItems.RemoveRange(existing.Items);
            existing.Items = request.Items.Select(i => new RecipeItem
            {
                RecipeId     = existing.RecipeId,
                IngredientId = i.IngredientId,
                Amount       = i.Amount
            }).ToList();

            await _db.SaveChangesAsync();
            return await GetByIdAsync(existing.RecipeId)
                ?? throw new InvalidOperationException("Failed to reload recipe.");
        }
        else
        {
            // ─── Є Batches → ВЕРСІОНУВАННЯ ─────────────────────────────────
            // 1. Архівуємо поточну версію
            existing.IsActive = false;

            // 2. Створюємо нову версію (новий запис у БД)
            var newVersion = new Recipe
            {
                StyleId     = existing.StyleId,
                VersionName = request.NewVersionName,
                IsActive    = true,
                Items       = request.Items.Select(i => new RecipeItem
                {
                    IngredientId = i.IngredientId,
                    Amount       = i.Amount
                }).ToList()
            };

            _db.Recipes.Add(newVersion);
            await _db.SaveChangesAsync();

            return await GetByIdAsync(newVersion.RecipeId)
                ?? throw new InvalidOperationException("Failed to reload new recipe version.");
        }
    }

    // ── DELETE ────────────────────────────────────────────────────────────────
    public async Task DeleteAsync(int id)
    {
        var recipe = await _db.Recipes
            .Include(r => r.Batches)
            .FirstOrDefaultAsync(r => r.RecipeId == id)
            ?? throw new KeyNotFoundException($"Recipe {id} not found.");

        if (recipe.Batches.Count > 0)
            throw new InvalidOperationException(
                "Cannot delete recipe that has associated batches. Archive it instead.");

        _db.Recipes.Remove(recipe);
        await _db.SaveChangesAsync();
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private async Task ValidateItemsAsync(IList<RecipeItemRequest> items)
    {
        if (items is null || items.Count == 0)
            throw new ArgumentException("Recipe must contain at least one ingredient.");

        var ids = items.Select(i => i.IngredientId).Distinct().ToList();
        var found = await _db.Ingredients.CountAsync(i => ids.Contains(i.IngredientId));
        if (found != ids.Count)
            throw new KeyNotFoundException("One or more ingredient IDs are invalid.");
    }

    private static RecipeDto MapToDto(Recipe r) => new(
        r.RecipeId,
        r.StyleId,
        r.Style.Name,
        r.VersionName,
        r.IsActive,
        r.Items.Select(ri => new RecipeItemDto(
            ri.IngredientId,
            ri.Ingredient.Name,
            ri.Ingredient.Type.ToString(),
            ri.Amount,
            ri.Ingredient.Unit)).ToList());
}
