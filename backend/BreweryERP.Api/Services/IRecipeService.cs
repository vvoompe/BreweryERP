using BreweryERP.Api.DTOs.Recipes;

namespace BreweryERP.Api.Services;

public interface IRecipeService
{
    Task<IEnumerable<RecipeListDto>> GetAllAsync(bool activeOnly = false);
    Task<RecipeDto?> GetByIdAsync(int id);
    Task<RecipeDto> CreateAsync(CreateRecipeRequest request);
    /// <summary>
    /// Патерн "Версіонування":
    /// — Якщо для рецепта ще немає жодного Batch → оновлює існуючий запис.
    /// — Якщо Batch існують → позначає поточний IsActive=false і створює новий рецепт.
    /// </summary>
    Task<RecipeDto> UpdateAsync(int recipeId, UpdateRecipeRequest request);
    Task DeleteAsync(int id);
}
