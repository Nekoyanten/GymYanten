// SECURITY FIX #4 — Editar: verificar que ClienteId no cambie (anti mass assignment)
// SECURITY FIX #6 — Validar rango realista de PesoUsadoKg (max 300 kg)
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
    public class ProgresoController : Controller
    {
        private readonly ApplicationDbContext          _context;
        private readonly UserManager<ApplicationUser>  _userManager;
        // SECURITY FIX #7 — Logger para audit trail
        private readonly ILogger<ProgresoController>  _logger;

        // SECURITY FIX #6 — Constante de peso máximo realista
        private const decimal PesoMaximoKg = 300m;

        public ProgresoController(
            ApplicationDbContext         context,
            UserManager<ApplicationUser> userManager,
            ILogger<ProgresoController>  logger)       // SECURITY FIX #7
        {
            _context     = context;
            _userManager = userManager;
            _logger      = logger;
        }

        // GET: /Progreso
        public async Task<IActionResult> Index(int? rutinaId, int? ejercicioId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Challenge();

            var query = _context.Progresos
                                .Include(p => p.Cliente)
                                .Include(p => p.Rutina)
                                .Include(p => p.Ejercicio)
                                .AsQueryable();

            if (User.IsInRole(Roles.Cliente))
                query = query.Where(p => p.ClienteId == usuario.Id);

            if (rutinaId.HasValue)   query = query.Where(p => p.RutinaId   == rutinaId.Value);
            if (ejercicioId.HasValue) query = query.Where(p => p.EjercicioId == ejercicioId.Value);

            ViewBag.Rutinas    = new SelectList(await _context.Rutinas.Where(r => r.Activa).ToListAsync(), "Id", "Nombre", rutinaId);
            ViewBag.Ejercicios = new SelectList(await _context.Ejercicios.Where(e => e.Activo).ToListAsync(), "Id", "Nombre", ejercicioId);

            return View(await query.OrderByDescending(p => p.Fecha).ToListAsync());
        }

        // GET: /Progreso/Registrar
        public async Task<IActionResult> Registrar()
        {
            await CargarSelectLists();
            return View();
        }

        // POST: /Progreso/Registrar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(
            [Bind("RutinaId,EjercicioId,Fecha,SeriesRealizadas,Repeticiones,PesoUsadoKg,Notas")] ProgresoEntrenamiento progreso)
        {
            ModelState.Remove("ClienteId");
            ModelState.Remove("Cliente");

            // SECURITY FIX #6 — Validar peso máximo realista antes de ModelState
            if (progreso.PesoUsadoKg.HasValue && progreso.PesoUsadoKg.Value > PesoMaximoKg)
            {
                ModelState.AddModelError(nameof(progreso.PesoUsadoKg),
                    $"El peso ingresado ({progreso.PesoUsadoKg} kg) supera el máximo permitido ({PesoMaximoKg} kg). Verifica el valor.");
            }

            if (ModelState.IsValid)
            {
                var usuario = await _userManager.GetUserAsync(User);
                // SECURITY FIX #4 — ClienteId siempre viene del usuario autenticado, nunca del form
                progreso.ClienteId    = usuario!.Id;
                progreso.FechaRegistro = DateTime.UtcNow;

                _context.Add(progreso);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "[AUDIT] Progreso registrado — UsuarioId: {UserId} | ProgresoId: {ProgresoId}",
                    usuario.Id, progreso.Id);

                TempData["Exito"] = "Progreso registrado correctamente. ¡Sigue así! 💪";
                return RedirectToAction(nameof(Index));
            }

            await CargarSelectLists();
            return View(progreso);
        }

        // GET: /Progreso/Detalles/5
        public async Task<IActionResult> Detalles(int? id)
        {
            if (id == null) return NotFound();

            var progreso = await _context.Progresos
                                         .Include(p => p.Cliente)
                                         .Include(p => p.Rutina)
                                         .Include(p => p.Ejercicio)
                                         .FirstOrDefaultAsync(p => p.Id == id);

            if (progreso == null) return NotFound();

            if (User.IsInRole(Roles.Cliente))
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (progreso.ClienteId != usuario!.Id) return Forbid();
            }

            return View(progreso);
        }

        // GET: /Progreso/Editar/5
        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null) return NotFound();

            var progreso = await _context.Progresos.FindAsync(id);
            if (progreso == null) return NotFound();

            if (User.IsInRole(Roles.Cliente))
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (progreso.ClienteId != usuario!.Id) return Forbid();
            }

            await CargarSelectLists(progreso.RutinaId, progreso.EjercicioId);
            return View(progreso);
        }

        // POST: /Progreso/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id,
            [Bind("Id,ClienteId,RutinaId,EjercicioId,Fecha,SeriesRealizadas,Repeticiones,PesoUsadoKg,Notas,FechaRegistro")] ProgresoEntrenamiento progreso)
        {
            if (id != progreso.Id) return NotFound();

            // SECURITY FIX #4 — Recuperar el registro original de la BD para validar ClienteId
            // Un atacante podría enviar un ClienteId diferente en el form para reasignar el progreso.
            var progresoOriginal = await _context.Progresos.AsNoTracking()
                                                 .FirstOrDefaultAsync(p => p.Id == id);
            if (progresoOriginal == null) return NotFound();

            // SECURITY FIX #4 — Verificar que el ClienteId no fue modificado
            if (progreso.ClienteId != progresoOriginal.ClienteId)
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                _logger.LogWarning(
                    "[SECURITY] Intento de mass assignment en Progreso.Editar — " +
                    "ProgresoId: {Id} | ClienteId original: {Original} | ClienteId enviado: {Sent} | IP: {IP}",
                    id, progresoOriginal.ClienteId, progreso.ClienteId, ip);

                return Forbid(); // 403 — no revelar detalles al cliente
            }

            // SECURITY FIX #4 — Forzar el ClienteId original por si ModelState lo omitió
            progreso.ClienteId = progresoOriginal.ClienteId;

            // SECURITY FIX #6 — Validar peso máximo realista
            if (progreso.PesoUsadoKg.HasValue && progreso.PesoUsadoKg.Value > PesoMaximoKg)
            {
                ModelState.AddModelError(nameof(progreso.PesoUsadoKg),
                    $"El peso ingresado ({progreso.PesoUsadoKg} kg) supera el máximo permitido ({PesoMaximoKg} kg). Verifica el valor.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(progreso);
                    await _context.SaveChangesAsync();

                    // SECURITY FIX #7 — Audit trail de edición
                    _logger.LogInformation(
                        "[AUDIT] Progreso editado — UsuarioId: {UserId} | ProgresoId: {ProgresoId}",
                        progresoOriginal.ClienteId, progreso.Id);

                    TempData["Exito"] = "Progreso actualizado.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Progresos.AnyAsync(p => p.Id == progreso.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await CargarSelectLists(progreso.RutinaId, progreso.EjercicioId);
            return View(progreso);
        }

        // GET: /Progreso/Eliminar/5
        public async Task<IActionResult> Eliminar(int? id)
        {
            if (id == null) return NotFound();

            var progreso = await _context.Progresos
                                         .Include(p => p.Rutina)
                                         .Include(p => p.Ejercicio)
                                         .FirstOrDefaultAsync(p => p.Id == id);

            if (progreso == null) return NotFound();
            return View(progreso);
        }

        // POST: /Progreso/Eliminar/5
        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var progreso = await _context.Progresos.FindAsync(id);
            if (progreso != null)
            {
                _context.Progresos.Remove(progreso);
                await _context.SaveChangesAsync();

                // SECURITY FIX #7 — Audit trail de eliminación
                _logger.LogInformation(
                    "[AUDIT] Progreso eliminado — UsuarioId: {UserId} | ProgresoId: {ProgresoId}",
                    progreso.ClienteId, id);

                TempData["Exito"] = "Registro de progreso eliminado.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private async Task CargarSelectLists(int? rutinaId = null, int? ejercicioId = null)
        {
            ViewBag.Rutinas    = new SelectList(
                await _context.Rutinas.Where(r => r.Activa).OrderBy(r => r.Nombre).ToListAsync(),
                "Id", "Nombre", rutinaId);

            ViewBag.Ejercicios = new SelectList(
                await _context.Ejercicios.Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync(),
                "Id", "Nombre", ejercicioId);
        }
    }
}
