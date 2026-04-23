using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.ActivityLogs;
using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class ActivityLogsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ActivityLogsController(ApplicationDbContext db) => _db = db;

    /// <summary>Отримати останні події.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ActivityLogDto>), 200)]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 20)
    {
        var logs = await _db.ActivityLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .Select(x => new ActivityLogDto(
                x.LogId, x.Action, x.EntityName, x.EntityId, x.Details, x.Timestamp, x.UserName))
            .ToListAsync();

        return Ok(logs);
    }
}
