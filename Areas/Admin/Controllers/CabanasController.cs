using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class CabanasController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public CabanasController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var cabanas = await _db.Cabanas.Include(c => c.Fotos).OrderBy(c => c.Nombre).ToListAsync();
            return View(cabanas);
        }

        public IActionResult Create()
        {
            return View(new Cabana());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cabana modelo, List<IFormFile> fotos)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            _db.Cabanas.Add(modelo);
            await _db.SaveChangesAsync();

            await GuardarFotosAsync(modelo.Id, fotos);

            TempData["Mensaje"] = $"Cabaña \"{modelo.Nombre}\" creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cabana = await _db.Cabanas.Include(c => c.Fotos).FirstOrDefaultAsync(c => c.Id == id);
            if (cabana is null)
            {
                return NotFound();
            }
            return View(cabana);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cabana modelo, List<IFormFile> fotos)
        {
            if (id != modelo.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                modelo.Fotos = await _db.Fotos.Where(f => f.CabanaId == id).OrderBy(f => f.Orden).ToListAsync();
                return View(modelo);
            }

            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == id);
            if (cabana is null)
            {
                return NotFound();
            }

            cabana.Nombre = modelo.Nombre;
            cabana.Descripcion = modelo.Descripcion;
            cabana.Capacidad = modelo.Capacidad;
            cabana.PrecioPorNoche = modelo.PrecioPorNoche;
            cabana.Activa = modelo.Activa;

            await _db.SaveChangesAsync();
            await GuardarFotosAsync(cabana.Id, fotos);

            TempData["Mensaje"] = $"Cabaña \"{cabana.Nombre}\" actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFoto(int id, int cabanaId)
        {
            var foto = await _db.Fotos.FindAsync(id);
            if (foto is not null)
            {
                var rutaFisica = Path.Combine(_env.WebRootPath, foto.RutaArchivo.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(rutaFisica))
                {
                    System.IO.File.Delete(rutaFisica);
                }
                _db.Fotos.Remove(foto);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = cabanaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cabana = await _db.Cabanas.Include(c => c.Fotos).FirstOrDefaultAsync(c => c.Id == id);
            if (cabana is null)
            {
                return NotFound();
            }

            var tieneReservas = await _db.Reservas.AnyAsync(r => r.CabanaId == id);
            if (tieneReservas)
            {
                TempData["Mensaje"] = $"No se puede eliminar \"{cabana.Nombre}\" porque tiene reservas asociadas. Marcala como inactiva en su lugar.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var foto in cabana.Fotos)
            {
                var rutaFisica = Path.Combine(_env.WebRootPath, foto.RutaArchivo.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(rutaFisica))
                {
                    System.IO.File.Delete(rutaFisica);
                }
            }

            _db.Cabanas.Remove(cabana);
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = $"Cabaña \"{cabana.Nombre}\" eliminada.";
            return RedirectToAction(nameof(Index));
        }

        private async Task GuardarFotosAsync(int cabanaId, List<IFormFile>? fotos)
        {
            if (fotos is null || fotos.Count == 0)
            {
                return;
            }

            var carpeta = Path.Combine(_env.WebRootPath, "uploads", "cabanas", cabanaId.ToString());
            Directory.CreateDirectory(carpeta);

            var ordenActual = await _db.Fotos.Where(f => f.CabanaId == cabanaId).CountAsync();

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

                var nombreArchivo = $"{Guid.NewGuid()}{extension}";
                var rutaFisica = Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(rutaFisica, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                _db.Fotos.Add(new FotoCabana
                {
                    CabanaId = cabanaId,
                    RutaArchivo = $"uploads/cabanas/{cabanaId}/{nombreArchivo}",
                    Orden = ordenActual++
                });
            }

            await _db.SaveChangesAsync();
        }
    }
}
