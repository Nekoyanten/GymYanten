using GymYanten.Data;
using GymYanten.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymYanten.Controllers
{
    /// <summary>
    /// Gestión de Rutinas.
    /// Entrenadores crean y diseñan rutinas.
    /// Clientes solo pueden ver las que les fueron asignadas.
    /// </summary>
    [Authorize]
    public class RutinasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RutinasController(ApplicationDbContext context,
                                 UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        //  GET: /Rutinas
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Challenge();

            IQueryable<Rutina> query;

            if (User.IsInRole(Roles.Administrador))
            {
                // Admin ve todas las rutinas
                query = _context.Rutinas
                                .Include(r => r.Entrenador)
                                .Where(r => r.Activa);
            }
            else if (User.IsInRole(Roles.Entrenador))
            {
                // Entrenador ve solo las suyas
                query = _context.Rutinas
                                .Include(r => r.Entrenador)
                                .Where(r => r.Activa && r.EntrenadorId == usuario.Id);
            }
            else
            {
                // Cliente ve las rutinas que tienen su progreso registrado
                var rutinasIds = await _context.Progresos
                                               .Where(p => p.ClienteId == usuario.Id)
                                               .Select(p => p.RutinaId)
                                               .Distinct()
                                               .ToListAsync();

                query = _context.Rutinas
                                .Include(r => r.Entrenador)
                                .Where(r => r.Activa && rutinasIds.Contains(r.Id));
            }

            return View(await query.OrderByDescending(r => r.FechaCreacion).ToListAsync());
        }


        //  GET: /Rutinas/Detalles/5
        public async Task<IActionResult> Detalles(int? id)
        {
            if (id == null) return NotFound();

            var rutina = await _context.Rutinas
                                       .Include(r => r.Entrenador)
                                       .FirstOrDefaultAsync(r => r.Id == id);

            if (rutina == null) return NotFound();

            return View(rutina);
        }

        //  GET: /Rutinas/Crear
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public IActionResult Crear()
        {
            return View();
        }

        //  POST: /Rutinas/Crear
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
                rutina.EntrenadorId = usuario!.Id;
                rutina.FechaCreacion = DateTime.UtcNow;

                _context.Add(rutina);
                await _context.SaveChangesAsync();

                TempData["Exito"] = $"Rutina '{rutina.Nombre}' creada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(rutina);
        }

        //  GET: /Rutinas/Editar/5
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null) return NotFound();

            var rutina = await _context.Rutinas.FindAsync(id);
            if (rutina == null) return NotFound();

            // Entrenador solo puede editar sus propias rutinas
            if (User.IsInRole(Roles.Entrenador))
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (rutina.EntrenadorId != usuario!.Id)
                    return Forbid();
            }

            return View(rutina);
        }

        //  POST: /Rutinas/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> Editar(int id,
            [Bind("Id,Nombre,Descripcion,Nivel,DuracionEstimadaMinutos,EntrenadorId,FechaCreacion,Activa")] Rutina rutina)
        {
            if (id != rutina.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(rutina);
                    await _context.SaveChangesAsync();
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

        //  GET: /Rutinas/Eliminar/5
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

        //  POST: /Rutinas/Eliminar/5
        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var rutina = await _context.Rutinas.FindAsync(id);
            if (rutina != null)
            {
                rutina.Activa = false; // Soft delete
                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Rutina '{rutina.Nombre}' eliminada.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}