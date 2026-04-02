using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BreweryERP.Api.DTOs.Auth;
using BreweryERP.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace BreweryERP.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser>  _userManager;
    private readonly IConfiguration                _config;

    public AuthService(
        UserManager<ApplicationUser>  userManager,
        IConfiguration                config)
    {
        _userManager   = userManager;
        _config        = config;
    }

    // ── LOGIN ─────────────────────────────────────────────────────────────────
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Невірний email або пароль.");

        // Перевіряємо чи акаунт заблокований
        if (await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("Акаунт заблокований. Зверніться до адміністратора.");

        // Перевірка пароля без SignInManager (чисто JWT — без cookies)
        var passwordOk = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordOk)
        {
            await _userManager.AccessFailedAsync(user);  // лічильник невдалих спроб
            throw new UnauthorizedAccessException("Невірний email або пароль.");
        }

        // Скидаємо лічильник після успішного входу
        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return await BuildTokenAsync(user, roles);
    }

    // ── REGISTER ──────────────────────────────────────────────────────────────
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var allowed = new[] { "Admin", "Warehouse", "Brewer" };
        if (!allowed.Contains(request.Role))
            throw new ArgumentException($"Role must be one of: {string.Join(", ", allowed)}");

        var user = new ApplicationUser
        {
            Email    = request.Email,
            UserName = request.Email,
            FullName = request.FullName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        var roles = await _userManager.GetRolesAsync(user);
        return await BuildTokenAsync(user, roles);
    }

    // ── TOKEN BUILDER ─────────────────────────────────────────────────────────
    private Task<AuthResponse> BuildTokenAsync(ApplicationUser user, IList<string> roles)
    {
        var jwtSection = _config.GetSection("JwtSettings");
        var secret     = jwtSection["Secret"]!;
        var issuer     = jwtSection["Issuer"]!;
        var audience   = jwtSection["Audience"]!;
        var hours      = int.Parse(jwtSection["ExpirationHours"]!);

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new("fullName",                    user.FullName),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        // Додаємо ролі як claims
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expires = DateTime.UtcNow.AddHours(hours);
        var token   = new JwtSecurityToken(issuer, audience, claims,
                          expires: expires, signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Task.FromResult(new AuthResponse(
            tokenString,
            user.Email!,
            user.FullName,
            roles,
            roles.FirstOrDefault() ?? string.Empty,
            expires));
    }
}
