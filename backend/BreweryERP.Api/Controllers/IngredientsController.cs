using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.Ingredients;
using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class IngredientsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public IngredientsController(ApplicationDbContext db) => _db = db;

    /// <summary>Список всіх інгредієнтів з залишками.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IngredientDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? type)
    {
        var query = _db.Ingredients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(type) &&
            Enum.TryParse<IngredientType>(type, true, out var parsedType))
        {
            query = query.Where(i => i.Type == parsedType);
        }

        var list = await query
            .OrderBy(i => i.Type).ThenBy(i => i.Name)
            .Select(i => new IngredientDto(i.IngredientId, i.Name, i.Type.ToString(), i.TotalStock, i.Unit))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Отримати інгредієнт за ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(IngredientDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var i = await _db.Ingredients.AsNoTracking().FirstOrDefaultAsync(x => x.IngredientId == id);
        return i is null
            ? NotFound()
            : Ok(new IngredientDto(i.IngredientId, i.Name, i.Type.ToString(), i.TotalStock, i.Unit));
    }

    /// <summary>Створити новий інгредієнт (Admin, Warehouse).</summary>
    [HttpPost]
    [Authorize(Policy = "WarehouseOrAdmin")]
    [ProducesResponseType(typeof(IngredientDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateIngredientRequest request)
    {
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Type = request.Type,
            Unit = request.Unit
        };
        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();

        var dto = new IngredientDto(ingredient.IngredientId, ingredient.Name,
                                    ingredient.Type.ToString(), ingredient.TotalStock, ingredient.Unit);
        return CreatedAtAction(nameof(GetById), new { id = ingredient.IngredientId }, dto);
    }

    /// <summary>Оновити інгредієнт (Admin, Warehouse).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "WarehouseOrAdmin")]
    [ProducesResponseType(typeof(IngredientDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateIngredientRequest request)
    {
        var ingredient = await _db.Ingredients.FindAsync(id);
        if (ingredient is null) return NotFound();

        ingredient.Name = request.Name;
        ingredient.Type = request.Type;
        ingredient.Unit = request.Unit;
        await _db.SaveChangesAsync();

        return Ok(new IngredientDto(ingredient.IngredientId, ingredient.Name,
                                    ingredient.Type.ToString(), ingredient.TotalStock, ingredient.Unit));
    }

    /// <summary>Видалити інгредієнт (Admin).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var ingredient = await _db.Ingredients.FindAsync(id);
        if (ingredient is null) return NotFound();
        _db.Ingredients.Remove(ingredient);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
