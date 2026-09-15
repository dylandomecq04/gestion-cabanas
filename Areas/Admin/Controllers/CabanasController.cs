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
        private readonly DisponibilidadService _disponibilidad;
        private readonly AlmacenamientoFotosService _almacenamiento;

        public CabanasController(ApplicationDbContext db, DisponibilidadService disponibilidad, AlmacenamientoFotosService almacenamiento)
        {
            _db = db;
            _disponibilidad = disponibilidad;
            _almacenamiento = almacenamiento;
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
            cabana.Capacidad = modelo.Capacidad;
            cabana.Activa = modelo.Activa;

            await _db.SaveChangesAsync();
            await GuardarFotosAsync(cabana.Id, fotos);

            TempData["Mensaje"] = $"Cabaña \"{cabana.Nombre}\" actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReordenarFotos(int cabanaId, [FromForm] List<int> idsFotos)
        {
            var fotos = await _db.Fotos.Where(f => f.CabanaId == cabanaId).ToListAsync();
            for (var i = 0; i < idsFotos.Count; i++)
            {
                var foto = fotos.FirstOrDefault(f => f.Id == idsFotos[i]);
                if (foto is not null)
                {
                    foto.Orden = i;
                }
            }
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFoto(int id, int cabanaId)
        {
            var foto = await _db.Fotos.FindAsync(id);
            if (foto is not null)
            {
                await _almacenamiento.EliminarAsync(foto.RutaArchivo);
                _db.Fotos.Remove(foto);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = cabanaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFotos(int cabanaId, List<int> ids)
        {
            if (ids is { Count: > 0 })
            {
                var fotos = await _db.Fotos.Where(f => f.CabanaId == cabanaId && ids.Contains(f.Id)).ToListAsync();
                foreach (var foto in fotos)
                {
                    await _almacenamiento.EliminarAsync(foto.RutaArchivo);
                }
                _db.Fotos.RemoveRange(fotos);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = fotos.Count == 1 ? "Se eliminó 1 foto." : $"Se eliminaron {fotos.Count} fotos.";
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

            var promos = await _disponibilidad.ObtenerPromosEnRangoAsync(id, primerDia, ultimoDia);

            var dias = new List<DiaTarifaVista>();
            for (var fecha = primerDia; fecha <= ultimoDia; fecha = fecha.AddDays(1))
            {
                var tarifa = tarifas.FirstOrDefault(t => t.Fecha.Date == fecha.Date);
                var promoDelDia = promos.FirstOrDefault(p => p.FechaDesde.Date <= fecha.Date && p.FechaHasta.Date >= fecha.Date);
                dias.Add(new DiaTarifaVista
                {
                    Fecha = fecha,
                    Reservada = reservas.Any(r => r.FechaDesde <= fecha && fecha < r.FechaHasta),
                    Pasada = fecha < hoy,
                    Precio = tarifa?.Precio,
                    EnPromo = promoDelDia is not null,
                    EtiquetaPromo = promoDelDia?.Nombre
                });
            }

            ViewBag.Cabana = cabana;
            ViewBag.Dias = dias;
            ViewBag.PromosEstadia = await _db.PromosEstadia
                .Where(p => p.CabanaId == id)
                .OrderByDescending(p => p.FechaDesde)
                .ToListAsync();
            ViewBag.PrimerDia = primerDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);

            return View();
        }

        public async Task<IActionResult> Precios(int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);

            ViewBag.Cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Id).ToListAsync();
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
        public async Task<IActionResult> AplicarPreciosPorDiaSemana(List<int> cabanaIds, int anio, int mes, decimal? precioSemana, decimal? precioSabado, decimal? precioDomingo)
        {
            if (cabanaIds is null || cabanaIds.Count == 0)
            {
                TempData["Mensaje"] = "Seleccioná al menos una cabaña para aplicar los precios.";
                return RedirectToAction(nameof(Precios), new { anio, mes });
            }

            if (!precioSemana.HasValue && !precioSabado.HasValue && !precioDomingo.HasValue)
            {
                TempData["Mensaje"] = "Cargá al menos un precio (lunes a viernes, sábado o domingo) para aplicar.";
                return RedirectToAction(nameof(Precios), new { anio, mes });
            }

            var cabanas = await _db.Cabanas.Where(c => cabanaIds.Contains(c.Id)).ToListAsync();
            if (cabanas.Count == 0)
            {
                return NotFound();
            }

            var primerDia = new DateTime(anio, mes, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);
            var hoy = DateTime.Today;

            var reservas = await _db.Reservas
                .Where(r => cabanaIds.Contains(r.CabanaId) && r.Estado == EstadoReserva.Confirmada && r.FechaDesde <= ultimoDia && r.FechaHasta >= primerDia)
                .ToListAsync();

            var dias = new List<TarifaDiaInput>();
            foreach (var cabana in cabanas)
            {
                for (var fecha = primerDia; fecha <= ultimoDia; fecha = fecha.AddDays(1))
                {
                    if (fecha < hoy || reservas.Any(r => r.CabanaId == cabana.Id && r.FechaDesde <= fecha && fecha < r.FechaHasta))
                    {
                        continue;
                    }

                    decimal? precio = fecha.DayOfWeek switch
                    {
                        DayOfWeek.Saturday => precioSabado,
                        DayOfWeek.Sunday => precioDomingo,
                        _ => precioSemana
                    };

                    if (!precio.HasValue)
                    {
                        continue;
                    }

                    dias.Add(new TarifaDiaInput
                    {
                        CabanaId = cabana.Id,
                        Fecha = fecha,
                        Precio = precio
                    });
                }
            }

            await _disponibilidad.GuardarTarifasAsync(dias);

            var nombres = string.Join(", ", cabanas.Select(c => c.Nombre));
            TempData["Mensaje"] = $"Precios de {nombres} actualizados para {primerDia:MMMM yyyy}.";
            return RedirectToAction(nameof(Precios), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPromoEstadia(int id, int cabanaId, int? anio, int? mes)
        {
            var promo = await _db.PromosEstadia.FindAsync(id);
            if (promo is not null)
            {
                promo.Activa = !promo.Activa;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Tarifas), new { id = cabanaId, anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPromoEstadia(int id, int cabanaId, int? anio, int? mes)
        {
            var promo = await _db.PromosEstadia.FindAsync(id);
            if (promo is not null)
            {
                _db.PromosEstadia.Remove(promo);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Tarifas), new { id = cabanaId, anio, mes });
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
                await _almacenamiento.EliminarAsync(foto.RutaArchivo);
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

                var nombreBlob = $"cabanas/{cabanaId}/{Guid.NewGuid()}{extension}";

                using var stream = archivo.OpenReadStream();
                var url = await _almacenamiento.SubirAsync(stream, nombreBlob, archivo.ContentType);

                _db.Fotos.Add(new FotoCabana
                {
                    CabanaId = cabanaId,
                    RutaArchivo = url,
                    Orden = ordenActual++
                });
            }

            await _db.SaveChangesAsync();
        }
    }
}
