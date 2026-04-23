namespace BreweryERP.Api.DTOs.Import;

// ── Column detection ───────────────────────────────────────────────────────────

/// <summary>Одна колонка файлу (excel-стовпець або CSV-поле).</summary>
public record ColumnInfo(
    int     Index,   // 1-based
    string  Letter,  // A, B, C... (або Col1, Col2 якщо заголовків немає)
    string? Header); // текст з першого рядка (null якщо немає заголовків)

/// <summary>Авто-визначений mapping: яка колонка відповідає якому полю (0 = не знайдено).</summary>
public record DetectedMapping(
    int ColName,
    int ColType,
    int ColQuantity,
    int ColUnit,
    int ColExpiration,  // 0 = не знайдено
    int ColUnitPrice);  // 0 = не знайдено

// ── Excel Preview ──────────────────────────────────────────────────────────────

/// <summary>Один розпарсений рядок.</summary>
public record ExcelRowPreview(
    int     RowNumber,
    string  IngredientName,
    string  IngredientType,
    decimal Quantity,
    string  Unit,
    string? ExpirationDate,
    decimal? UnitPrice,
    bool    IsNew,
    int?    IngredientId,
    string? Error);

/// <summary>Результат парсингу файлу (з metadata для mapping UI).</summary>
public record ExcelPreviewDto(
    IList<ExcelRowPreview> Rows,
    int                    ValidCount,
    int                    ErrorCount,
    IList<string>          GlobalErrors,
    IList<ColumnInfo>      Columns,           // ← всі колонки файлу
    DetectedMapping?       SuggestedMapping); // ← авто-визначений mapping

// ── Import Commit ──────────────────────────────────────────────────────────────

public record ExcelRowCommit(
    string    IngredientName,
    string    IngredientType,
    decimal   Quantity,
    string    Unit,
    decimal?  UnitPrice,
    DateOnly? ExpirationDate,
    int?      IngredientId);

public record ExcelImportRequest(
    int       SupplierId,
    string    DocNumber,
    DateTime? ReceiveDate,
    IList<ExcelRowCommit> Rows);

/// <summary>Результат підтвердженого імпорту — з лічильником.</summary>
public record ImportResultDto(
    int    InvoiceId,
    string DocNumber,
    int    ImportedRows,   // ← кількість рядків що реально імпортовано
    int    NewIngredients, // ← автоматично створено нових інгредієнтів
    string Message);

// ── Import Log ─────────────────────────────────────────────────────────────────

public record ImportLogDto(
    int      ImportId,
    string   FileName,
    DateTime ImportedAt,
    string   ImportedBy,
    int?     InvoiceId,
    string   Status,
    string?  Error,
    int      RowCount);
