using BreweryERP.Api.DTOs.Import;
using BreweryERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BreweryERP.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Policy = "WarehouseOrAdmin")]
public class ExcelImportController : ControllerBase
{
    private readonly IExcelImportService _importService;
    public ExcelImportController(IExcelImportService importService) => _importService = importService;

    // ── GET /api/import/template ──────────────────────────────────────────────
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate()
    {
        var bytes = await _importService.GetTemplateAsync();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "supply_invoice_template.xlsx");
    }

    // ── POST /api/import/preview ──────────────────────────────────────────────
    /// <summary>
    /// Парсить файл і повертає preview.
    /// Query params для mapping (0 = авто-визначити за заголовком):
    ///   dataStartRow, colName, colType, colQty, colUnit, colExp, colPrice
    /// </summary>
    [HttpPost("preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ExcelPreviewDto>> Preview(
        IFormFile file,
        [FromQuery] int dataStartRow = 2,
        [FromQuery] int colName  = 0,
        [FromQuery] int colType  = 0,
        [FromQuery] int colQty   = 0,
        [FromQuery] int colUnit  = 0,
        [FromQuery] int colExp   = 0,
        [FromQuery] int colPrice = 0)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Файл не завантажено." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".csv")
            return BadRequest(new { message = "Дозволені формати: .xlsx, .csv" });

        try
        {
            var preview = await _importService.ParsePreviewAsync(
                file, dataStartRow, colName, colType, colQty, colUnit, colExp, colPrice);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── POST /api/import/commit ───────────────────────────────────────────────
    [HttpPost("commit")]
    public async Task<IActionResult> Commit(
        [FromBody] ExcelImportRequest request,
        [FromQuery] string fileName = "imported")
    {
        if (request.Rows.Count == 0)
            return BadRequest(new { message = "Немає рядків для імпорту." });

        var userEmail = User.FindFirstValue(ClaimTypes.Email)
                     ?? User.FindFirstValue("email")
                     ?? "unknown";
        try
        {
            var (_, result) = await _importService.CommitImportAsync(request, userEmail, fileName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── GET /api/import/logs ──────────────────────────────────────────────────
    [HttpGet("logs")]
    public async Task<ActionResult<IEnumerable<ImportLogDto>>> GetLogs()
        => Ok(await _importService.GetLogsAsync());
}
