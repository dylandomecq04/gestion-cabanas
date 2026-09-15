using GestionCabanas.Data;
using GestionCabanas.Models;
using GestionCabanas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class InicioController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AlmacenamientoFotosService _almacenamiento;

        public InicioController(ApplicationDbContext db, AlmacenamientoFotosService almacenamiento)
        {
            _db = db;
            _almacenamiento = almacenamiento;
        }

        public async Task<IActionResult> Index()
        {
            var inicio = await _db.InicioSitio.FirstOrDefaultAsync();
            if (inicio is null)
            {
                inicio = new InicioSitio();
                _db.InicioSitio.Add(inicio);
                await _db.SaveChangesAsync();
            }

            ViewBag.Fotos = await _db.FotosHero.OrderBy(f => f.Orden).ToListAsync();
            return View(inicio);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(InicioSitio modelo, List<IFormFile> fotos)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Fotos = await _db.FotosHero.OrderBy(f => f.Orden).ToListAsync();
                return View(nameof(Index), modelo);
            }

            var inicio = await _db.InicioSitio.FirstOrDefaultAsync();
            if (inicio is null)
            {
                inicio = new InicioSitio();
                _db.InicioSitio.Add(inicio);
            }

            inicio.TextoEyebrow = modelo.TextoEyebrow;
            inicio.Titulo = modelo.Titulo;

            await _db.SaveChangesAsync();
            await GuardarFotosAsync(fotos);

            TempData["Mensaje"] = "Inicio actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFoto(int id)
        {
            var foto = await _db.FotosHero.FindAsync(id);
            if (foto is not null)
            {
                await _almacenamiento.EliminarAsync(foto.RutaArchivo);
                _db.FotosHero.Remove(foto);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFotos(List<int> ids)
        {
            if (ids is { Count: > 0 })
            {
                var fotos = await _db.FotosHero.Where(f => ids.Contains(f.Id)).ToListAsync();
                foreach (var foto in fotos)
                {
                    await _almacenamiento.EliminarAsync(foto.RutaArchivo);
                }
                _db.FotosHero.RemoveRange(fotos);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = fotos.Count == 1 ? "Se eliminó 1 foto." : $"Se eliminaron {fotos.Count} fotos.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task GuardarFotosAsync(List<IFormFile>? fotos)
        {
            if (fotos is null || fotos.Count == 0)
            {
                return;
            }

            var ordenActual = await _db.FotosHero.CountAsync();

            foreach (var archivo in fotos)
            {
                if (archivo.Length == 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(archivo.FileName);
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!extensionesPermitidas.Contains(extension.ToLowerInvariant()))
                {
                    continue;
                }

                var nombreBlob = $"hero/{Guid.NewGuid()}{extension}";

                using var stream = archivo.OpenReadStream();
                var url = await _almacenamiento.SubirAsync(stream, nombreBlob, archivo.ContentType);

                _db.FotosHero.Add(new FotoHero
                {
                    RutaArchivo = url,
                    Orden = ordenActual++
                });
            }

            await _db.SaveChangesAsync();
        }
    }
}
