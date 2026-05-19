// SECURITY FIX — Program.cs hardened:
//   #1  CustomIdentityPasswordHasher (PBKDF2-HMAC-SHA256, 100k iter)
//   #2  Lockout: 5 intentos fallidos → 15 min bloqueo
//   #3  Rate Limiting en /Account/Login via middleware personalizado (compatible con MVC)

using GymYanten.Data;
using GymYanten.Models;
using GymYanten.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// ── Base de datos ─────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// SECURITY FIX #1 — Registrar PasswordHasherService y el adaptador para Identity
// IMPORTANTE: registrar ANTES de AddIdentity para que DI lo resuelva correctamente
builder.Services.AddScoped<PasswordHasherService>();
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, CustomIdentityPasswordHasher>();

// ── ASP.NET Core Identity ─────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Contraseña
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Cuenta
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;

    // SECURITY FIX #2 — Lockout explícito
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Cookie de sesión ──────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ── MVC ───────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// SECURITY FIX #3 — Rate Limiting manual por IP para POST /Account/Login
// Compatible con routing MVC convencional (no usa MapPost que genera conflicto).
// Ventana deslizante: máximo 10 intentos por IP por minuto.
var loginAttempts = new ConcurrentDictionary<string, (int Count, DateTime WindowStart)>();
const int MaxLoginAttempts = 10;
const int WindowSeconds = 60;

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Account/Login")
        && context.Request.Method == "POST")
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;

        var entry = loginAttempts.AddOrUpdate(
            ip,
            (_) => (1, now),
            (_, old) =>
            {
                // Reiniciar ventana si ya expiró
                if ((now - old.WindowStart).TotalSeconds > WindowSeconds)
                    return (1, now);
                return (old.Count + 1, old.WindowStart);
            });

        if (entry.Count > MaxLoginAttempts)
        {
            // SECURITY FIX #7 — Loguear hit de rate limit
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                "[SECURITY] Rate limit superado en /Account/Login — IP: {IP} | Intentos: {Count}",
                ip, entry.Count);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(
                "Demasiados intentos de login. Espera un minuto e intenta de nuevo.");
            return; // Cortar el pipeline, NO llegar al controller
        }
    }

    await next(context);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── Seed ─────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al inicializar la base de datos.");
    }
}

app.Run();