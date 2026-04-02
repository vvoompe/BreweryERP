using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.Suppliers;
using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class SuppliersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public SuppliersController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SupplierDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Suppliers.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.SupplierId, s.Name, s.Edrpou))
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SupplierDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == id);
        return s is null ? NotFound() : Ok(new SupplierDto(s.SupplierId, s.Name, s.Edrpou));
    }

    [HttpPost]
    [Authorize(Policy = "WarehouseOrAdmin")]
    [ProducesResponseType(typeof(SupplierDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
    {
        var supplier = new Supplier { Name = request.Name, Edrpou = request.Edrpou };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        var dto = new SupplierDto(supplier.SupplierId, supplier.Name, supplier.Edrpou);
        return CreatedAtAction(nameof(GetById), new { id = supplier.SupplierId }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "WarehouseOrAdmin")]
    [ProducesResponseType(typeof(SupplierDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateSupplierRequest request)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        supplier.Name   = request.Name;
        supplier.Edrpou = request.Edrpou;
        await _db.SaveChangesAsync();
        return Ok(new SupplierDto(supplier.SupplierId, supplier.Name, supplier.Edrpou));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
