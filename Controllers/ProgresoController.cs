
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
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProgresoController(ApplicationDbContext context,
                                  UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        //  GET: /Progreso
        //  Historial del cliente autenticado
        public async Task<IActionResult> Index(int? rutinaId, int? ejercicioId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Challenge();

            // Admin y Entrenador ven todo; Cliente ve solo lo suyo
            var query = _context.Progresos
                                .Include(p => p.Cliente)
                                .Include(p => p.Rutina)
                                .Include(p => p.Ejercicio)
                                .AsQueryable();

            if (User.IsInRole(Roles.Cliente))
                query = query.Where(p => p.ClienteId == usuario.Id);

            if (rutinaId.HasValue)
                query = query.Where(p => p.RutinaId == rutinaId.Value);

            if (ejercicioId.HasValue)
                query = query.Where(p => p.EjercicioId == ejercicioId.Value);

            // Filtros para la View
            ViewBag.Rutinas = new SelectList(await _context.Rutinas.Where(r => r.Activa).ToListAsync(), "Id", "Nombre", rutinaId);
            ViewBag.Ejercicios = new SelectList(await _context.Ejercicios.Where(e => e.Activo).ToListAsync(), "Id", "Nombre", ejercicioId);

            return View(await query.OrderByDescending(p => p.Fecha).ToListAsync());
        }

        //  GET: /Progreso/Registrar
        public async Task<IActionResult> Registrar()
        {
            await CargarSelectLists();
            return View();
        }

        //  POST: /Progreso/Registrar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(
            [Bind("RutinaId,EjercicioId,Fecha,SeriesRealizadas,Repeticiones,PesoUsadoKg,Notas")] ProgresoEntrenamiento progreso)
        {
            if (ModelState.IsValid)
            {
                var usuario = await _userManager.GetUserAsync(User);
                progreso.ClienteId = usuario!.Id;
                progreso.FechaRegistro = DateTime.UtcNow;

                _context.Add(progreso);
                await _context.SaveChangesAsync();

                TempData["Exito"] = "Progreso registrado correctamente. ¡Sigue así! 💪";
                return RedirectToAction(nameof(Index));
            }

            await CargarSelectLists();
            return View(progreso);
        }

        //  GET: /Progreso/Detalles/5
        public async Task<IActionResult> Detalles(int? id)
        {
            if (id == null) return NotFound();

            var progreso = await _context.Progresos
                                         .Include(p => p.Cliente)
                                         .Include(p => p.Rutina)
                                         .Include(p => p.Ejercicio)
                                         .FirstOrDefaultAsync(p => p.Id == id);

            if (progreso == null) return NotFound();

            // Cliente solo puede ver su propio progreso
            if (User.IsInRole(Roles.Cliente))
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (progreso.ClienteId != usuario!.Id)
                    return Forbid();
            }

            return View(progreso);
        }

        //  GET: /Progreso/Editar/5
        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null) return NotFound();

            var progreso = await _context.Progresos.FindAsync(id);
            if (progreso == null) return NotFound();

            // Verificar que el cliente solo edita lo suyo
            if (User.IsInRole(Roles.Cliente))
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (progreso.ClienteId != usuario!.Id) return Forbid();
            }

            await CargarSelectLists(progreso.RutinaId, progreso.EjercicioId);
            return View(progreso);
        }

        //  POST: /Progreso/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id,
            [Bind("Id,ClienteId,RutinaId,EjercicioId,Fecha,SeriesRealizadas,Repeticiones,PesoUsadoKg,Notas,FechaRegistro")] ProgresoEntrenamiento progreso)
        {
            if (id != progreso.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(progreso);
                    await _context.SaveChangesAsync();
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

        //  GET: /Progreso/Eliminar/5
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

        //  POST: /Progreso/Eliminar/5
        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var progreso = await _context.Progresos.FindAsync(id);
            if (progreso != null)
            {
                _context.Progresos.Remove(progreso);
                await _context.SaveChangesAsync();
                TempData["Exito"] = "Registro de progreso eliminado.";
            }
            return RedirectToAction(nameof(Index));
        }

        //  Método privado: carga los SelectList
        private async Task CargarSelectLists(int? rutinaId = null, int? ejercicioId = null)
        {
            ViewBag.Rutinas = new SelectList(
                await _context.Rutinas.Where(r => r.Activa).OrderBy(r => r.Nombre).ToListAsync(),
                "Id", "Nombre", rutinaId);

            ViewBag.Ejercicios = new SelectList(
                await _context.Ejercicios.Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync(),
                "Id", "Nombre", ejercicioId);
        }
    }
}