namespace BreweryERP.Api.DTOs.BeerStyles;

public record CreateBeerStyleRequest(
    string   Name,
    int?     TargetSrm,
    decimal? TargetAbv);

public record BeerStyleDto(
    int      StyleId,
    string   Name,
    int?     TargetSrm,
    decimal? TargetAbv);
