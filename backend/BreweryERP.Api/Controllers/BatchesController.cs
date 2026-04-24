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
    private readonly ILogger<BatchesController> _logger;
    public BatchesController(IBatchService service, ILogger<BatchesController> logger)
    {
        _service = service;
        _logger = logger;
    }

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
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "CreateBatch: KeyNotFoundException");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "CreateBatch: InvalidOperationException");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateBatch: Unhandled Exception during batch creation. Payload: {@Request}", request);
            return StatusCode(500, new { message = "Внутрішня помилка сервера при створенні партії.", error = ex.Message });
        }
    }

    /// <summary>Оновити партію (повне редагування).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "BrewerOrAdmin")]
    [ProducesResponseType(typeof(BatchDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBatchRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "UpdateBatch: KeyNotFoundException for {BatchId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateBatch: Error updating batch {BatchId}", id);
            return StatusCode(500, new { message = ex.Message });
        }
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
