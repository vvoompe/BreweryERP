using BreweryERP.Api.DTOs.SalesOrders;
using BreweryERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BreweryERP.Api.Controllers;

/// <summary>
/// Контролер замовлень.
/// Патерн "Головний-підлеглий": POST приймає замовлення + масив позицій.
/// PriceAtMoment фіксується автоматично сервісом з ProductSku.Price.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class SalesOrdersController : ControllerBase
{
    private readonly ISalesOrderService _service;
    public SalesOrdersController(ISalesOrderService service) => _service = service;

    /// <summary>Список замовлень із сумою та статусом.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SalesOrderListDto>), 200)]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    /// <summary>Повні деталі замовлення з позиціями.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SalesOrderDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Створити замовлення з позиціями (Master-Detail, один запит).
    /// Ціна кожної позиції фіксується з поточного SKU.Price.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AnyRole")]
    [ProducesResponseType(typeof(SalesOrderDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateSalesOrderRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.OrderId }, result);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Змінити статус замовлення.
    /// Допустимі переходи: New→Reserved→Shipped→Paid.
    /// При переході в Shipped — списується QuantityInStock у ProductSku.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "AnyRole")]
    [ProducesResponseType(typeof(SalesOrderDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var result = await _service.UpdateStatusAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
