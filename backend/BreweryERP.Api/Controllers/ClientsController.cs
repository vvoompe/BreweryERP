using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.SalesOrders;
using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyRole")]
[Produces("application/json")]
public class ClientsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ClientsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClientDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var query = _db.Clients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || (c.Phone != null && c.Phone.Contains(search)));

        return Ok(await query.OrderBy(c => c.Name)
            .Select(c => new ClientDto(c.ClientId, c.Name, c.Phone))
            .ToListAsync());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClientDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == id);
        return c is null ? NotFound() : Ok(new ClientDto(c.ClientId, c.Name, c.Phone));
    }

    [HttpPost]
    [Authorize(Policy = "AnyRole")]
    [ProducesResponseType(typeof(ClientDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request)
    {
        var client = new Client { Name = request.Name, Phone = request.Phone };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        var dto = new ClientDto(client.ClientId, client.Name, client.Phone);
        return CreatedAtAction(nameof(GetById), new { id = client.ClientId }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AnyRole")]
    [ProducesResponseType(typeof(ClientDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateClientRequest request)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client is null) return NotFound();
        client.Name  = request.Name;
        client.Phone = request.Phone;
        await _db.SaveChangesAsync();
        return Ok(new ClientDto(client.ClientId, client.Name, client.Phone));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client is null) return NotFound();
        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
