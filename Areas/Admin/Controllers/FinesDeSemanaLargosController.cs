using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class FinesDeSemanaLargosController : Controller
    {
        private readonly ApplicationDbContext _db;

        public FinesDeSemanaLargosController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            await CargarListadoAsync();
            var hoy = DateTime.Today;
            return View(new FinDeSemanaLargo { FechaDesde = hoy, FechaHasta = hoy.AddDays(3), ExigeReservaCompleta = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(FinDeSemanaLargo modelo)
        {
            var desde = modelo.FechaDesde.Date;
            var hasta = modelo.FechaHasta.Date;

            if (hasta <= desde.AddDays(1))
            {
                ModelState.AddModelError(nameof(FinDeSemanaLargo.FechaHasta), "La salida tiene que ser al menos 2 días después de la entrada.");
            }
            else if (hasta <= DateTime.Today)
            {
                ModelState.AddModelError(nameof(FinDeSemanaLargo.FechaHasta), "Ese fin de semana largo ya pasó.");
            }
            else
            {
                var superpuesto = await _db.FinesDeSemanaLargos
                    .Where(f => f.FechaDesde < hasta && f.FechaHasta > desde)
                    .FirstOrDefaultAsync();
                if (superpuesto is not null)
                {
                    ModelState.AddModelError(string.Empty,
                        $"Se superpone con otro fin de semana largo ya cargado ({superpuesto.FechaDesde:dddd d/M} al {superpuesto.FechaHasta:dddd d/M}).");
                }
            }

            if (!ModelState.IsValid)
            {
                await CargarListadoAsync();
                return View(nameof(Index), modelo);
            }

            _db.FinesDeSemanaLargos.Add(new FinDeSemanaLargo
            {
                Nombre = string.IsNullOrWhiteSpace(modelo.Nombre) ? null : modelo.Nombre.Trim(),
                FechaDesde = desde,
                FechaHasta = hasta,
                ExigeReservaCompleta = modelo.ExigeReservaCompleta
            });
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = modelo.ExigeReservaCompleta
                ? "Fin de semana largo cargado: desde el sitio sólo se puede reservar completo."
                : "Fin de semana largo cargado, sin restricción.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Alternar(int id)
        {
            var fin = await _db.FinesDeSemanaLargos.FindAsync(id);
            if (fin is not null)
            {
                fin.ExigeReservaCompleta = !fin.ExigeReservaCompleta;
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = fin.ExigeReservaCompleta
                    ? "Ahora ese fin de semana largo sólo se puede reservar completo."
                    : "Se quitó la restricción de ese fin de semana largo.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var fin = await _db.FinesDeSemanaLargos.FindAsync(id);
            if (fin is not null)
            {
                _db.FinesDeSemanaLargos.Remove(fin);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = "Fin de semana largo eliminado.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task CargarListadoAsync()
        {
            var hoy = DateTime.Today;
            ViewBag.Proximos = await _db.FinesDeSemanaLargos
                .Where(f => f.FechaHasta > hoy)
                .OrderBy(f => f.FechaDesde)
                .ToListAsync();
            ViewBag.Pasados = await _db.FinesDeSemanaLargos
                .Where(f => f.FechaHasta <= hoy)
                .OrderByDescending(f => f.FechaDesde)
                .ToListAsync();
        }
    }
}
