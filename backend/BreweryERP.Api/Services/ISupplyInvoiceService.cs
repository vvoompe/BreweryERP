using BreweryERP.Api.DTOs.SupplyInvoices;

namespace BreweryERP.Api.Services;

public interface ISupplyInvoiceService
{
    Task<IEnumerable<SupplyInvoiceListDto>> GetAllAsync();
    Task<SupplyInvoiceDto?> GetByIdAsync(int id);
    /// <summary>
    /// Патерн "Головний-підлеглий" + бізнес-логіка ролі Warehouse:
    /// транзакційно зберігає Invoice + Items і збільшує TotalStock для кожного інгредієнта.
    /// </summary>
    Task<SupplyInvoiceDto> CreateAsync(CreateSupplyInvoiceRequest request);
    Task DeleteAsync(int id);
}
