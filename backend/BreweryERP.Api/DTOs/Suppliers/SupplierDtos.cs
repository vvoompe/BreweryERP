namespace BreweryERP.Api.DTOs.Suppliers;

public record CreateSupplierRequest(string Name, string? Edrpou);

public record SupplierDto(int SupplierId, string Name, string? Edrpou);
