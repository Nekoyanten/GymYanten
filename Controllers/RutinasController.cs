// SECURITY FIX #5 — Editar: validar EntrenadorId coincide con usuario actual (anti mass assignment)
// SECURITY FIX #7 — ILogger para audit trail

using GymYanten.Data;
using GymYanten.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymYanten.Controllers
{
    [Authorize]
    public class RutinasController : Controller
    {
        private readonly ApplicationDbContext          _context;
        private readonly UserManager<ApplicationUser>  _userManager;
        // SECURITY FIX #7 — Logger para audit trail
        private readonly ILogger<RutinasController>   _logger;

        public RutinasController(
            ApplicationDbContext         context,
            UserManager<ApplicationUser> userManager,
            ILogger<RutinasController>   logger)       // SECURITY FIX #7
        {
            _context     = context;
            _userManager = userManager;
            _logger      = logger;
        }

        // GET: /Rutinas
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Challenge();

            IQueryable<Rutina> query;

            if (User.IsInRole(Roles.Administrador))
                query = _context.Rutinas.Include(r => r.Entrenador).Where(r => r.Activa);
            else if (User.IsInRole(Roles.Entrenador))
                query = _context.Rutinas.Include(r => r.Entrenador)
                                        .Where(r => r.Activa && r.EntrenadorId == usuario.Id);
            else
            {
                var rutinasIds = await _context.Progresos
                                               .Where(p => p.ClienteId == usuario.Id)
                                               .Select(p => p.RutinaId)
                                               .Distinct()
                                               .ToListAsync();
                query = _context.Rutinas.Include(r => r.Entrenador)
                                        .Where(r => r.Activa && rutinasIds.Contains(r.Id));
            }

            return View(await query.OrderByDescending(r => r.FechaCreacion).ToListAsync());
        }

        // GET: /Rutinas/Detalles/5
        public async Task<IActionResult> Detalles(int? id)
        {
            if (id == null) return NotFound();

            var rutina = await _context.Rutinas
                                       .Include(r => r.Entrenador)
                                       .FirstOrDefaultAsync(r => r.Id == id);

            if (rutina == null) return NotFound();
            return View(rutina);
        }

        // GET: /Rutinas/Crear
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public IActionResult Crear() => View();

        // POST: /Rutinas/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> Crear(
            [Bind("Nombre,Descripcion,Nivel,DuracionEstimadaMinutos")] Rutina rutina)
        {
            ModelState.Remove("EntrenadorId");
            ModelState.Remove("Entrenador");

            if (ModelState.IsValid)
            {
                var usuario = await _userManager.GetUserAsync(User);
                // EntrenadorId siempre del usuario autenticado, nunca del form
                rutina.EntrenadorId  = usuario!.Id;
                rutina.FechaCreacion = DateTime.UtcNow;

                _context.Add(rutina);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "[AUDIT] Rutina creada — EntrenadorId: {UserId} | RutinaId: {RutinaId} | Nombre: {Nombre}",
                    usuario.Id, rutina.Id, rutina.Nombre);

                TempData["Exito"] = $"Rutina '{rutina.Nombre}' creada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(rutina);
        }

        // GET: /Rutinas/Editar/5
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null) return NotFound();

            var rutina = await _context.Rutinas.FindAsync(id);
            if (rutina == null) return NotFound();

            if (User.IsInRole(Roles.Entrenador))
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (rutina.EntrenadorId != usuario!.Id) return Forbid();
            }

            return View(rutina);
        }

        // POST: /Rutinas/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> Editar(int id,
            [Bind("Id,Nombre,Descripcion,Nivel,DuracionEstimadaMinutos,EntrenadorId,FechaCreacion,Activa")] Rutina rutina)
        {
            if (id != rutina.Id) return NotFound();

            ModelState.Remove("Entrenador");

            // SECURITY FIX #5 — Recuperar registro original para validar EntrenadorId
            var rutinaOriginal = await _context.Rutinas.AsNoTracking()
                                               .FirstOrDefaultAsync(r => r.Id == id);
            if (rutinaOriginal == null) return NotFound();

            // SECURITY FIX #5 — Entrenador no puede reasignar la rutina a otro entrenador
            if (User.IsInRole(Roles.Entrenador))
            {
                var usuario = await _userManager.GetUserAsync(User);

                // Verificar que el usuario actual es el propietario original
                if (rutinaOriginal.EntrenadorId != usuario!.Id)
                {
                    _logger.LogWarning(
                        "[SECURITY] Intento de edición no autorizada de Rutina — " +
                        "UsuarioId: {UserId} | RutinaId: {RutinaId} | EntrenadorId original: {Original} | IP: {IP}",
                        usuario.Id, id, rutinaOriginal.EntrenadorId,
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                    return Forbid();
                }

                // SECURITY FIX #5 — Si el form envió un EntrenadorId diferente, detectarlo y loguearlo
                if (rutina.EntrenadorId != rutinaOriginal.EntrenadorId)
                {
                    _logger.LogWarning(
                        "[SECURITY] Intento de mass assignment en Rutina.Editar — " +
                        "RutinaId: {Id} | EntrenadorId original: {Original} | EntrenadorId enviado: {Sent} | IP: {IP}",
                        id, rutinaOriginal.EntrenadorId, rutina.EntrenadorId,
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                }
            }

            // SECURITY FIX #5 — Forzar EntrenadorId original en todos los casos;
            // solo Admin podría cambiarlo y ese caso no está implementado aquí.
            rutina.EntrenadorId = rutinaOriginal.EntrenadorId;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(rutina);
                    await _context.SaveChangesAsync();

                    // SECURITY FIX #7 — Audit trail de edición
                    _logger.LogInformation(
                        "[AUDIT] Rutina editada — EntrenadorId: {UserId} | RutinaId: {RutinaId} | Nombre: {Nombre}",
                        rutina.EntrenadorId, rutina.Id, rutina.Nombre);

                    TempData["Exito"] = $"Rutina '{rutina.Nombre}' actualizada.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Rutinas.AnyAsync(r => r.Id == rutina.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(rutina);
        }

        // GET: /Rutinas/Eliminar/5
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> Eliminar(int? id)
        {
            if (id == null) return NotFound();

            var rutina = await _context.Rutinas
                                       .Include(r => r.Entrenador)
                                       .FirstOrDefaultAsync(r => r.Id == id);

            if (rutina == null) return NotFound();
            return View(rutina);
        }

        // POST: /Rutinas/Eliminar/5
        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var rutina = await _context.Rutinas.FindAsync(id);
            if (rutina != null)
            {
                rutina.Activa = false; // Soft delete

                // SECURITY FIX #7 — Audit trail de eliminación lógica
                var usuario = await _userManager.GetUserAsync(User);
                _logger.LogInformation(
                    "[AUDIT] Rutina desactivada (soft delete) — UsuarioId: {UserId} | RutinaId: {RutinaId} | Nombre: {Nombre}",
                    usuario?.Id ?? "unknown", id, rutina.Nombre);

                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Rutina '{rutina.Nombre}' eliminada.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
