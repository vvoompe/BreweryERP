using BreweryERP.Api.DTOs.Batches;
using BreweryERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BreweryERP.Api.Controllers;

/// <summary>
/// Контролер варок. Бізнес-логіка ролі Brewer:
/// POST автоматично списує сировину зі складу згідно рецепту.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class BatchesController : ControllerBase
{
    private readonly IBatchService _service;
    public BatchesController(IBatchService service) => _service = service;

    /// <summary>Список усіх варок.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BatchDto>), 200)]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    /// <summary>Деталі варки.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BatchDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Розпочати нову варку (Brewer, Admin).
    /// ★ Автоматично списує TotalStock для кожного інгредієнта рецепту.
    /// Відповідь містить Batch + список списаних залишків.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "BrewerOrAdmin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateBatchRequest request)
    {
        try
        {
            var (batch, writeoffs) = await _service.CreateAsync(request);
            return Ok(new { batch, stockWriteoffs = writeoffs });
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Оновити статус варки (Brewer, Admin).
    /// Допустимі переходи: Brewing→Fermenting→Completed/Failed.
    /// При Completed можна вказати ActualAbv та ActualSrm.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "BrewerOrAdmin")]
    [ProducesResponseType(typeof(BatchDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBatchStatusRequest request)
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
