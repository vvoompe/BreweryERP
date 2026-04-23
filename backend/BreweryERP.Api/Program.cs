using System.Text;
using BreweryERP.Api.Data;
using BreweryERP.Api.Models;
using BreweryERP.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════════════════════
// DATABASE — MySQL + Pomelo EF Core Provider
// ══════════════════════════════════════════════════════════════════════════════
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(3)));

// ══════════════════════════════════════════════════════════════════════════════
// IDENTITY — Users, Roles, Password policy
// ══════════════════════════════════════════════════════════════════════════════
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength         = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase       = false;
        options.User.RequireUniqueEmail         = true;
        // Підтримка блокування акаунтів
        options.Lockout.AllowedForNewUsers      = true;
        options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromDays(36500); // ~100 years
        options.Lockout.MaxFailedAccessAttempts = 99; // ручне блокування, не автоматичне
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ══════════════════════════════════════════════════════════════════════════════
// JWT AUTHENTICATION
// ══════════════════════════════════════════════════════════════════════════════
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtSecret  = jwtSection["Secret"]!;

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero  // точне закінчення токена
        };
    });

// ══════════════════════════════════════════════════════════════════════════════
// AUTHORIZATION — Policy-based (Admin, Warehouse, Brewer)
// ══════════════════════════════════════════════════════════════════════════════
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",     p => p.RequireRole("Admin"));
    options.AddPolicy("WarehouseOrAdmin", p => p.RequireRole("Admin", "Warehouse"));
    options.AddPolicy("BrewerOrAdmin",    p => p.RequireRole("Admin", "Brewer"));
    options.AddPolicy("AnyRole",       p => p.RequireRole("Admin", "Warehouse", "Brewer"));
});

// ══════════════════════════════════════════════════════════════════════════════
// APPLICATION SERVICES (DI)
// ══════════════════════════════════════════════════════════════════════════════
builder.Services.AddScoped<IAuthService,          AuthService>();
builder.Services.AddScoped<ISupplyInvoiceService, SupplyInvoiceService>();
builder.Services.AddScoped<IRecipeService,        RecipeService>();
builder.Services.AddScoped<IBatchService,         BatchService>();
builder.Services.AddScoped<ISalesOrderService,    SalesOrderService>();
builder.Services.AddScoped<IExcelImportService,   ExcelImportService>();

// ══════════════════════════════════════════════════════════════════════════════
// CORS — дозволяємо Angular dev server
// ══════════════════════════════════════════════════════════════════════════════
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!;
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ══════════════════════════════════════════════════════════════════════════════
// SWAGGER — з підтримкою JWT авторизації в UI
// ══════════════════════════════════════════════════════════════════════════════
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Brewery ERP API",
        Version = "v1",
        Description = "CRM/ERP система для управління крафтовою броварнею"
    });

    // Кнопка "Authorize" у Swagger UI з Bearer JWT
    var securityScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Введіть JWT токен (без префікса Bearer)"
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ══════════════════════════════════════════════════════════════════════════════
// PIPELINE
// ══════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// Seed roles at startup
await SeedRolesAsync(app.Services);

// Swagger доступний завжди (для локальної розробки)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Brewery ERP v1");
    c.DocumentTitle = "🍺 Brewery ERP API";
    c.RoutePrefix = "swagger";
});

// HTTPS redirect вимкнено для локальної розробки (XAMPP — HTTP only)
// app.UseHttpsRedirection();
app.UseCors("AngularPolicy");
app.UseAuthentication();   // ← до UseAuthorization!
app.UseAuthorization();
app.MapControllers();

app.Run();

// ══════════════════════════════════════════════════════════════════════════════
// SEED — Roles (Admin, Warehouse, Brewer)
// ══════════════════════════════════════════════════════════════════════════════
static async Task SeedRolesAsync(IServiceProvider services)
{
    using var scope       = services.CreateScope();
    var roleManager       = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var requiredRoles     = new[] { "Admin", "Warehouse", "Brewer" };

    foreach (var role in requiredRoles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}
