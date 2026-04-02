using BreweryERP.Api.Models;

namespace BreweryERP.Api.DTOs.Ingredients;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateIngredientRequest(
    string        Name,
    IngredientType Type,
    string        Unit = "kg");

public record UpdateIngredientRequest(
    string        Name,
    IngredientType Type,
    string        Unit);

// ── Responses ─────────────────────────────────────────────────────────────────

public record IngredientDto(
    int            IngredientId,
    string         Name,
    string         Type,
    decimal        TotalStock,
    string         Unit);
