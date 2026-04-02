using BreweryERP.Api.DTOs.Auth;
using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BreweryERP.Api.Controllers;

/// <summary>
/// Управління персоналом — список, зміна ролі, видалення.
/// Доступно тільки Admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser>  _users;
    private readonly RoleManager<IdentityRole>     _roles;

    public UsersController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole>    roles)
    {
        _users = users;
        _roles = roles;
    }

    // ── GET /api/users ────────────────────────────────────────────────────────
    /// <summary>Список всіх співробітників з ролями.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StaffDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var allUsers = _users.Users.ToList();

        var result = new List<StaffDto>();
        foreach (var user in allUsers)
        {
            var userRoles = await _users.GetRolesAsync(user);
            result.Add(new StaffDto(
                user.Id,
                user.Email!,
                user.FullName,
                userRoles.FirstOrDefault() ?? "—",
                user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow));
        }

        return Ok(result.OrderBy(u => u.FullName));
    }

    // ── PUT /api/users/{id}/role ───────────────────────────────────────────────
    /// <summary>Змінити роль співробітника.</summary>
    [HttpPut("{id}/role")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateStaffRequest request)
    {
        var allowed = new[] { "Admin", "Warehouse", "Brewer" };
        if (!allowed.Contains(request.Role))
            return BadRequest(new { message = $"Роль має бути: {string.Join(", ", allowed)}" });

        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        // Знімаємо всі поточні ролі
        var currentRoles = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, currentRoles);

        // Призначаємо нову
        await _users.AddToRoleAsync(user, request.Role);

        return NoContent();
    }

    // ── PUT /api/users/{id}/lock ───────────────────────────────────────────────
    /// <summary>Заблокувати / розблокувати акаунт.</summary>
    [HttpPut("{id}/lock")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var isCurrentlyLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

        if (isCurrentlyLocked)
        {
            // Розблоковуємо
            await _users.SetLockoutEndDateAsync(user, null);
        }
        else
        {
            // Блокуємо на 100 років
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        }

        return NoContent();
    }

    // ── DELETE /api/users/{id} ────────────────────────────────────────────────
    /// <summary>Видалити співробітника.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string id)
    {
        // Заборона видаляти себе
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (id == currentUserId)
            return BadRequest(new { message = "Ви не можете видалити власний акаунт." });

        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        await _users.DeleteAsync(user);
        return NoContent();
    }

    // ── GET /api/users/me ─────────────────────────────────────────────────────
    /// <summary>Дані поточного авторизованого користувача.</summary>
    [HttpGet("me")]
    [Authorize]   // будь-яка роль, не тільки Admin
    [ProducesResponseType(typeof(StaffDto), 200)]
    public async Task<IActionResult> GetMe()
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;

        var user = await _users.FindByEmailAsync(email ?? "");
        if (user is null) return NotFound();

        var roles = await _users.GetRolesAsync(user);
        return Ok(new StaffDto(
            user.Id,
            user.Email!,
            user.FullName,
            roles.FirstOrDefault() ?? "—",
            false));
    }
}
