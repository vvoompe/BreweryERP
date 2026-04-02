using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.BeerStyles;
using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class BeerStylesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public BeerStylesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BeerStyleDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.BeerStyles.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new BeerStyleDto(s.StyleId, s.Name, s.TargetSrm, s.TargetAbv))
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BeerStyleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await _db.BeerStyles.AsNoTracking().FirstOrDefaultAsync(x => x.StyleId == id);
        return s is null ? NotFound() : Ok(new BeerStyleDto(s.StyleId, s.Name, s.TargetSrm, s.TargetAbv));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(BeerStyleDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateBeerStyleRequest request)
    {
        var style = new BeerStyle
        {
            Name      = request.Name,
            TargetSrm = request.TargetSrm,
            TargetAbv = request.TargetAbv
        };
        _db.BeerStyles.Add(style);
        await _db.SaveChangesAsync();
        var dto = new BeerStyleDto(style.StyleId, style.Name, style.TargetSrm, style.TargetAbv);
        return CreatedAtAction(nameof(GetById), new { id = style.StyleId }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(BeerStyleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateBeerStyleRequest request)
    {
        var style = await _db.BeerStyles.FindAsync(id);
        if (style is null) return NotFound();
        style.Name      = request.Name;
        style.TargetSrm = request.TargetSrm;
        style.TargetAbv = request.TargetAbv;
        await _db.SaveChangesAsync();
        return Ok(new BeerStyleDto(style.StyleId, style.Name, style.TargetSrm, style.TargetAbv));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var style = await _db.BeerStyles.FindAsync(id);
        if (style is null) return NotFound();
        _db.BeerStyles.Remove(style);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
