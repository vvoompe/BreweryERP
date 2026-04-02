using BreweryERP.Api.DTOs.Auth;
using BreweryERP.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BreweryERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Вхід у систему. Повертає JWT токен.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _auth.LoginAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Реєстрація нового користувача. Перший запуск — без токена (bootstrap Admin).</summary>
    [HttpPost("register")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]   // ← для першого адміна; після сіду можна повернути AdminOnly
    [ProducesResponseType(typeof(AuthResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _auth.RegisterAsync(request);
            return CreatedAtAction(nameof(Login), result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
