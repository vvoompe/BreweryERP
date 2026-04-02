using BreweryERP.Api.DTOs.Recipes;
using BreweryERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BreweryERP.Api.Controllers;

/// <summary>
/// Контролер рецептів. Реалізує патерн "Версіонування":
/// PUT не перезаписує рецепт якщо він вже використовувався у Batch,
/// а створює нову активну версію і архівує стару (IsActive=false).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _service;
    public RecipesController(IRecipeService service) => _service = service;

    /// <summary>Список рецептів. activeOnly=true — тільки активні.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RecipeListDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetAllAsync(activeOnly));

    /// <summary>Рецепт з повним складом інгредієнтів.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RecipeDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Створити новий рецепт (Admin, Brewer).</summary>
    [HttpPost]
    [Authorize(Policy = "BrewerOrAdmin")]
    [ProducesResponseType(typeof(RecipeDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateRecipeRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.RecipeId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex)    { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Оновити рецепт (Admin, Brewer).
    /// ★ Версіонування: якщо є Batches → архівує поточний і створює новий.
    /// Відповідь завжди містить активний (новий або оновлений) рецепт.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "BrewerOrAdmin")]
    [ProducesResponseType(typeof(RecipeDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRecipeRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Видалити рецепт тільки якщо немає Batches (Admin).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
