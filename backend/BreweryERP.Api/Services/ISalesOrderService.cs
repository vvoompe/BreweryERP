using BreweryERP.Api.DTOs.SalesOrders;
using BreweryERP.Api.Models;

namespace BreweryERP.Api.Services;

public interface ISalesOrderService
{
    Task<IEnumerable<SalesOrderListDto>> GetAllAsync();
    Task<SalesOrderDto?> GetByIdAsync(int id);
    /// <summary>
    /// Патерн "Головний-підлеглий": транзакційно зберігає Order + Items,
    /// фіксуючи PriceAtMoment зі складу на момент замовлення.
    /// </summary>
    Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest request);
    Task<SalesOrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusRequest request);
}
