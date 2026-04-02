namespace BreweryERP.Api.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Role);   // "Admin" | "Warehouse" | "Brewer"

/// <summary>Відповідь автентифікації — токен + дані користувача.</summary>
public record AuthResponse(
    string         Token,
    string         Email,
    string         FullName,
    IList<string>  Roles,
    string         Role,       // перша роль (для фронтенду)
    DateTime       ExpiresAt);

// ── Staff management DTOs ────────────────────────────────────────────────────

/// <summary>Список співробітників (для адмін-панелі).</summary>
public record StaffDto(
    string   Id,
    string   Email,
    string   FullName,
    string   Role,
    bool     IsLocked);

/// <summary>Запит на зміну ролі або блокування.</summary>
public record UpdateStaffRequest(string Role);
