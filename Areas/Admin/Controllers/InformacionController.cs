using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class InformacionController : Controller
    {
        private readonly ApplicationDbContext _db;

        public InformacionController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var info = await _db.InformacionSitio.FirstOrDefaultAsync();
            if (info is null)
            {
                info = new InformacionSitio();
                _db.InformacionSitio.Add(info);
                await _db.SaveChangesAsync();
            }

            ViewBag.Preguntas = await _db.PreguntasFrecuentes.OrderBy(p => p.Orden).ToListAsync();
            return View(info);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(InformacionSitio modelo)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Preguntas = await _db.PreguntasFrecuentes.OrderBy(p => p.Orden).ToListAsync();
                return View(nameof(Index), modelo);
            }

            var info = await _db.InformacionSitio.FirstOrDefaultAsync();
            if (info is null)
            {
                info = new InformacionSitio();
                _db.InformacionSitio.Add(info);
            }

            info.Direccion = modelo.Direccion;
            info.ComoLlegar = modelo.ComoLlegar;
            info.Comodidades = modelo.Comodidades;
            info.InformacionAdicional = modelo.InformacionAdicional;

            await _db.SaveChangesAsync();
            TempData["Mensaje"] = "Información actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult CrearPregunta()
        {
            return View(new PreguntaFrecuente());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPregunta(PreguntaFrecuente modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            if (modelo.Orden == 0)
            {
                var maxOrden = await _db.PreguntasFrecuentes.Select(p => (int?)p.Orden).MaxAsync() ?? 0;
                modelo.Orden = maxOrden + 1;
            }

            _db.PreguntasFrecuentes.Add(modelo);
            await _db.SaveChangesAsync();
            TempData["Mensaje"] = "Pregunta agregada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditarPregunta(int id)
        {
            var pregunta = await _db.PreguntasFrecuentes.FindAsync(id);
            if (pregunta is null)
            {
                return NotFound();
            }
            return View(pregunta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPregunta(int id, PreguntaFrecuente modelo)
        {
            if (id != modelo.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            var pregunta = await _db.PreguntasFrecuentes.FindAsync(id);
            if (pregunta is null)
            {
                return NotFound();
            }

            pregunta.Pregunta = modelo.Pregunta;
            pregunta.Respuesta = modelo.Respuesta;
            pregunta.Orden = modelo.Orden;

            await _db.SaveChangesAsync();
            TempData["Mensaje"] = "Pregunta actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPregunta(int id)
        {
            var pregunta = await _db.PreguntasFrecuentes.FindAsync(id);
            if (pregunta is not null)
            {
                _db.PreguntasFrecuentes.Remove(pregunta);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = "Pregunta eliminada.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
