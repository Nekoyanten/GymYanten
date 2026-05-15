// SECURITY FIX #7 — ILogger<AccountController> para audit trail de accesos

using GymYanten.Models.ViewModels;
using GymYanten.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GymYanten.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole>     _roleManager;
        // SECURITY FIX #7 — Logger para audit trail
        private readonly ILogger<AccountController>    _logger;

        public AccountController(
            UserManager<ApplicationUser>  userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole>     roleManager,
            ILogger<AccountController>    logger)      // SECURITY FIX #7
        {
            _userManager  = userManager;
            _signInManager = signInManager;
            _roleManager  = roleManager;
            _logger       = logger;
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid) return View(model);

            // SECURITY FIX #7 — Registrar intento de login (sin contraseña, solo email + IP)
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation(
                "[SECURITY] Intento de login — Email: {Email} | IP: {IP}",
                model.Email, ip);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password,
                model.RecordarMe, lockoutOnFailure: true); // lockoutOnFailure: true activa el conteo de Identity

            if (result.Succeeded)
            {
                // SECURITY FIX #7 — Audit: login exitoso
                _logger.LogInformation(
                    "[SECURITY] Login exitoso — Email: {Email} | IP: {IP}",
                    model.Email, ip);

                return LocalRedirect(returnUrl ?? "/");
            }

            if (result.IsLockedOut)
            {
                // SECURITY FIX #7 — Audit: cuenta bloqueada
                _logger.LogWarning(
                    "[SECURITY] Cuenta BLOQUEADA — Email: {Email} | IP: {IP}",
                    model.Email, ip);

                ModelState.AddModelError(string.Empty, "Cuenta bloqueada temporalmente. Intenta de nuevo en 15 minutos.");
                return View(model);
            }

            // SECURITY FIX #7 — Audit: fallo de autenticación
            _logger.LogWarning(
                "[SECURITY] Login FALLIDO — Email: {Email} | IP: {IP}",
                model.Email, ip);

            ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
            return View(model);
        }

        // GET: /Account/Registro
        [AllowAnonymous]
        public IActionResult Registro() => View();

        // POST: /Account/Registro
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var usuario = new ApplicationUser
            {
                UserName  = model.Email,
                Email     = model.Email,
                Nombre    = model.Nombre,
                Apellido  = model.Apellido,
                Telefono  = model.Telefono,
                Activo    = true
            };

            var result = await _userManager.CreateAsync(usuario, model.Password);

            if (result.Succeeded)
            {
                // SECURITY FIX #7 — Audit: nuevo registro
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                _logger.LogInformation(
                    "[SECURITY] Nuevo usuario registrado — Email: {Email} | IP: {IP}",
                    model.Email, ip);

                await _userManager.AddToRoleAsync(usuario, Roles.Cliente);
                await _signInManager.SignInAsync(usuario, isPersistent: false);
                TempData["Exito"] = "¡Bienvenido! Tu cuenta fue creada exitosamente.";
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // SECURITY FIX #7 — Audit: logout
            var email = User.Identity?.Name ?? "unknown";
            var ip    = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation(
                "[SECURITY] Logout — Email: {Email} | IP: {IP}", email, ip);

            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
