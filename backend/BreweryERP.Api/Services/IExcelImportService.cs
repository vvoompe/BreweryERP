using BreweryERP.Api.DTOs.Import;
using BreweryERP.Api.DTOs.SupplyInvoices;

namespace BreweryERP.Api.Services;

public interface IExcelImportService
{
    /// <summary>
    /// Парсить файл (xlsx або csv) з заданим mapping та повертає preview.
    /// colXxx = 0 → авто-визначити за назвою заголовка.
    /// </summary>
    Task<ExcelPreviewDto> ParsePreviewAsync(
        IFormFile file,
        int dataStartRow = 2,
        int colName      = 0,
        int colType      = 0,
        int colQty       = 0,
        int colUnit      = 0,
        int colExp       = 0,
        int colPrice     = 0);

    /// <summary>Підтверджує і зберігає, повертає ImportResultDto з лічильниками.</summary>
    Task<(SupplyInvoiceDto Invoice, ImportResultDto Result)> CommitImportAsync(
        ExcelImportRequest request, string userEmail, string fileName);

    /// <summary>Генерує порожній .xlsx шаблон.</summary>
    Task<byte[]> GetTemplateAsync();

    /// <summary>Журнал імпортів.</summary>
    Task<IEnumerable<ImportLogDto>> GetLogsAsync();
}
