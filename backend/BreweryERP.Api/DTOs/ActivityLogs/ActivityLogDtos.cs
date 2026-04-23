namespace BreweryERP.Api.DTOs.ActivityLogs;

public record ActivityLogDto(
    int      LogId,
    string   Action,
    string   EntityName,
    int      EntityId,
    string?  Details,
    DateTime Timestamp,
    string   UserName);
