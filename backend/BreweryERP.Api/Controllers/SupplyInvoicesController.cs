using BreweryERP.Api.DTOs.SupplyInvoices;
using BreweryERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BreweryERP.Api.Controllers;

/// <summary>
/// Контролер накладних. Реалізує патерн "Головний-підлеглий":
/// POST приймає Master + масив Detail у єдиному запиті,
/// сервіс зберігає їх транзакційно та оновлює TotalStock (роль Warehouse).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class SupplyInvoicesController : ControllerBase
{
    private readonly ISupplyInvoiceService _service;
    public SupplyInvoicesController(ISupplyInvoiceService service) => _service = service;

    /// <summary>Список усіх накладних.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SupplyInvoiceListDto>), 200)]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    /// <summary>Накладна з повним списком рядків.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SupplyInvoiceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Створення накладної з рядками (Master-Detail, один запит).
    /// При збереженні автоматично збільшується TotalStock інгредієнтів.
    /// Доступно: Warehouse, Admin.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "WarehouseOrAdmin")]
    [ProducesResponseType(typeof(SupplyInvoiceDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateSupplyInvoiceRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.InvoiceId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex)    { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Видалення накладної (Admin). Items видаляються каскадно.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
