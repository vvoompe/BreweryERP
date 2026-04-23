using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.SalesOrders;
using BreweryERP.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Services;

/// <summary>
/// Реалізує патерн "Головний-підлеглий" для замовлень.
/// Ціна фіксується з ProductSku.Price у момент створення замовлення (PriceAtMoment).
/// </summary>
public class SalesOrderService : ISalesOrderService
{
    private readonly ApplicationDbContext _db;

    public SalesOrderService(ApplicationDbContext db) => _db = db;

    // ── GET ALL ───────────────────────────────────────────────────────────────
    public async Task<IEnumerable<SalesOrderListDto>> GetAllAsync()
    {
        return await _db.SalesOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new SalesOrderListDto(
                o.OrderId,
                o.Client.Name,
                o.OrderDate,
                o.Status.ToString(),
                o.Items.Sum(i => i.Quantity * i.PriceAtMoment),
                o.Items.Sum(i => i.Quantity * i.PriceAtMoment) - o.Items.Sum(i => i.Quantity * i.ProductSku.UnitCost),
                o.Items.Count))
            .ToListAsync();
    }

    // ── GET BY ID ─────────────────────────────────────────────────────────────
    public async Task<SalesOrderDto?> GetByIdAsync(int id)
    {
        var order = await _db.SalesOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductSku)
                    .ThenInclude(sku => sku.Batch)
                        .ThenInclude(b => b.Recipe)
                            .ThenInclude(r => r.Style)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        return order is null ? null : MapToDto(order);
    }

    // ── CREATE (Master-Detail) ────────────────────────────────────────────────
    public async Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest request)
    {
        // Валідація клієнта
        var clientExists = await _db.Clients.AnyAsync(c => c.ClientId == request.ClientId);
        if (!clientExists)
            throw new KeyNotFoundException($"Client {request.ClientId} not found.");

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Order must contain at least one item.");

        // Bulk-завантаження SKU
        var skuIds = request.Items.Select(i => i.SkuId).Distinct().ToList();
        var skus = await _db.ProductSkus
            .Where(s => skuIds.Contains(s.SkuId))
            .ToDictionaryAsync(s => s.SkuId);

        // Валідація SKU
        var notFound = skuIds.Except(skus.Keys).ToList();
        if (notFound.Count > 0)
            throw new KeyNotFoundException($"SKUs not found: {string.Join(", ", notFound)}");

        // Валідація залишків
        var stockErrors = request.Items
            .Where(i => skus[i.SkuId].QuantityInStock < i.Quantity)
            .Select(i => $"SKU {i.SkuId}: requested {i.Quantity}, " +
                         $"available {skus[i.SkuId].QuantityInStock}")
            .ToList();
        if (stockErrors.Count > 0)
            throw new InvalidOperationException(
                "Insufficient stock:\n" + string.Join("\n", stockErrors));

        // ════ ТРАНЗАКЦІЯ (через ExecutionStrategy — сумісно з EnableRetryOnFailure) ════
        SalesOrderDto? created = null;
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. Створення Master-замовлення
                var order = new SalesOrder
                {
                    ClientId  = request.ClientId,
                    OrderDate = DateTime.UtcNow,
                    Status    = OrderStatus.New
                };
                _db.SalesOrders.Add(order);
                await _db.SaveChangesAsync(); // Отримуємо OrderId

                // 2. Додавання Detail-рядків + фіксація ціни (PriceAtMoment)
                foreach (var itemReq in request.Items)
                {
                    var sku = skus[itemReq.SkuId];

                    _db.OrderItems.Add(new OrderItem
                    {
                        OrderId       = order.OrderId,
                        SkuId         = itemReq.SkuId,
                        Quantity      = itemReq.Quantity,
                        PriceAtMoment = sku.Price   // ★ Фіксуємо поточну ціну
                    });

                    // Резервуємо кількість при статусі New
                    // (повне списання — при переході в Shipped)
                }

                _db.ActivityLogs.Add(new ActivityLog {
                    Action = "Order Created",
                    EntityName = "SalesOrder",
                    EntityId = order.OrderId,
                    Details = $"Created order for Client #{request.ClientId} with {request.Items.Count} items",
                    UserName = "System"
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                created = await GetByIdAsync(order.OrderId)
                    ?? throw new InvalidOperationException("Failed to reload order.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });

        return created!;
    }

    // ── UPDATE STATUS ─────────────────────────────────────────────────────────
    public async Task<SalesOrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusRequest request)
    {
        var order = await _db.SalesOrders
            .Include(o => o.Client)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductSku)
                    .ThenInclude(sku => sku.Batch)
                        .ThenInclude(b => b.Recipe)
                            .ThenInclude(r => r.Style)
            .FirstOrDefaultAsync(o => o.OrderId == orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        // Валідація переходів
        var allowed = (order.Status, request.Status) switch
        {
            (OrderStatus.New,      OrderStatus.Reserved) => true,
            (OrderStatus.Reserved, OrderStatus.Shipped)  => true,
            (OrderStatus.Shipped,  OrderStatus.Paid)     => true,
            (OrderStatus.New,      OrderStatus.Shipped)  => true, // прямий відвантаж
            _ => false
        };

        if (!allowed)
            throw new InvalidOperationException(
                $"Invalid status transition: {order.Status} → {request.Status}");

        // ★ При переході в Shipped — фізичне списання залишків складу
        if (request.Status == OrderStatus.Shipped)
        {
            foreach (var item in order.Items)
            {
                if (item.ProductSku.QuantityInStock < item.Quantity)
                    throw new InvalidOperationException(
                        $"SKU {item.SkuId}: insufficient stock to ship.");

                item.ProductSku.QuantityInStock -= item.Quantity;
            }
        }

        order.Status = request.Status;

        _db.ActivityLogs.Add(new ActivityLog {
            Action = "Order Status Changed",
            EntityName = "SalesOrder",
            EntityId = order.OrderId,
            Details = $"Status changed to {request.Status}",
            UserName = "System"
        });

        await _db.SaveChangesAsync();

        return MapToDto(order);
    }

    // ── MAPPING ───────────────────────────────────────────────────────────────
    private static SalesOrderDto MapToDto(SalesOrder o)
    {
        var totalAmount = o.Items.Sum(i => i.Quantity * i.PriceAtMoment);
        var totalCost   = o.Items.Sum(i => i.Quantity * i.ProductSku.UnitCost);
        var margin      = totalAmount - totalCost;
        var marginPct   = totalAmount > 0 ? (margin / totalAmount) * 100 : 0;

        return new SalesOrderDto(
            o.OrderId,
            o.ClientId,
            o.Client.Name,
            o.OrderDate,
            o.Status.ToString(),
            totalAmount,
            totalCost,
            margin,
            Math.Round(marginPct, 2),
            o.Items.Select(i => new OrderItemDto(
                i.SkuId,
                i.ProductSku?.Batch?.Recipe?.Style != null 
                    ? $"{i.ProductSku.Batch.Recipe.Style.Name} ({i.ProductSku.Batch.Recipe.VersionName})" 
                    : $"SKU #{i.SkuId}",
                i.ProductSku?.PackagingType.ToString() ?? "",
                i.Quantity,
                i.PriceAtMoment,
                i.Quantity * i.PriceAtMoment)).ToList());
    }
}
