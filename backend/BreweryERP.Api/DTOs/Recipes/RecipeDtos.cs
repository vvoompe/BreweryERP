namespace BreweryERP.Api.DTOs.Recipes;

// ── Requests ──────────────────────────────────────────────────────────────────

public record RecipeItemRequest(int IngredientId, decimal Amount);

public record CreateRecipeRequest(
    int                     StyleId,
    string                  VersionName,
    IList<RecipeItemRequest> Items);

/// <summary>
/// При оновленні рецепта сервіс перевіряє патерн "Версіонування":
/// якщо є пов'язані Batches — створює нову версію, старий → IsActive=false.
/// </summary>
public record UpdateRecipeRequest(
    string                  NewVersionName,
    IList<RecipeItemRequest> Items);

// ── Responses ─────────────────────────────────────────────────────────────────

public record RecipeItemDto(
    int     IngredientId,
    string  IngredientName,
    string  IngredientType,
    decimal Amount,
    string  Unit);

public record RecipeDto(
    int                  RecipeId,
    int                  StyleId,
    string               StyleName,
    string               VersionName,
    bool                 IsActive,
    IList<RecipeItemDto> Items);

public record RecipeListDto
{
    public int    RecipeId    { get; init; }
    public string StyleName   { get; init; } = string.Empty;
    public string VersionName { get; init; } = string.Empty;
    public bool   IsActive    { get; init; }
    public int    ItemCount   { get; init; }
}
