namespace BreweryERP.Api.DTOs.SupplyInvoices;

// ── Requests ──────────────────────────────────────────────────────────────────

/// <summary>
/// Рядок (Detail) накладної — частина запиту на створення Master-Detail документа.
/// </summary>
public record InvoiceItemRequest(
    int       IngredientId,
    decimal   Quantity,
    decimal?  UnitPrice,
    DateOnly? ExpirationDate);

/// <summary>
/// Master + всі Detail-рядки в одному запиті.
/// Патерн "Головний-підлеглий": транзакційне збереження на бекенді.
/// </summary>
public record CreateSupplyInvoiceRequest(
    int                      SupplierId,
    string                   DocNumber,
    DateTime?                ReceiveDate,                  // null → CURRENT_TIMESTAMP
    IList<InvoiceItemRequest> Items);

// ── Responses ─────────────────────────────────────────────────────────────────

public record InvoiceItemDto(
    int       IngredientId,
    string    IngredientName,
    decimal   Quantity,
    decimal?  UnitPrice,
    string    Unit,
    DateOnly? ExpirationDate);

public record SupplyInvoiceDto(
    int                   InvoiceId,
    int                   SupplierId,
    string                SupplierName,
    string                DocNumber,
    DateTime              ReceiveDate,
    IList<InvoiceItemDto> Items);

public record SupplyInvoiceListDto(
    int      InvoiceId,
    string   SupplierName,
    string   DocNumber,
    DateTime ReceiveDate,
    int      ItemCount);
