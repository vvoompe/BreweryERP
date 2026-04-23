using System.Text;
using BreweryERP.Api.DTOs.Import;
using BreweryERP.Api.Models;

namespace BreweryERP.Api.Services;

/// <summary>
/// Чисті статичні методи парсингу CSV/Excel — ізольовані від I/O,
/// легко покриваються unit-тестами без мокування.
/// </summary>
public static class ImportParser
{
    // ── COLUMN LETTER ─────────────────────────────────────────────────────────

    /// <summary>1 → A, 2 → B, 27 → AA</summary>
    public static string ColLetter(int col)
    {
        if (col <= 0) return "?";
        var s = "";
        while (col > 0)
        {
            int rem = (col - 1) % 26;
            s = (char)('A' + rem) + s;
            col = (col - 1) / 26;
        }
        return s;
    }

    // ── CSV DELIMITER DETECTION ───────────────────────────────────────────────

    private static readonly char[] Delimiters = [',', ';', '\t', '|'];

    /// <summary>
    /// Визначає роздільник CSV за першим рядком.
    /// Якщо жоден не знайдено — повертає кому як дефолт.
    /// </summary>
    public static char DetectDelimiter(string line)
    {
        if (string.IsNullOrEmpty(line)) return ',';

        var best = Delimiters
            .Select(d => (Delimiter: d, Count: line.Count(c => c == d)))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        return best.Count > 0 ? best.Delimiter : ',';
    }

    // ── CSV LINE PARSER ───────────────────────────────────────────────────────

    /// <summary>
    /// RFC 4180-сумісний парсер рядка CSV.
    /// Підтримує: поля в лапках, подвоєні лапки як escape, довільний роздільник.
    /// </summary>
    public static string[] ParseCsvLine(string line, char delimiter)
    {
        var result   = new List<string>();
        var current  = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // пропускаємо другу лапку
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
        return [.. result];
    }

    // ── COLUMN INFO BUILDER ───────────────────────────────────────────────────

    public static IList<ColumnInfo> BuildColumnInfo(string?[] headers) =>
        headers
            .Select((h, i) => new ColumnInfo(i + 1, ColLetter(i + 1),
                string.IsNullOrWhiteSpace(h) ? null : h.Trim()))
            .ToList();

    // ── AUTO DETECT MAPPING ───────────────────────────────────────────────────

    private static readonly (string Field, string[] Keywords)[] MappingRules =
    [
        ("name",  ["назва", "name", "ingredient", "інгредієнт", "найменування"]),
        ("type",  ["тип", "type", "вид", "категорія", "category"]),
        ("qty",   ["кількість", "qty", "quantity", "кіл", "amount", "об'єм", "об"]),
        ("unit",  ["одиниця", "unit", "од.", "міра", "units"]),
        ("exp",   ["дата", "date", "expir", "закінч", "термін", "строк", "годен"]),
        ("price", ["ціна", "price", "вартість", "cost", "прайс", "тариф"]),
    ];

    /// <summary>
    /// Авто-визначення mapping за заголовками (UA + EN, регістронезалежно).
    /// Повертає 0 для колонок, які не знайдені.
    /// </summary>
    public static DetectedMapping AutoDetect(string?[] headers)
    {
        static bool Matches(string? h, string[] keywords) =>
            h is not null && keywords.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase));

        int Find(string field)
        {
            var keywords = MappingRules.First(r => r.Field == field).Keywords;
            for (int i = 0; i < headers.Length; i++)
                if (Matches(headers[i], keywords)) return i + 1;
            return 0;
        }

        return new DetectedMapping(
            ColName:       Find("name"),
            ColType:       Find("type"),
            ColQuantity:   Find("qty"),
            ColUnit:       Find("unit"),
            ColExpiration: Find("exp"),
            ColUnitPrice:  Find("price"));
    }

    // ── ROW VALIDATION ────────────────────────────────────────────────────────

    /// <summary>
    /// Перевіряє один рядок накладної.
    /// Повертає рядок помилки або null якщо все OK.
    /// </summary>
    public static string? ValidateRow(
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

        if (!Enum.TryParse<IngredientType>(typStr, ignoreCase: true, out ingType))
            errs.Add($"Невідомий тип \"{typStr}\". Допустимо: Malt, Hop, Yeast, Additive, Water");

        if (!decimal.TryParse(qtyStr.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out qty) || qty <= 0)
            errs.Add($"Кількість \"{qtyStr}\" має бути число > 0");

        if (string.IsNullOrWhiteSpace(unit))
            errs.Add("Відсутня одиниця виміру");

        if (!string.IsNullOrWhiteSpace(expStr))
        {
            string[] fmts = ["dd.MM.yyyy", "d.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy"];
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

    // ── MAPPING RESOLVER ──────────────────────────────────────────────────────

    /// <summary>Merge авто-detected з user-provided (user-provided має пріоритет).</summary>
    public static (int ColName, int ColType, int ColQty, int ColUnit, int ColExp, int ColPrice)
        ResolveMapping(DetectedMapping auto, int colName, int colType, int colQty,
                       int colUnit, int colExp, int colPrice)
    {
        static int R(int user, int detected) => user > 0 ? user : detected;
        return (R(colName, auto.ColName), R(colType, auto.ColType),
                R(colQty,  auto.ColQuantity), R(colUnit, auto.ColUnit),
                R(colExp,  auto.ColExpiration), R(colPrice, auto.ColUnitPrice));
    }
}
