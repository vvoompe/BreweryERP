using BreweryERP.Api.DTOs.Batches;
using BreweryERP.Api.Models;

namespace BreweryERP.Api.Services;

public interface IBatchService
{
    Task<IEnumerable<BatchDto>> GetAllAsync();
    Task<BatchDto?> GetByIdAsync(int id);
    /// <summary>
    /// Бізнес-логіка ролі Brewer:
    /// транзакційно створює Batch та списує TotalStock кожного інгредієнта
    /// з рецепту. Кидає InvalidOperationException якщо запасів не вистачає.
    /// </summary>
    Task<(BatchDto Batch, IList<BatchStockWriteoffDto> Writeoffs)> CreateAsync(CreateBatchRequest request);
    Task<BatchDto> UpdateStatusAsync(int batchId, UpdateBatchStatusRequest request);
}
