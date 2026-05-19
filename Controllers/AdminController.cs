// SECURITY FIX — AdminController hardened:
//   #A  Self-escalation: admin no puede editar su propia cuenta (EditarUsuario, GET y POST)
//   #B  Self-lockout: admin no puede desactivarse a sí mismo (DesactivarUsuario)
//   #C  Self-demotion: admin no puede quitarse el rol Administrador (EditarUsuario POST)
//   #D  Sesión fantasma: UpdateSecurityStamp al desactivar → invalida cookie activa inmediatamente
//   #7  ILogger para audit trail en acciones destructivas

using GymYanten.Data;
using GymYanten.Models;
using GymYanten.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymYanten.Controllers
{
    [Authorize(Roles = Roles.Administrador)]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        // SECURITY FIX #7 — Logger para audit trail
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ILogger<AdminController> logger)        // SECURITY FIX #7
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        // ── GET: /Admin ──────────────────────────────────────
        public async Task<IActionResult> Index(bool mostrarInactivos = false)
        {
            var query = mostrarInactivos
                ? _userManager.Users
                : _userManager.Users.Where(u => u.Activo);

            var usuarios = query.ToList();
            var vm = new List<UsuarioAdminViewModel>();

            foreach (var u in usuarios)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vm.Add(new UsuarioAdminViewModel
                {
                    Id = u.Id,
                    NombreCompleto = u.NombreCompleto,
                    Email = u.Email ?? "",
                    Telefono = u.Telefono,
                    Rol = roles.FirstOrDefault() ?? "Sin rol",
                    FechaRegistro = u.FechaRegistro,
                    Activo = u.Activo
                });
            }

            ViewBag.MostrarInactivos = mostrarInactivos;
            ViewBag.TotalUsuarios = _userManager.Users.Count(u => u.Activo);
            ViewBag.TotalInactivos = _userManager.Users.Count(u => !u.Activo);
            ViewBag.TotalClientes = _userManager.Users.Count(u => u.Activo);
            ViewBag.TotalEntrenadores = vm.Count(x => x.Rol == Roles.Entrenador && x.Activo);
            ViewBag.TotalEjercicios = await _context.Ejercicios.CountAsync(e => e.Activo);
            ViewBag.TotalEjInactivos = await _context.Ejercicios.CountAsync(e => !e.Activo);
            ViewBag.TotalRutinas = await _context.Rutinas.CountAsync(r => r.Activa);
            ViewBag.TotalRutInactivas = await _context.Rutinas.CountAsync(r => !r.Activa);
            ViewBag.TotalProgreso = await _context.Progresos.CountAsync();

            return View(vm);
        }

        // ── POST: /Admin/DesactivarUsuario ───────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesactivarUsuario(string id)
        {
            // SECURITY FIX #B — Admin no puede desactivarse a sí mismo
            var adminActualId = _userManager.GetUserId(User);
            if (id == adminActualId)
            {
                _logger.LogWarning(
                    "[SECURITY] Admin intentó desactivarse a sí mismo — AdminId: {Id}", adminActualId);
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction(nameof(Index));
            }

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario != null)
            {
                usuario.Activo = false;
                await _userManager.UpdateAsync(usuario);

                // SECURITY FIX #D — Invalidar cookie/sesión activa del usuario desactivado
                // UpdateSecurityStamp hace que Identity rechace la cookie existente
                // en la próxima request del usuario, forzando logout inmediato.
                await _userManager.UpdateSecurityStampAsync(usuario);

                _logger.LogInformation(
                    "[AUDIT] Usuario desactivado — AdminId: {AdminId} | TargetId: {TargetId} | Email: {Email}",
                    adminActualId, id, usuario.Email);

                TempData["Exito"] = $"Usuario '{usuario.NombreCompleto}' desactivado. Sus registros de progreso se conservan.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ── POST: /Admin/ReactivarUsuario ────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivarUsuario(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario != null)
            {
                usuario.Activo = true;
                await _userManager.UpdateAsync(usuario);

                _logger.LogInformation(
                    "[AUDIT] Usuario reactivado — AdminId: {AdminId} | TargetId: {TargetId} | Email: {Email}",
                    _userManager.GetUserId(User), id, usuario.Email);

                TempData["Exito"] = $"Usuario '{usuario.NombreCompleto}' reactivado. Ya puede iniciar sesión.";
            }
            return RedirectToAction(nameof(Index), new { mostrarInactivos = true });
        }

        // ── POST: /Admin/ReactivarEjercicio ──────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivarEjercicio(int id)
        {
            var ejercicio = await _context.Ejercicios.FindAsync(id);
            if (ejercicio != null)
            {
                ejercicio.Activo = true;
                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Ejercicio '{ejercicio.Nombre}' reactivado.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ── POST: /Admin/ReactivarRutina ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivarRutina(int id)
        {
            var rutina = await _context.Rutinas.FindAsync(id);
            if (rutina != null)
            {
                rutina.Activa = true;
                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Rutina '{rutina.Nombre}' reactivada.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ── GET: /Admin/Inactivos ─────────────────────────────
        public async Task<IActionResult> Inactivos()
        {
            var usuariosInactivos = _userManager.Users.Where(u => !u.Activo).ToList();
            var vmUsuarios = new List<UsuarioAdminViewModel>();
            foreach (var u in usuariosInactivos)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vmUsuarios.Add(new UsuarioAdminViewModel
                {
                    Id = u.Id,
                    NombreCompleto = u.NombreCompleto,
                    Email = u.Email ?? "",
                    Telefono = u.Telefono,
                    Rol = roles.FirstOrDefault() ?? "Sin rol",
                    FechaRegistro = u.FechaRegistro,
                    Activo = false
                });
            }

            ViewBag.UsuariosInactivos = vmUsuarios;
            ViewBag.EjerciciosInactivos = await _context.Ejercicios
                                               .Where(e => !e.Activo)
                                               .ToListAsync();
            ViewBag.RutinasInactivas = await _context.Rutinas
                                               .Include(r => r.Entrenador)
                                               .Where(r => !r.Activa)
                                               .ToListAsync();
            return View();
        }

        // ── GET: /Admin/CrearUsuario ──────────────────────────
        public IActionResult CrearUsuario()
        {
            ViewBag.Roles = new SelectList(Roles.Todos);
            return View();
        }

        // ── POST: /Admin/CrearUsuario ─────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearUsuario(CrearUsuarioViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existente = await _userManager.FindByEmailAsync(model.Email);
                if (existente != null && !existente.Activo)
                {
                    existente.Activo = true;
                    existente.Nombre = model.Nombre;
                    existente.Apellido = model.Apellido;
                    existente.Telefono = model.Telefono;
                    await _userManager.UpdateAsync(existente);

                    var rolesActuales = await _userManager.GetRolesAsync(existente);
                    await _userManager.RemoveFromRolesAsync(existente, rolesActuales);
                    await _userManager.AddToRoleAsync(existente, model.Rol);

                    var token = await _userManager.GeneratePasswordResetTokenAsync(existente);
                    await _userManager.ResetPasswordAsync(existente, token, model.Password);

                    _logger.LogInformation(
                        "[AUDIT] Usuario reactivado vía CrearUsuario — AdminId: {AdminId} | Email: {Email}",
                        _userManager.GetUserId(User), model.Email);

                    TempData["Exito"] = $"El email ya existía desactivado. El usuario '{existente.NombreCompleto}' fue reactivado con los nuevos datos.";
                    return RedirectToAction(nameof(Index));
                }

                var usuario = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    Nombre = model.Nombre,
                    Apellido = model.Apellido,
                    Telefono = model.Telefono,
                    EmailConfirmed = true,
                    Activo = true
                };

                var result = await _userManager.CreateAsync(usuario, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(usuario, model.Rol);

                    _logger.LogInformation(
                        "[AUDIT] Usuario creado — AdminId: {AdminId} | Email: {Email} | Rol: {Rol}",
                        _userManager.GetUserId(User), model.Email, model.Rol);

                    TempData["Exito"] = $"Usuario '{usuario.NombreCompleto}' creado con rol {model.Rol}.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Roles = new SelectList(Roles.Todos);
            return View(model);
        }

        // ── GET: /Admin/EditarUsuario/id ──────────────────────
        public async Task<IActionResult> EditarUsuario(string? id)
        {
            if (id == null) return NotFound();

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            // SECURITY FIX #A — Admin no puede editar su propia cuenta
            // (evita auto-demotion accidental o escalada de privilegios manipulando el form)
            var adminActualId = _userManager.GetUserId(User);
            if (id == adminActualId)
            {
                TempData["Error"] = "No puedes editar tu propia cuenta desde el panel de administración.";
                return RedirectToAction(nameof(Index));
            }

            var roles = await _userManager.GetRolesAsync(usuario);
            var vm = new EditarUsuarioViewModel
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Email = usuario.Email ?? "",
                Telefono = usuario.Telefono,
                Rol = roles.FirstOrDefault() ?? Roles.Cliente,
                Activo = usuario.Activo
            };

            ViewBag.Roles = new SelectList(Roles.Todos, vm.Rol);
            return View(vm);
        }

        // ── POST: /Admin/EditarUsuario ────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarUsuario(EditarUsuarioViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(Roles.Todos, model.Rol);
                return View(model);
            }

            var adminActualId = _userManager.GetUserId(User);

            // SECURITY FIX #A — Double-check en POST: evita que manipulen el Id del form
            if (model.Id == adminActualId)
            {
                _logger.LogWarning(
                    "[SECURITY] Admin intentó editarse a sí mismo vía POST — AdminId: {Id}", adminActualId);
                TempData["Error"] = "No puedes editar tu propia cuenta desde el panel de administración.";
                return RedirectToAction(nameof(Index));
            }

            var usuario = await _userManager.FindByIdAsync(model.Id);
            if (usuario == null) return NotFound();

            // SECURITY FIX #C — Impedir quitar el rol Administrador al último admin
            // Si el target es Admin y el nuevo rol es diferente, verificar que quede al menos 1 admin
            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            bool erAdmin = rolesActuales.Contains(Roles.Administrador);
            bool bajaAdmin = erAdmin && model.Rol != Roles.Administrador;

            if (bajaAdmin)
            {
                var totalAdmins = (await _userManager.GetUsersInRoleAsync(Roles.Administrador)).Count;
                if (totalAdmins <= 1)
                {
                    _logger.LogWarning(
                        "[SECURITY] Intento de quitar el último Administrador — AdminId: {AdminId} | TargetId: {TargetId}",
                        adminActualId, model.Id);
                    TempData["Error"] = "No puedes cambiar el rol del único administrador del sistema.";
                    ViewBag.Roles = new SelectList(Roles.Todos, model.Rol);
                    return View(model);
                }
            }

            // Actualizar datos del usuario
            usuario.Nombre = model.Nombre;
            usuario.Apellido = model.Apellido;
            usuario.Telefono = model.Telefono;
            usuario.Activo = model.Activo;
            await _userManager.UpdateAsync(usuario);

            // Cambio de rol
            await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            await _userManager.AddToRoleAsync(usuario, model.Rol);

            // SECURITY FIX #D — Si se desactivó la cuenta, invalidar su sesión activa
            if (!model.Activo)
                await _userManager.UpdateSecurityStampAsync(usuario);

            _logger.LogInformation(
                "[AUDIT] Usuario editado — AdminId: {AdminId} | TargetId: {TargetId} | " +
                "RolAnterior: {RolAnterior} | RolNuevo: {RolNuevo} | Activo: {Activo}",
                adminActualId, model.Id,
                rolesActuales.FirstOrDefault() ?? "ninguno", model.Rol, model.Activo);

            TempData["Exito"] = $"Usuario '{usuario.NombreCompleto}' actualizado.";
            return RedirectToAction(nameof(Index));
        }

        // ── POST: /Admin/CambiarContrasena ────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(CambiarContrasenaViewModel model)
        {
            // SECURITY FIX — Ahora recibe un ViewModel con Data Annotations validadas
            // por ModelState antes de llegar a cualquier lógica de negocio.
            // Antes: (string id, string nuevaContrasena) → sin validación de longitud ni confirmación.
            if (!ModelState.IsValid)
            {
                // Recolectar los errores de validación para mostrarlos en TempData
                var errores = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();
                TempData["Error"] = errores ?? "Datos inválidos. Verifica la contraseña.";
                return RedirectToAction(nameof(Index));
            }

            var adminActualId = _userManager.GetUserId(User);

            // SECURITY FIX #A — Admin no puede cambiarse su propia contraseña desde el panel
            if (model.Id == adminActualId)
            {
                TempData["Error"] = "Para cambiar tu propia contraseña usa la sección de perfil.";
                return RedirectToAction(nameof(Index));
            }

            var usuario = await _userManager.FindByIdAsync(model.Id);
            if (usuario == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
            var result = await _userManager.ResetPasswordAsync(usuario, token, model.NuevaContrasena);

            if (result.Succeeded)
            {
                // SECURITY FIX #D — Invalidar sesiones activas del usuario
                await _userManager.UpdateSecurityStampAsync(usuario);

                _logger.LogInformation(
                    "[AUDIT] Contraseña cambiada por admin — AdminId: {AdminId} | TargetId: {TargetId} | Email: {Email}",
                    adminActualId, model.Id, usuario.Email);

                TempData["Exito"] = $"Contraseña de '{usuario.NombreCompleto}' actualizada.";
            }
            else
            {
                // Mostrar el error específico de Identity (ej: "Passwords must have at least one uppercase")
                var errorIdentity = string.Join(" ", result.Errors.Select(e => e.Description));
                _logger.LogWarning(
                    "[SECURITY] Cambio de contraseña fallido — AdminId: {AdminId} | TargetId: {TargetId} | Errores: {Errores}",
                    adminActualId, model.Id, errorIdentity);
                TempData["Error"] = $"No se pudo cambiar la contraseña: {errorIdentity}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}