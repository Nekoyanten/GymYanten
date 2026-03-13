using GymYanten.Data;
using GymYanten.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymYanten.Controllers
{

    [Authorize(Roles = $"{Roles.Administrador},{Roles.Entrenador}")]
    public class EjerciciosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EjerciciosController(ApplicationDbContext context)
        {
            _context = context;
        }

        //  GET: /Ejercicios
        [AllowAnonymous] 
        public async Task<IActionResult> Index(string? buscar, GrupoMuscular? grupo)
        {
            var query = _context.Ejercicios
                                .Where(e => e.Activo)
                                .AsQueryable();

            // Filtro por nombre
            if (!string.IsNullOrWhiteSpace(buscar))
                query = query.Where(e => e.Nombre.Contains(buscar));

            // Filtro por grupo muscular
            if (grupo.HasValue)
                query = query.Where(e => e.GrupoMuscular == grupo.Value);

            // Pasamos los filtros a la View para mantener el estado
            ViewBag.Buscar = buscar;
            ViewBag.Grupo = grupo;
            ViewBag.Grupos = Enum.GetValues<GrupoMuscular>();

            var ejercicios = await query.OrderBy(e => e.Nombre).ToListAsync();
            return View(ejercicios);
        }

        //  GET: /Ejercicios/Detalles/5
        [AllowAnonymous]
        public async Task<IActionResult> Detalles(int? id)
        {
            if (id == null) return NotFound();

            var ejercicio = await _context.Ejercicios
                                          .FirstOrDefaultAsync(e => e.Id == id);

            if (ejercicio == null) return NotFound();

            return View(ejercicio);
        }

        //  GET: /Ejercicios/Crear
        public IActionResult Crear()
        {
            return View();
        }

        //  POST: /Ejercicios/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            [Bind("Nombre,Descripcion,GrupoMuscular,RequiereEquipo")] Ejercicio ejercicio)
        {
            if (ModelState.IsValid)
            {
                _context.Add(ejercicio);
                await _context.SaveChangesAsync();

                TempData["Exito"] = $"Ejercicio '{ejercicio.Nombre}' creado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(ejercicio);
        }

        //  GET: /Ejercicios/Editar/5
        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null) return NotFound();

            var ejercicio = await _context.Ejercicios.FindAsync(id);
            if (ejercicio == null) return NotFound();

            return View(ejercicio);
        }

        //  POST: /Ejercicios/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id,
            [Bind("Id,Nombre,Descripcion,GrupoMuscular,RequiereEquipo,Activo")] Ejercicio ejercicio)
        {
            if (id != ejercicio.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ejercicio);
                    await _context.SaveChangesAsync();
                    TempData["Exito"] = $"Ejercicio '{ejercicio.Nombre}' actualizado.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Ejercicios.AnyAsync(e => e.Id == ejercicio.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(ejercicio);
        }

        //  GET: /Ejercicios/Eliminar/5
        public async Task<IActionResult> Eliminar(int? id)
        {
            if (id == null) return NotFound();

            var ejercicio = await _context.Ejercicios
                                          .FirstOrDefaultAsync(e => e.Id == id);
            if (ejercicio == null) return NotFound();

            return View(ejercicio);
        }

        //  POST: /Ejercicios/Eliminar/5
        //  Borrado lógico (Activo = false), no físico
        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var ejercicio = await _context.Ejercicios.FindAsync(id);
            if (ejercicio != null)
            {
                ejercicio.Activo = false; // Soft delete
                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Ejercicio '{ejercicio.Nombre}' eliminado.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}