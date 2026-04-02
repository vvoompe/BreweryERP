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
public class ProductSkusController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ProductSkusController(ApplicationDbContext db) => _db = db;

    /// <summary>Список SKU, опціонально фільтр за batch.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductSkuDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] int? batchId)
    {
        var query = _db.ProductSkus.AsNoTracking().AsQueryable();
        if (batchId.HasValue) query = query.Where(s => s.BatchId == batchId.Value);

        var list = await query
            .OrderByDescending(s => s.SkuId)
            .Select(s => new ProductSkuDto(
                s.SkuId, s.BatchId, s.PackagingType.ToString(), s.Price, s.QuantityInStock))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Деталі SKU.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductSkuDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await _db.ProductSkus.AsNoTracking().FirstOrDefaultAsync(x => x.SkuId == id);
        return s is null ? NotFound()
            : Ok(new ProductSkuDto(s.SkuId, s.BatchId, s.PackagingType.ToString(), s.Price, s.QuantityInStock));
    }

    /// <summary>Додати SKU до готової партії (Admin, Brewer).</summary>
    [HttpPost]
    [Authorize(Policy = "BrewerOrAdmin")]
    [ProducesResponseType(typeof(ProductSkuDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateProductSkuRequest request)
    {
        var batchExists = await _db.Batches.AnyAsync(b => b.BatchId == request.BatchId);
        if (!batchExists) return NotFound(new { message = $"Batch {request.BatchId} not found." });

        var sku = new ProductSku
        {
            BatchId         = request.BatchId,
            PackagingType   = request.PackagingType,
            Price           = request.Price,
            QuantityInStock = request.QuantityInStock
        };
        _db.ProductSkus.Add(sku);
        await _db.SaveChangesAsync();

        var dto = new ProductSkuDto(sku.SkuId, sku.BatchId,
                                    sku.PackagingType.ToString(), sku.Price, sku.QuantityInStock);
        return CreatedAtAction(nameof(GetById), new { id = sku.SkuId }, dto);
    }

    /// <summary>Скоригувати ціну або кількість (Admin).</summary>
    [HttpPatch("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ProductSkuDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Patch(int id,
        [FromBody] CreateProductSkuRequest request)
    {
        var sku = await _db.ProductSkus.FindAsync(id);
        if (sku is null) return NotFound();
        sku.Price           = request.Price;
        sku.QuantityInStock = request.QuantityInStock;
        await _db.SaveChangesAsync();
        return Ok(new ProductSkuDto(sku.SkuId, sku.BatchId,
                                    sku.PackagingType.ToString(), sku.Price, sku.QuantityInStock));
    }
}
