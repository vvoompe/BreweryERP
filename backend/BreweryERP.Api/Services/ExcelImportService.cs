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
/// Підтримує:
///   - авто-детекцію колонок за назвами заголовків
///   - ручне налаштування mapping (colName, colType, ...)
///   - вибір рядка початку даних (dataStartRow)
///   - авто-визначення роздільника CSV (кома / крапка з комою / TAB)
///   - авто-BOM detection для UTF-8
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

        // 1. Читаємо сирі рядки
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

        // 2. Авто-визначення mapping (якщо user не задав кастомний)
        var suggested = AutoDetect(raw.Headers);
        var mapping   = ResolveMapping(suggested, colName, colType, colQty, colUnit, colExp, colPrice);

        if (mapping.ColName == 0 || mapping.ColType == 0 ||
            mapping.ColQty  == 0 || mapping.ColUnit == 0)
        {
            globalErrors.Add(
                "Не вдалося автоматично визначити обов'язкові колонки (Назва, Тип, Кількість, Одиниця). " +
                "Вкажіть mapping вручну.");
            return new ExcelPreviewDto([], 0, 0, globalErrors, raw.Columns, suggested);
        }

        // 3. Завантажуємо існуючі інгредієнти для матчингу
        var allIngredients = await _db.Ingredients
            .AsNoTracking()
            .ToDictionaryAsync(i => i.Name.ToLowerInvariant());

        // 4. Парсимо рядки
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

            var rowError = ValidateRow(name, typStr, qtyStr, unit, expStr, priceStr,
                out var qty, out _, out var expDate, out var price);

            allIngredients.TryGetValue(name.ToLowerInvariant(), out var existing);

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
                    var existing = await _db.Ingredients
                        .FirstOrDefaultAsync(i => i.Name.ToLower() == row.IngredientName.ToLower());

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

            var createRequest = new CreateSupplyInvoiceRequest(
                request.SupplierId, request.DocNumber, request.ReceiveDate, itemRequests);

            var invoice = await _invoiceService.CreateAsync(createRequest);

            log.Status    = "Success";
            log.InvoiceId = invoice.InvoiceId;
            await _db.SaveChangesAsync();

            var result = new ImportResultDto(
                invoice.InvoiceId,
                invoice.DocNumber,
                invoice.Items.Count,
                newIngredients,
                $"Успішно імпортовано {invoice.Items.Count} позицій" +
                (newIngredients > 0 ? $", створено {newIngredients} нових інгредієнтів" : ""));

            return (invoice, result);
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
        IList<ColumnInfo>           Columns,
        string?[]                   Headers,
        IList<(int RowNum, string[] Cells)> AllRows);

    private static bool IsExcel(string fileName) =>
        Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    /// <summary>Читає xlsx — повертає header і data rows.</summary>
    private static RawFileData ReadExcel(IFormFile file, int dataStartRow)
    {
        using var stream = file.OpenReadStream();
        using var wb     = new XLWorkbook(stream);

        var ws = wb.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Excel файл не містить жодного аркуша.");

        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        int lastRow = ws.LastRowUsed()?.RowNumber()       ?? 0;

        // Заголовки з рядка 1
        var headers = Enumerable.Range(1, lastCol)
            .Select(c => (string?)ws.Cell(1, c).GetString().Trim())
            .ToArray();

        var columns = BuildColumnInfo(headers);

        // Дані з dataStartRow
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

    /// <summary>Читає CSV з авто-детекцією роздільника та BOM.</summary>
    private static RawFileData ReadCsv(IFormFile file, int dataStartRow)
    {
        using var stream = file.OpenReadStream();
        // Підтримуємо UTF-8 BOM
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line != null) lines.Add(line);
        }

        if (lines.Count == 0)
            throw new InvalidOperationException("CSV файл порожній.");

        // Авто-детекція роздільника за першим рядком
        char delimiter = DetectDelimiter(lines[0]);

        // Рядок заголовків (рядок 1)
        var headers = ParseCsvLine(lines[0], delimiter)
            .Select(h => (string?)h.Trim())
            .ToArray();

        var columns = BuildColumnInfo(headers);

        var rows = new List<(int, string[])>();
        for (int i = dataStartRow - 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = ParseCsvLine(lines[i], delimiter)
                .Select(c => c.Trim())
                .ToArray();
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add((i + 1, cells));
        }

        return new RawFileData(columns, headers, rows);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE: CSV HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static char DetectDelimiter(string line)
    {
        // Вибираємо роздільник за кількістю входжень у першому рядку
        var candidates = new[] { ',', ';', '\t', '|' };
        return candidates
            .OrderByDescending(d => line.Count(c => c == d))
            .First();
    }

    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var result  = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                // Подвоєна лапка всередині — escape
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE: AUTO-DETECT & MAPPING
    // ══════════════════════════════════════════════════════════════════════════

    private static IList<ColumnInfo> BuildColumnInfo(string?[] headers)
    {
        return headers
            .Select((h, i) => new ColumnInfo(i + 1, ColLetter(i + 1), h))
            .ToList();
    }

    /// <summary>
    /// Авто-визначення mapping за назвами заголовків (UA + EN, регістронезалежно).
    /// Повертає 0 для колонок що не знайдені.
    /// </summary>
    private static DetectedMapping AutoDetect(string?[] headers)
    {
        static bool Matches(string? h, params string[] keywords) =>
            h is not null && keywords.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase));

        int Find(params string[] keywords)
        {
            for (int i = 0; i < headers.Length; i++)
                if (Matches(headers[i], keywords)) return i + 1;
            return 0;
        }

        return new DetectedMapping(
            ColName:       Find("назва", "name", "ingredient", "інгредієнт"),
            ColType:       Find("тип", "type", "вид", "категорія", "category"),
            ColQuantity:   Find("кількість", "qty", "quantity", "кіл", "amount"),
            ColUnit:       Find("одиниця", "unit", "од.", "міра"),
            ColExpiration: Find("дата", "date", "expir", "закінч", "термін", "строк"),
            ColUnitPrice:  Find("ціна", "price", "вартість", "cost", "прайс"));
    }

    /// <summary>Merge авто-detected з user-provided (user-provided має пріоритет).</summary>
    private static (int ColName, int ColType, int ColQty, int ColUnit, int ColExp, int ColPrice)
        ResolveMapping(DetectedMapping auto,
            int colName, int colType, int colQty,
            int colUnit, int colExp, int colPrice)
    {
        int R(int user, int detected) => user > 0 ? user : detected;
        return (
            R(colName,  auto.ColName),
            R(colType,  auto.ColType),
            R(colQty,   auto.ColQuantity),
            R(colUnit,  auto.ColUnit),
            R(colExp,   auto.ColExpiration),
            R(colPrice, auto.ColUnitPrice));
    }

    private static string ColLetter(int col)
    {
        var s = "";
        while (col > 0)
        {
            int rem = (col - 1) % 26;
            s = (char)('A' + rem) + s;
            col = (col - 1) / 26;
        }
        return s;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE: ROW VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    private static string? ValidateRow(
        string   name,
        string   typStr,
        string   qtyStr,
        string   unit,
        string   expStr,
        string   priceStr,
        out decimal        qty,
        out IngredientType ingType,
        out DateOnly?      expDate,
        out decimal?       price)
    {
        qty     = 0;
        ingType = IngredientType.Additive;
        expDate = null;
        price   = null;
        var errs = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errs.Add("Відсутня назва інгредієнта");

        if (!Enum.TryParse<IngredientType>(typStr, true, out ingType))
            errs.Add($"Невідомий тип \"{typStr}\". Допустимо: Malt, Hop, Yeast, Additive, Water");

        if (!decimal.TryParse(qtyStr.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out qty) || qty <= 0)
            errs.Add($"Кількість \"{qtyStr}\" має бути число > 0");

        if (string.IsNullOrWhiteSpace(unit))
            errs.Add("Відсутня одиниця виміру");

        if (!string.IsNullOrWhiteSpace(expStr))
        {
            string[] fmts = ["dd.MM.yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "d.MM.yyyy", "dd/MM/yyyy"];
            if (!DateOnly.TryParseExact(expStr, fmts,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var d))
                errs.Add($"Некоректний формат дати \"{expStr}\"");
            else
                expDate = d;
        }

        if (!string.IsNullOrWhiteSpace(priceStr))
        {
            if (!decimal.TryParse(priceStr.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) || p < 0)
                errs.Add($"Ціна \"{priceStr}\" має бути число >= 0");
            else
                price = p;
        }

        return errs.Count > 0 ? string.Join("; ", errs) : null;
    }
}
