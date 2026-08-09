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
    public class CabanasController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly DisponibilidadService _disponibilidad;

        public CabanasController(ApplicationDbContext db, IWebHostEnvironment env, DisponibilidadService disponibilidad)
        {
            _db = db;
            _env = env;
            _disponibilidad = disponibilidad;
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

        public async Task<IActionResult> Tarifas(int id, int? anio, int? mes)
        {
            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == id);
            if (cabana is null)
            {
                return NotFound();
            }

            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var reservas = await _db.Reservas
                .Where(r => r.CabanaId == id && r.Estado == EstadoReserva.Confirmada && r.FechaDesde <= ultimoDia && r.FechaHasta >= primerDia)
                .ToListAsync();

            var tarifas = await _db.TarifasDias
                .Where(t => t.CabanaId == id && t.Fecha >= primerDia && t.Fecha <= ultimoDia)
                .ToListAsync();

            var dias = new List<DiaTarifaVista>();
            for (var fecha = primerDia; fecha <= ultimoDia; fecha = fecha.AddDays(1))
            {
                var tarifa = tarifas.FirstOrDefault(t => t.Fecha.Date == fecha.Date);
                dias.Add(new DiaTarifaVista
                {
                    Fecha = fecha,
                    Reservada = reservas.Any(r => r.FechaDesde <= fecha && fecha < r.FechaHasta),
                    Pasada = fecha < hoy,
                    Precio = tarifa?.Precio,
                    Bloqueada = tarifa?.Bloqueada ?? false
                });
            }

            ViewBag.Cabana = cabana;
            ViewBag.Dias = dias;
            ViewBag.PrimerDia = primerDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarTarifas(int id, int anio, int mes, List<TarifaDiaInput> dias)
        {
            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == id);
            if (cabana is null)
            {
                return NotFound();
            }

            var dias2 = dias ?? new List<TarifaDiaInput>();
            foreach (var dia in dias2)
            {
                dia.CabanaId = id;
            }
            await _disponibilidad.GuardarTarifasAsync(dias2);

            TempData["Mensaje"] = $"Precios y disponibilidad de \"{cabana.Nombre}\" actualizados.";
            return RedirectToAction(nameof(Tarifas), new { id, anio, mes });
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
