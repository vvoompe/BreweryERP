using System.Text;
using BreweryERP.Api.Data;
using BreweryERP.Api.DTOs.Import;
using BreweryERP.Api.DTOs.SupplyInvoices;
using BreweryERP.Api.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BreweryERP.Api.Services;

/// <summary>
/// Сервіс імпорту накладних з Excel (.xlsx) та CSV.
/// Чиста логіка парсингу делегується в <see cref="ImportParser"/>.
/// </summary>
public class ExcelImportService : IExcelImportService
{
    private readonly ApplicationDbContext  _db;
    private readonly ISupplyInvoiceService _invoiceService;

    public ExcelImportService(ApplicationDbContext db, ISupplyInvoiceService invoiceService)
    {
        _db             = db;
        _invoiceService = invoiceService;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PARSE PREVIEW
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ExcelPreviewDto> ParsePreviewAsync(
        IFormFile file,
        int dataStartRow = 2,
        int colName  = 0,
        int colType  = 0,
        int colQty   = 0,
        int colUnit  = 0,
        int colExp   = 0,
        int colPrice = 0)
    {
        var globalErrors = new List<string>();

        RawFileData raw;
        try
        {
            raw = IsExcel(file.FileName)
                ? ReadExcel(file, dataStartRow)
                : ReadCsv(file, dataStartRow);
        }
        catch (Exception ex)
        {
            globalErrors.Add($"Помилка читання файлу: {ex.Message}");
            return new ExcelPreviewDto([], 0, 0, globalErrors, [], null);
        }

        if (raw.AllRows.Count == 0)
        {
            globalErrors.Add("Файл не містить рядків з даними.");
            return new ExcelPreviewDto([], 0, 0, globalErrors, raw.Columns, null);
        }

        var suggested = ImportParser.AutoDetect(raw.Headers);
        var mapping   = ImportParser.ResolveMapping(suggested, colName, colType, colQty, colUnit, colExp, colPrice);

        if (mapping.ColName == 0 || mapping.ColType == 0 ||
            mapping.ColQty  == 0 || mapping.ColUnit == 0)
        {
            globalErrors.Add(
                "Не вдалося автоматично визначити обов'язкові колонки (Назва, Тип, Кількість, Одиниця). " +
                "Вкажіть mapping вручну.");
            return new ExcelPreviewDto([], 0, 0, globalErrors, raw.Columns, suggested);
        }

        var allIngredients = await _db.Ingredients
            .AsNoTracking()
            .ToDictionaryAsync(i => i.Name.ToLowerInvariant()); // ★ FIX: ToLowerInvariant

        var rows = new List<ExcelRowPreview>();
        foreach (var (rowNum, cells) in raw.AllRows)
        {
            string Get(int col) => (col > 0 && col <= cells.Length) ? cells[col - 1] : "";

            var name     = Get(mapping.ColName).Trim();
            var typStr   = Get(mapping.ColType).Trim();
            var qtyStr   = Get(mapping.ColQty).Trim();
            var unit     = Get(mapping.ColUnit).Trim();
            var expStr   = mapping.ColExp   > 0 ? Get(mapping.ColExp).Trim()   : "";
            var priceStr = mapping.ColPrice > 0 ? Get(mapping.ColPrice).Trim() : "";

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(qtyStr)) continue;

            var rowError = ImportParser.ValidateRow(name, typStr, qtyStr, unit, expStr, priceStr,
                out var qty, out _, out var expDate, out var price);

            allIngredients.TryGetValue(name.ToLowerInvariant(), out var existing); // ★ FIX

            rows.Add(new ExcelRowPreview(
                RowNumber:       rowNum,
                IngredientName:  name,
                IngredientType:  typStr,
                Quantity:        qty,
                Unit:            unit,
                ExpirationDate:  expDate?.ToString("yyyy-MM-dd"),
                UnitPrice:       price,
                IsNew:           existing is null,
                IngredientId:    existing?.IngredientId,
                Error:           rowError));
        }

        int valid  = rows.Count(r => r.Error is null);
        int errors = rows.Count(r => r.Error is not null);
        return new ExcelPreviewDto(rows, valid, errors, globalErrors, raw.Columns, suggested);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // COMMIT IMPORT
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ FIX: тепер ingredient auto-creation та invoice creation виконуються
    /// в одній транзакції, щоб уникнути orphaned ingredients при помилці.
    /// </summary>
    public async Task<(SupplyInvoiceDto Invoice, ImportResultDto Result)> CommitImportAsync(
        ExcelImportRequest request, string userEmail, string fileName)
    {
        var log = new ImportLog
        {
            FileName   = fileName,
            ImportedAt = DateTime.UtcNow,
            ImportedBy = userEmail,
            RowCount   = request.Rows.Count,
            Status     = "Processing"
        };
        _db.ImportLogs.Add(log);
        await _db.SaveChangesAsync();

        int newIngredients = 0;

        try
        {
            // ★ FIX: вся операція — одна транзакція
            await using var tx = await _db.Database.BeginTransactionAsync();

            var itemRequests = new List<InvoiceItemRequest>();

            foreach (var row in request.Rows)
            {
                int ingredientId;
                if (row.IngredientId.HasValue)
                {
                    ingredientId = row.IngredientId.Value;
                }
                else
                {
                    // ★ FIX: ToLowerInvariant для консистентності
                    var existing = await _db.Ingredients
                        .FirstOrDefaultAsync(i =>
                            i.Name.ToLower() == row.IngredientName.ToLowerInvariant());

                    if (existing is not null)
                    {
                        ingredientId = existing.IngredientId;
                    }
                    else
                    {
                        if (!Enum.TryParse<IngredientType>(row.IngredientType, true, out var ingType))
                            ingType = IngredientType.Additive;

                        var newIng = new Ingredient
                        {
                            Name       = row.IngredientName,
                            Type       = ingType,
                            Unit       = row.Unit,
                            TotalStock = 0
                        };
                        _db.Ingredients.Add(newIng);
                        await _db.SaveChangesAsync();
                        ingredientId = newIng.IngredientId;
                        newIngredients++;
                    }
                }

                itemRequests.Add(new InvoiceItemRequest(
                    ingredientId, row.Quantity, row.UnitPrice, row.ExpirationDate));
            }

            // Перевіряємо постачальника перед CreateAsync
            var supplierExists = await _db.Suppliers.AnyAsync(s => s.SupplierId == request.SupplierId);
            if (!supplierExists)
                throw new KeyNotFoundException($"Постачальника з ID {request.SupplierId} не знайдено.");

            // Створюємо накладну напряму (без транзакції всередині — ми вже в транзакції)
            var invoice = new SupplyInvoice
            {
                SupplierId  = request.SupplierId,
                DocNumber   = request.DocNumber,
                ReceiveDate = request.ReceiveDate ?? DateTime.UtcNow
            };
            _db.SupplyInvoices.Add(invoice);
            await _db.SaveChangesAsync();

            var ingredients = await _db.Ingredients
                .Where(i => itemRequests.Select(ir => ir.IngredientId).Contains(i.IngredientId))
                .ToDictionaryAsync(i => i.IngredientId);

            foreach (var ir in itemRequests)
            {
                var ingredient = ingredients[ir.IngredientId];
                ingredient.TotalStock += ir.Quantity; // ★ TotalStock update

                _db.InvoiceItems.Add(new InvoiceItem
                {
                    InvoiceId      = invoice.InvoiceId,
                    IngredientId   = ir.IngredientId,
                    Quantity       = ir.Quantity,
                    UnitPrice      = ir.UnitPrice,
                    ExpirationDate = ir.ExpirationDate
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Перезавантажуємо для маппінгу DTO
            var invoiceDto = await _invoiceService.GetByIdAsync(invoice.InvoiceId)
                ?? throw new InvalidOperationException("Не вдалося перезавантажити накладну після збереження.");

            log.Status    = "Success";
            log.InvoiceId = invoice.InvoiceId;
            await _db.SaveChangesAsync();

            var result = new ImportResultDto(
                invoice.InvoiceId,
                invoice.DocNumber,
                itemRequests.Count,
                newIngredients,
                $"Успішно імпортовано {itemRequests.Count} позицій" +
                (newIngredients > 0 ? $", створено {newIngredients} нових інгредієнтів" : ""));

            return (invoiceDto, result);
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Error  = ex.Message[..Math.Min(ex.Message.Length, 500)];
            await _db.SaveChangesAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TEMPLATE
    // ══════════════════════════════════════════════════════════════════════════

    public Task<byte[]> GetTemplateAsync()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Накладна");

        string[] headers = ["Назва інгредієнта", "Тип", "Кількість", "Одиниця", "Дата закінчення", "Ціна/од"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold            = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D4860B");
            cell.Style.Font.FontColor       = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        string[] hints =
        [
            "Наприклад: Pilsner Malt",
            "Malt / Hop / Yeast / Additive / Water",
            "500",
            "kg / g / L / mL / pcs",
            "31.12.2025 (необов'язково)",
            "25.50 (необов'язково)"
        ];
        for (int c = 0; c < hints.Length; c++)
        {
            var cell = ws.Cell(2, c + 1);
            cell.Value = hints[c];
            cell.Style.Font.Italic    = true;
            cell.Style.Font.FontColor = XLColor.Gray;
        }

        ws.Cell(3, 1).Value = "Pilsner Malt";  ws.Cell(3, 2).Value = "Malt";
        ws.Cell(3, 3).Value = 500;              ws.Cell(3, 4).Value = "kg";
        ws.Cell(3, 5).Value = "31.12.2025";     ws.Cell(3, 6).Value = 25.50;

        ws.Cell(4, 1).Value = "Cascade Hops";  ws.Cell(4, 2).Value = "Hop";
        ws.Cell(4, 3).Value = 50;              ws.Cell(4, 4).Value = "kg";
        ws.Cell(4, 5).Value = "";              ws.Cell(4, 6).Value = 180.00;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LOGS
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<ImportLogDto>> GetLogsAsync()
    {
        return await _db.ImportLogs
            .AsNoTracking()
            .OrderByDescending(l => l.ImportedAt)
            .Select(l => new ImportLogDto(
                l.ImportId, l.FileName, l.ImportedAt, l.ImportedBy,
                l.InvoiceId, l.Status, l.Error, l.RowCount))
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE: FILE READERS
    // ══════════════════════════════════════════════════════════════════════════

    private record RawFileData(
        IList<ColumnInfo>                    Columns,
        string?[]                            Headers,
        IList<(int RowNum, string[] Cells)>  AllRows);

    private static bool IsExcel(string fileName) =>
        Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    private static RawFileData ReadExcel(IFormFile file, int dataStartRow)
    {
        using var stream = file.OpenReadStream();
        using var wb     = new XLWorkbook(stream);

        var ws = wb.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Excel файл не містить жодного аркуша.");

        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        int lastRow = ws.LastRowUsed()?.RowNumber()       ?? 0;

        var headers = Enumerable.Range(1, lastCol)
            .Select(c => (string?)ws.Cell(1, c).GetString().Trim())
            .ToArray();

        var columns = ImportParser.BuildColumnInfo(headers);

        var rows = new List<(int, string[])>();
        for (int r = dataStartRow; r <= lastRow; r++)
        {
            var cells = Enumerable.Range(1, lastCol)
                .Select(c => ws.Cell(r, c).GetString().Trim())
                .ToArray();
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add((r, cells));
        }

        return new RawFileData(columns, headers, rows);
    }

    private static RawFileData ReadCsv(IFormFile file, int dataStartRow)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line != null) lines.Add(line);
        }

        if (lines.Count == 0)
            throw new InvalidOperationException("CSV файл порожній.");

        char delimiter = ImportParser.DetectDelimiter(lines[0]);

        var headers = ImportParser.ParseCsvLine(lines[0], delimiter)
            .Select(h => (string?)h.Trim())
            .ToArray();

        var columns = ImportParser.BuildColumnInfo(headers);

        var rows = new List<(int, string[])>();
        for (int i = dataStartRow - 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = ImportParser.ParseCsvLine(lines[i], delimiter)
                .Select(c => c.Trim())
                .ToArray();
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add((i + 1, cells));
        }

        return new RawFileData(columns, headers, rows);
    }
}
