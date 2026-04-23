using BreweryERP.Api.DTOs.Import;
using BreweryERP.Api.Models;
using BreweryERP.Api.Services;

namespace BreweryERP.Tests.Services;

/// <summary>
/// Unit tests for ImportParser pure static methods.
/// No DB or I/O dependencies — fast, deterministic.
/// </summary>
public class ImportParserTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // ColLetter
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1,  "A")]
    [InlineData(2,  "B")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(52, "AZ")]
    [InlineData(53, "BA")]
    public void ColLetter_ConvertsNumberToLetter(int col, string expected)
    {
        Assert.Equal(expected, ImportParser.ColLetter(col));
    }

    [Fact]
    public void ColLetter_Zero_ReturnsQuestionMark()
    {
        Assert.Equal("?", ImportParser.ColLetter(0));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DetectDelimiter
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("a,b,c,d",   ',')]
    [InlineData("a;b;c",     ';')]
    [InlineData("a\tb\tc",   '\t')]
    [InlineData("a|b|c|d|e", '|')]
    public void DetectDelimiter_FindsCorrectDelimiter(string line, char expected)
    {
        Assert.Equal(expected, ImportParser.DetectDelimiter(line));
    }

    [Fact]
    public void DetectDelimiter_EmptyLine_ReturnsComma()
    {
        Assert.Equal(',', ImportParser.DetectDelimiter(""));
    }

    [Fact]
    public void DetectDelimiter_NoKnownDelimiter_ReturnsComma()
    {
        // рядок без жодного кандидата
        Assert.Equal(',', ImportParser.DetectDelimiter("abcdef"));
    }

    [Fact]
    public void DetectDelimiter_SemicolonBeatsComma_WhenMoreFrequent()
    {
        // "a;b;c,d" — 2 крапки з комою, 1 кома → крапка з комою
        Assert.Equal(';', ImportParser.DetectDelimiter("a;b;c,d"));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ParseCsvLine
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ParseCsvLine_SimpleComma_SplitsCorrectly()
    {
        var result = ImportParser.ParseCsvLine("Pilsner Malt,Malt,500,kg", ',');
        Assert.Equal(new string[] { "Pilsner Malt", "Malt", "500", "kg" }, result);
    }

    [Fact]
    public void ParseCsvLine_Semicolon_SplitsCorrectly()
    {
        var result = ImportParser.ParseCsvLine("Cascade Hops;Hop;50;kg", ';');
        Assert.Equal(new string[] { "Cascade Hops", "Hop", "50", "kg" }, result);
    }

    [Fact]
    public void ParseCsvLine_QuotedFieldWithComma_TreatedAsSingleField()
    {
        var result = ImportParser.ParseCsvLine("\"Malt, Pilsner\",Malt,500,kg", ',');
        Assert.Equal(new string[] { "Malt, Pilsner", "Malt", "500", "kg" }, result);
    }

    [Fact]
    public void ParseCsvLine_DoubleQuoteEscape_ProducesOneQuote()
    {
        var result = ImportParser.ParseCsvLine("\"He said \"\"hello\"\"\",Malt,100,kg", ',');
        Assert.Equal(new string[] { "He said \"hello\"", "Malt", "100", "kg" }, result);
    }

    [Fact]
    public void ParseCsvLine_EmptyFields_ReturnEmptyStrings()
    {
        var result = ImportParser.ParseCsvLine("Name,,100,", ',');
        Assert.Equal(new string[] { "Name", "", "100", "" }, result);
    }

    [Fact]
    public void ParseCsvLine_SingleField_ReturnsOneElement()
    {
        var result = ImportParser.ParseCsvLine("OnlyOne", ',');
        Assert.Single(result);
        Assert.Equal("OnlyOne", result[0]);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AutoDetect
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AutoDetect_EnglishHeaders_MapsCorrectly()
    {
        string?[] headers = ["Ingredient Name", "Type", "Quantity", "Unit", "Expiration", "Price"];
        var result = ImportParser.AutoDetect(headers);

        Assert.Equal(1, result.ColName);
        Assert.Equal(2, result.ColType);
        Assert.Equal(3, result.ColQuantity);
        Assert.Equal(4, result.ColUnit);
        Assert.Equal(5, result.ColExpiration);
        Assert.Equal(6, result.ColUnitPrice);
    }

    [Fact]
    public void AutoDetect_UkrainianHeaders_MapsCorrectly()
    {
        string?[] headers = ["Назва інгредієнта", "Тип", "Кількість", "Одиниця", "Дата закінчення", "Ціна/од"];
        var result = ImportParser.AutoDetect(headers);

        Assert.Equal(1, result.ColName);
        Assert.Equal(2, result.ColType);
        Assert.Equal(3, result.ColQuantity);
        Assert.Equal(4, result.ColUnit);
        Assert.Equal(5, result.ColExpiration);
        Assert.Equal(6, result.ColUnitPrice);
    }

    [Fact]
    public void AutoDetect_CaseInsensitive_StillMatches()
    {
        string?[] headers = ["НАЗВА", "TYP", "QTY", "UNIT", "DATE", "PRICE"];
        var result = ImportParser.AutoDetect(headers);
        Assert.Equal(1, result.ColName);
        Assert.Equal(3, result.ColQuantity);
    }

    [Fact]
    public void AutoDetect_UnknownHeaders_ReturnsZero()
    {
        string?[] headers = ["Колонка1", "Колонка2", "Колонка3"];
        var result = ImportParser.AutoDetect(headers);
        Assert.Equal(0, result.ColName);
        Assert.Equal(0, result.ColType);
        Assert.Equal(0, result.ColQuantity);
        Assert.Equal(0, result.ColUnit);
    }

    [Fact]
    public void AutoDetect_NullHeader_SkipsWithoutException()
    {
        string?[] headers = [null, "Тип", null, "Кількість", "Одиниця", null];
        // не повинно кидати вийняток
        var result = ImportParser.AutoDetect(headers);
        Assert.Equal(2, result.ColType);
        Assert.Equal(4, result.ColQuantity);
        Assert.Equal(5, result.ColUnit);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ValidateRow
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateRow_ValidRow_ReturnsNull()
    {
        var error = ImportParser.ValidateRow(
            "Pilsner Malt", "Malt", "500", "kg", "31.12.2025", "25.50",
            out var qty, out var ingType, out var expDate, out var price);

        Assert.Null(error);
        Assert.Equal(500m, qty);
        Assert.Equal(IngredientType.Malt, ingType);
        Assert.Equal(new DateOnly(2025, 12, 31), expDate);
        Assert.Equal(25.50m, price);
    }

    [Fact]
    public void ValidateRow_ValidRow_OptionalFieldsOmitted_ReturnsNull()
    {
        var error = ImportParser.ValidateRow(
            "Hops", "Hop", "50", "kg", "", "",
            out var qty, out _, out var expDate, out var price);

        Assert.Null(error);
        Assert.Equal(50m, qty);
        Assert.Null(expDate);
        Assert.Null(price);
    }

    [Fact]
    public void ValidateRow_MissingName_ReturnsError()
    {
        var error = ImportParser.ValidateRow(
            "", "Malt", "100", "kg", "", "",
            out _, out _, out _, out _);

        Assert.NotNull(error);
        Assert.Contains("назва", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRow_InvalidType_ReturnsError()
    {
        var error = ImportParser.ValidateRow(
            "Something", "InvalidType", "100", "kg", "", "",
            out _, out _, out _, out _);

        Assert.NotNull(error);
        Assert.Contains("тип", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRow_ZeroQuantity_ReturnsError()
    {
        var error = ImportParser.ValidateRow(
            "Malt", "Malt", "0", "kg", "", "",
            out _, out _, out _, out _);

        Assert.NotNull(error);
        Assert.Contains("Кількість", error);
    }

    [Fact]
    public void ValidateRow_NegativeQuantity_ReturnsError()
    {
        var error = ImportParser.ValidateRow(
            "Malt", "Malt", "-5", "kg", "", "",
            out _, out _, out _, out _);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateRow_CommaAsDecimalSeparator_ParsedCorrectly()
    {
        var error = ImportParser.ValidateRow(
            "Malt", "Malt", "500,5", "kg", "", "",
            out var qty, out _, out _, out _);

        Assert.Null(error);
        Assert.Equal(500.5m, qty);
    }

    [Fact]
    public void ValidateRow_InvalidDate_ReturnsError()
    {
        var error = ImportParser.ValidateRow(
            "Malt", "Malt", "100", "kg", "не-дата", "",
            out _, out _, out _, out _);

        Assert.NotNull(error);
        Assert.Contains("дата", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("31.12.2025")]
    [InlineData("2025-12-31")]
    [InlineData("12/31/2025")]
    [InlineData("1.5.2026")]
    public void ValidateRow_ValidDateFormats_ParsedCorrectly(string dateStr)
    {
        var error = ImportParser.ValidateRow(
            "Malt", "Malt", "100", "kg", dateStr, "",
            out _, out _, out var expDate, out _);

        Assert.Null(error);
        Assert.NotNull(expDate);
    }

    [Fact]
    public void ValidateRow_NegativePrice_ReturnsError()
    {
        var error = ImportParser.ValidateRow(
            "Malt", "Malt", "100", "kg", "", "-5",
            out _, out _, out _, out _);

        Assert.NotNull(error);
        Assert.Contains("Ціна", error);
    }

    [Fact]
    public void ValidateRow_ZeroPrice_IsValid()
    {
        var error = ImportParser.ValidateRow(
            "Malt", "Malt", "100", "kg", "", "0",
            out _, out _, out _, out var price);

        Assert.Null(error);
        Assert.Equal(0m, price);
    }

    [Fact]
    public void ValidateRow_MultipleErrors_AllReturned()
    {
        var error = ImportParser.ValidateRow("", "BadType", "0", "", "", "",
            out _, out _, out _, out _);

        Assert.NotNull(error);
        // Повинно містити всі 4 помилки (назва, тип, кількість, одиниця)
        var errorCount = error!.Split(';').Length;
        Assert.Equal(4, errorCount);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BuildColumnInfo
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void BuildColumnInfo_CorrectLettersAndHeaders()
    {
        string?[] headers = ["Назва", "Тип", "Кількість"];
        var cols = ImportParser.BuildColumnInfo(headers);

        Assert.Equal(3, cols.Count);
        Assert.Equal(1, cols[0].Index); Assert.Equal("A", cols[0].Letter); Assert.Equal("Назва",     cols[0].Header);
        Assert.Equal(2, cols[1].Index); Assert.Equal("B", cols[1].Letter); Assert.Equal("Тип",       cols[1].Header);
        Assert.Equal(3, cols[2].Index); Assert.Equal("C", cols[2].Letter); Assert.Equal("Кількість", cols[2].Header);
    }

    [Fact]
    public void BuildColumnInfo_EmptyHeader_ReturnsNull()
    {
        string?[] headers = ["Назва", "", null];
        var cols = ImportParser.BuildColumnInfo(headers);
        Assert.Null(cols[1].Header);
        Assert.Null(cols[2].Header);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ResolveMapping
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ResolveMapping_UserOverridesAutoDetect()
    {
        var auto = new DetectedMapping(1, 2, 3, 4, 5, 6);
        var result = ImportParser.ResolveMapping(auto, 10, 0, 0, 0, 0, 0);
        Assert.Equal(10, result.ColName); // user overrides
        Assert.Equal(2,  result.ColType); // auto used
    }

    [Fact]
    public void ResolveMapping_AllUserZero_UsesAutoDetect()
    {
        var auto = new DetectedMapping(1, 2, 3, 4, 5, 6);
        var result = ImportParser.ResolveMapping(auto, 0, 0, 0, 0, 0, 0);
        Assert.Equal((1, 2, 3, 4, 5, 6), result);
    }
}
