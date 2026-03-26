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

        public AdminController(UserManager<ApplicationUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context     = context;
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
                    Id             = u.Id,
                    NombreCompleto = u.NombreCompleto,
                    Email          = u.Email ?? "",
                    Telefono       = u.Telefono,
                    Rol            = roles.FirstOrDefault() ?? "Sin rol",
                    FechaRegistro  = u.FechaRegistro,
                    Activo         = u.Activo
                });
            }

            ViewBag.MostrarInactivos  = mostrarInactivos;
            ViewBag.TotalUsuarios     = _userManager.Users.Count(u => u.Activo);
            ViewBag.TotalInactivos    = _userManager.Users.Count(u => !u.Activo);
            ViewBag.TotalClientes     = _userManager.Users.Count(u => u.Activo);
            ViewBag.TotalEntrenadores = vm.Count(x => x.Rol == Roles.Entrenador && x.Activo);
            ViewBag.TotalEjercicios   = await _context.Ejercicios.CountAsync(e => e.Activo);
            ViewBag.TotalEjInactivos  = await _context.Ejercicios.CountAsync(e => !e.Activo);
            ViewBag.TotalRutinas      = await _context.Rutinas.CountAsync(r => r.Activa);
            ViewBag.TotalRutInactivas = await _context.Rutinas.CountAsync(r => !r.Activa);
            ViewBag.TotalProgreso     = await _context.Progresos.CountAsync();

            return View(vm);
        }

        // ── POST: /Admin/DesactivarUsuario ───────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesactivarUsuario(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario != null)
            {
                usuario.Activo = false;
                await _userManager.UpdateAsync(usuario);
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
        // Vista dedicada para gestionar todos los registros inactivos
        public async Task<IActionResult> Inactivos()
        {
            var usuariosInactivos = _userManager.Users.Where(u => !u.Activo).ToList();
            var vmUsuarios = new List<UsuarioAdminViewModel>();
            foreach (var u in usuariosInactivos)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vmUsuarios.Add(new UsuarioAdminViewModel
                {
                    Id             = u.Id,
                    NombreCompleto = u.NombreCompleto,
                    Email          = u.Email ?? "",
                    Telefono       = u.Telefono,
                    Rol            = roles.FirstOrDefault() ?? "Sin rol",
                    FechaRegistro  = u.FechaRegistro,
                    Activo         = false
                });
            }

            ViewBag.UsuariosInactivos   = vmUsuarios;
            ViewBag.EjerciciosInactivos = await _context.Ejercicios
                                               .Where(e => !e.Activo)
                                               .ToListAsync();
            ViewBag.RutinasInactivas    = await _context.Rutinas
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
                // Si el email existe pero está inactivo → reactivar en lugar de error
                var existente = await _userManager.FindByEmailAsync(model.Email);
                if (existente != null && !existente.Activo)
                {
                    existente.Activo   = true;
                    existente.Nombre   = model.Nombre;
                    existente.Apellido = model.Apellido;
                    existente.Telefono = model.Telefono;
                    await _userManager.UpdateAsync(existente);

                    var rolesActuales = await _userManager.GetRolesAsync(existente);
                    await _userManager.RemoveFromRolesAsync(existente, rolesActuales);
                    await _userManager.AddToRoleAsync(existente, model.Rol);

                    var token = await _userManager.GeneratePasswordResetTokenAsync(existente);
                    await _userManager.ResetPasswordAsync(existente, token, model.Password);

                    TempData["Exito"] = $"El email ya existía desactivado. El usuario '{existente.NombreCompleto}' fue reactivado con los nuevos datos.";
                    return RedirectToAction(nameof(Index));
                }

                var usuario = new ApplicationUser
                {
                    UserName       = model.Email,
                    Email          = model.Email,
                    Nombre         = model.Nombre,
                    Apellido       = model.Apellido,
                    Telefono       = model.Telefono,
                    EmailConfirmed = true,
                    Activo         = true
                };

                var result = await _userManager.CreateAsync(usuario, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(usuario, model.Rol);
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

            var roles = await _userManager.GetRolesAsync(usuario);
            var vm = new EditarUsuarioViewModel
            {
                Id       = usuario.Id,
                Nombre   = usuario.Nombre,
                Apellido = usuario.Apellido,
                Email    = usuario.Email ?? "",
                Telefono = usuario.Telefono,
                Rol      = roles.FirstOrDefault() ?? Roles.Cliente,
                Activo   = usuario.Activo
            };

            ViewBag.Roles = new SelectList(Roles.Todos, vm.Rol);
            return View(vm);
        }

        // ── POST: /Admin/EditarUsuario ────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarUsuario(EditarUsuarioViewModel model)
        {
            if (ModelState.IsValid)
            {
                var usuario = await _userManager.FindByIdAsync(model.Id);
                if (usuario == null) return NotFound();

                usuario.Nombre   = model.Nombre;
                usuario.Apellido = model.Apellido;
                usuario.Telefono = model.Telefono;
                usuario.Activo   = model.Activo;
                await _userManager.UpdateAsync(usuario);

                var rolesActuales = await _userManager.GetRolesAsync(usuario);
                await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
                await _userManager.AddToRoleAsync(usuario, model.Rol);

                TempData["Exito"] = $"Usuario '{usuario.NombreCompleto}' actualizado.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Roles = new SelectList(Roles.Todos, model.Rol);
            return View(model);
        }

        // ── POST: /Admin/CambiarContrasena ────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(string id, string nuevaContrasena)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario != null && !string.IsNullOrWhiteSpace(nuevaContrasena))
            {
                var token  = await _userManager.GeneratePasswordResetTokenAsync(usuario);
                var result = await _userManager.ResetPasswordAsync(usuario, token, nuevaContrasena);
                TempData[result.Succeeded ? "Exito" : "Error"] = result.Succeeded
                    ? $"Contraseña de '{usuario.NombreCompleto}' actualizada."
                    : "No se pudo cambiar la contraseña. Debe tener mayúscula, número y símbolo.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
