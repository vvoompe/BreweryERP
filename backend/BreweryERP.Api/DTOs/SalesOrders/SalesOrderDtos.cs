using BreweryERP.Api.Models;

namespace BreweryERP.Api.DTOs.SalesOrders;

// ── Requests ──────────────────────────────────────────────────────────────────

public record OrderItemRequest(int SkuId, int Quantity);

/// <summary>
/// Master + Detail рядки в одному запиті (патерн "Головний-підлеглий").
/// ціна фіксується з ProductSku.Price на момент замовлення.
/// </summary>
public record CreateSalesOrderRequest(
    int                    ClientId,
    IList<OrderItemRequest> Items);

public record UpdateOrderStatusRequest(OrderStatus Status);

// ── Responses ─────────────────────────────────────────────────────────────────

public record OrderItemDto(
    int     SkuId,
    string  BeerName,
    string  PackagingType,
    int     Quantity,
    decimal PriceAtMoment,
    decimal LineTotal);

public record SalesOrderDto(
    int                 OrderId,
    int                 ClientId,
    string              ClientName,
    DateTime            OrderDate,
    string              Status,
    decimal             TotalAmount,
    decimal             TotalCost,
    decimal             ProfitMargin,
    decimal             ProfitMarginPercent,
    IList<OrderItemDto> Items);

public record SalesOrderListDto(
    int      OrderId,
    string   ClientName,
    DateTime OrderDate,
    string   Status,
    decimal  TotalAmount,
    decimal  ProfitMargin,
    int      ItemCount);

// ── Clients ───────────────────────────────────────────────────────────────────

public record CreateClientRequest(string Name, string? Phone);

public record ClientDto(int ClientId, string Name, string? Phone);

// ── ProductSkus ───────────────────────────────────────────────────────────────

public record ProductSkuDto(
    int     SkuId,
    int     BatchId,
    string  PackagingType,
    decimal Price,
    decimal UnitCost,
    int     QuantityInStock);

public record CreateProductSkuRequest(
    int          BatchId,
    PackagingType PackagingType,
    decimal      Price,
    int          QuantityInStock);
