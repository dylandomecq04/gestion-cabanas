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

        public async Task<IActionResult> Create()
        {
            var ultima = await _db.Cabanas.MaxAsync(c => (int?)c.Orden) ?? 0;
            return View(new Cabana { Orden = ultima + 1 });
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
            cabana.Orden = modelo.Orden;
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
                    Precio2 = tarifa?.Precio2,
                    Precio4 = tarifa?.Precio4,
                    Precio6 = tarifa?.Precio6,
                    EnPromo = promoDelDia is not null,
                    EtiquetaPromo = promoDelDia?.Nombre,
                    PromoPrecioPorNoche = promoDelDia?.PrecioPromedioPorNoche()
                });
            }

            var promosLista = await _db.PromosEstadia
                .Where(p => p.CabanaId == id)
                .OrderByDescending(p => p.FechaDesde)
                .ToListAsync();

            var tramosCabana = cabana.TramosDePrecio().ToList();
            var comparaciones = new Dictionary<int, List<ComparacionPromoNoches>>();
            foreach (var promo in promosLista)
            {
                var comparacionesPromo = new List<ComparacionPromoNoches>();
                foreach (var noches in new[] { 1, 2, 3 })
                {
                    var precioPromo = promo.PrecioPorNoches(noches);
                    if (!precioPromo.HasValue)
                    {
                        continue;
                    }

                    var preciosRegulares = new List<(int Tramo, decimal? PrecioRegular)>();
                    foreach (var tramo in tramosCabana)
                    {
                        var regular = await _disponibilidad.PrecioRegularEstadiaAsync(promo.CabanaId, promo.FechaDesde, noches, tramo);
                        preciosRegulares.Add((tramo, regular));
                    }

                    comparacionesPromo.Add(new ComparacionPromoNoches
                    {
                        Noches = noches,
                        PrecioPromo = precioPromo.Value,
                        PreciosRegulares = preciosRegulares
                    });
                }
                comparaciones[promo.Id] = comparacionesPromo;
            }

            ViewBag.Cabana = cabana;
            ViewBag.Dias = dias;
            ViewBag.PromosEstadia = promosLista;
            ViewBag.ComparacionesPromo = comparaciones;
            ViewBag.FinesDeSemanaLargos = await _disponibilidad.ObtenerFinesDeSemanaLargosExigidosAsync(primerDia, ultimoDia.AddDays(1));
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
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Id).ToListAsync();
            ViewBag.Cabanas = cabanas;
            ViewBag.GruposCabanas = cabanas
                .GroupBy(c => c.Grupo)
                .OrderBy(g => g.Min(c => c.Orden))
                .ToList();

            var tarifasDelMes = await _db.TarifasDias
                .Where(t => t.Fecha >= primerDia && t.Fecha <= ultimoDia)
                .ToListAsync();

            static string TipoDia(DateTime fecha) => fecha.DayOfWeek switch
            {
                DayOfWeek.Saturday => "Sabado",
                DayOfWeek.Sunday => "Domingo",
                _ => "Semana"
            };

            static decimal? ValorUniforme(IEnumerable<decimal?> valores)
            {
                var distintos = valores.Where(v => v.HasValue).Select(v => v!.Value).Distinct().ToList();
                return distintos.Count == 1 ? distintos[0] : (decimal?)null;
            }

            // Precio ya cargado para cada cabaña/tipo de día/tramo, solo cuando es el mismo en todo el mes
            // (así el formulario puede precargarlo sin arriesgarse a mostrar un valor que no representa a todo el mes).
            ViewBag.PreciosPrecargados = cabanas.ToDictionary(
                c => c.Id,
                c => new[] { "Semana", "Sabado", "Domingo" }.ToDictionary(
                    tipo => tipo,
                    tipo =>
                    {
                        var tarifasCabanaTipo = tarifasDelMes.Where(t => t.CabanaId == c.Id && TipoDia(t.Fecha) == tipo).ToList();
                        return Cabana.TodosLosTramos.ToDictionary(
                            tramo => tramo,
                            tramo => ValorUniforme(tarifasCabanaTipo.Select(t => t.PrecioDelTramo(tramo))));
                    }));

            ViewBag.MinimosNoches = await _disponibilidad.ObtenerMinimosNochesAsync();
            ViewBag.PromosEstadia = await _db.PromosEstadia
                .Include(p => p.Cabana)
                .OrderByDescending(p => p.FechaDesde)
                .ToListAsync();
            ViewBag.DescuentoSalidaAnticipada = await _db.DescuentosSalidaAnticipada
                .Where(d => d.Anio == primerDia.Year && d.Mes == primerDia.Month)
                .Select(d => (decimal?)d.Monto)
                .FirstOrDefaultAsync();
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
        public async Task<IActionResult> GuardarDescuentoSalidaAnticipada(int anio, int mes, decimal? monto)
        {
            if (mes < 1 || mes > 12 || anio < 2000 || anio > 2100 || monto < 0)
            {
                return RedirectToAction(nameof(Precios));
            }

            var mesTexto = new DateTime(anio, mes, 1).ToString("MMMM yyyy");
            var existente = await _db.DescuentosSalidaAnticipada.FirstOrDefaultAsync(d => d.Anio == anio && d.Mes == mes);

            if (!monto.HasValue || monto.Value == 0)
            {
                if (existente is not null)
                {
                    _db.DescuentosSalidaAnticipada.Remove(existente);
                    await _db.SaveChangesAsync();
                }
                TempData["Mensaje"] = $"Sin descuento por salida anticipada en {mesTexto}.";
            }
            else
            {
                if (existente is null)
                {
                    existente = new DescuentoSalidaAnticipada { Anio = anio, Mes = mes };
                    _db.DescuentosSalidaAnticipada.Add(existente);
                }
                existente.Monto = monto.Value;
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = $"Descuento por salida anticipada del domingo en {mesTexto}: ${monto.Value:N0}.";
            }

            return RedirectToAction(nameof(Precios), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarMinimoNoches(int anio, int mes, List<MinimoNocheInput> dias)
        {
            await _disponibilidad.GuardarMinimosNochesAsync(dias ?? new List<MinimoNocheInput>());
            TempData["Mensaje"] = "Mínimo de noches actualizado.";
            return RedirectToAction(nameof(Precios), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarMinimoNoches(int anio, int mes, DateTime fecha)
        {
            var existente = await _db.MinimosNoches.FirstOrDefaultAsync(m => m.Fecha == fecha.Date);
            if (existente is not null)
            {
                _db.MinimosNoches.Remove(existente);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = $"Se quitó el mínimo de noches para el {fecha:dddd d/M}.";
            }

            return RedirectToAction(nameof(Precios), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarPreciosPorDiaSemana(
            List<int> cabanaIds, int anio, int mes,
            decimal? precioSemana2, decimal? precioSemana4, decimal? precioSemana6,
            decimal? precioSabado2, decimal? precioSabado4, decimal? precioSabado6,
            decimal? precioDomingo2, decimal? precioDomingo4, decimal? precioDomingo6)
        {
            if (cabanaIds is null || cabanaIds.Count == 0)
            {
                TempData["Mensaje"] = "Seleccioná al menos una cabaña para aplicar los precios.";
                return RedirectToAction(nameof(Precios), new { anio, mes });
            }

            var preciosSemana = new[] { precioSemana2, precioSemana4, precioSemana6 };
            var preciosSabado = new[] { precioSabado2, precioSabado4, precioSabado6 };
            var preciosDomingo = new[] { precioDomingo2, precioDomingo4, precioDomingo6 };

            if (preciosSemana.Concat(preciosSabado).Concat(preciosDomingo).All(p => !p.HasValue))
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

            var existentes = (await _db.TarifasDias
                .Where(t => cabanaIds.Contains(t.CabanaId) && t.Fecha >= primerDia && t.Fecha <= ultimoDia)
                .ToListAsync())
                .ToDictionary(t => (t.CabanaId, t.Fecha.Date));

            // Sólo se pisan los tramos que se cargaron: los que se dejan vacíos conservan lo que ya había.
            var dias = new List<TarifaDiaInput>();
            foreach (var cabana in cabanas)
            {
                for (var fecha = primerDia; fecha <= ultimoDia; fecha = fecha.AddDays(1))
                {
                    if (fecha < hoy || reservas.Any(r => r.CabanaId == cabana.Id && r.FechaDesde <= fecha && fecha < r.FechaHasta))
                    {
                        continue;
                    }

                    var preciosDelDia = fecha.DayOfWeek switch
                    {
                        DayOfWeek.Saturday => preciosSabado,
                        DayOfWeek.Sunday => preciosDomingo,
                        _ => preciosSemana
                    };

                    // Solo cuentan los tramos que esta cabaña admite (una para 4 no tiene precio para 6).
                    var precios = preciosDelDia
                        .Select((precio, i) => cabana.AdmiteTramo(Cabana.TodosLosTramos[i]) ? precio : null)
                        .ToArray();

                    if (precios.All(p => !p.HasValue))
                    {
                        continue;
                    }

                    existentes.TryGetValue((cabana.Id, fecha), out var actual);
                    dias.Add(new TarifaDiaInput
                    {
                        CabanaId = cabana.Id,
                        Fecha = fecha,
                        Precio2 = precios[0] ?? actual?.Precio2,
                        Precio4 = precios[1] ?? actual?.Precio4,
                        Precio6 = precios[2] ?? actual?.Precio6
                    });
                }
            }

            await _disponibilidad.GuardarTarifasAsync(dias);

            var nombres = string.Join(", ", cabanas.Select(c => c.Nombre));
            var mensaje = $"Precios de {nombres} actualizados para {primerDia:MMMM yyyy}.";

            // Avisar de los tramos cargados que no se aplicaron a alguna cabaña por no tener lugar para tantas personas.
            var omitidos = new List<string>();
            for (var i = 0; i < Cabana.TodosLosTramos.Length; i++)
            {
                var tramo = Cabana.TodosLosTramos[i];
                var sinLugar = cabanas.Where(c => !c.AdmiteTramo(tramo)).Select(c => c.Nombre).ToList();
                var cargado = preciosSemana[i].HasValue || preciosSabado[i].HasValue || preciosDomingo[i].HasValue;
                if (sinLugar.Count > 0 && cargado)
                {
                    omitidos.Add($"el precio para {tramo} no se aplicó a {string.Join(", ", sinLugar)} (no tiene{(sinLugar.Count > 1 ? "n" : "")} lugar para tantas personas)");
                }
            }
            if (omitidos.Count > 0)
            {
                var detalle = string.Join("; ", omitidos);
                mensaje = dias.Count == 0
                    ? $"No se cargó ningún precio: {detalle}."
                    : mensaje + $" Ojo: {detalle}.";
            }

            TempData["Mensaje"] = mensaje;
            return RedirectToAction(nameof(Precios), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopiarPreciosMesSiguiente(int anio, int mes)
        {
            var primerDia = new DateTime(anio, mes, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);
            var primerDiaSiguiente = primerDia.AddMonths(1);
            var ultimoDiaSiguiente = primerDiaSiguiente.AddMonths(1).AddDays(-1);
            var diasEnMesSiguiente = ultimoDiaSiguiente.Day;
            var hoy = DateTime.Today;

            var tarifasOrigen = await _db.TarifasDias
                .Where(t => t.Fecha >= primerDia && t.Fecha <= ultimoDia)
                .ToListAsync();

            if (tarifasOrigen.Count == 0)
            {
                TempData["Mensaje"] = $"No hay precios cargados en {primerDia:MMMM yyyy} para copiar.";
                return RedirectToAction(nameof(Precios), new { anio, mes });
            }

            var reservasDestino = await _db.Reservas
                .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaDesde <= ultimoDiaSiguiente && r.FechaHasta >= primerDiaSiguiente)
                .ToListAsync();

            // Se pisan los precios ya cargados del mes siguiente, salvo los días pasados o ya reservados.
            var dias = new List<TarifaDiaInput>();
            foreach (var tarifa in tarifasOrigen)
            {
                if (tarifa.Fecha.Day > diasEnMesSiguiente)
                {
                    continue;
                }

                var fechaDestino = new DateTime(primerDiaSiguiente.Year, primerDiaSiguiente.Month, tarifa.Fecha.Day);
                if (fechaDestino < hoy || reservasDestino.Any(r => r.CabanaId == tarifa.CabanaId && r.FechaDesde <= fechaDestino && fechaDestino < r.FechaHasta))
                {
                    continue;
                }

                dias.Add(new TarifaDiaInput
                {
                    CabanaId = tarifa.CabanaId,
                    Fecha = fechaDestino,
                    Precio2 = tarifa.Precio2,
                    Precio4 = tarifa.Precio4,
                    Precio6 = tarifa.Precio6
                });
            }

            await _disponibilidad.GuardarTarifasAsync(dias);

            TempData["Mensaje"] = dias.Count > 0
                ? $"Precios de {primerDia:MMMM yyyy} copiados a {primerDiaSiguiente:MMMM yyyy}."
                : $"No se copió ningún precio: los días de {primerDiaSiguiente:MMMM yyyy} ya pasaron o están reservados.";
            return RedirectToAction(nameof(Precios), new { anio = primerDiaSiguiente.Year, mes = primerDiaSiguiente.Month });
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
        public async Task<IActionResult> CrearPromoEstadia(PromoEstadiaMultipleInput modelo, int? anio, int? mes)
        {
            if (modelo.CabanaIds is null || modelo.CabanaIds.Count == 0)
            {
                TempData["Mensaje"] = "Elegí al menos una cabaña para la promoción.";
                return RedirectToAction(nameof(Precios), new { anio, mes });
            }

            if (modelo.FechaHasta.Date < modelo.FechaDesde.Date)
            {
                TempData["Mensaje"] = "El rango de fechas de la promoción no es válido.";
                return RedirectToAction(nameof(Precios), new { anio, mes });
            }

            if (!modelo.Precio1Noche.HasValue && !modelo.Precio2Noches.HasValue && !modelo.Precio3Noches.HasValue)
            {
                TempData["Mensaje"] = "Cargá al menos un precio (1, 2 o 3 noches) para la promoción.";
                return RedirectToAction(nameof(Precios), new { anio, mes });
            }

            var cabanaIdsValidos = await _db.Cabanas
                .Where(c => modelo.CabanaIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var cabanaId in cabanaIdsValidos)
            {
                _db.PromosEstadia.Add(new PromoEstadia
                {
                    CabanaId = cabanaId,
                    Nombre = modelo.Nombre,
                    Descripcion = modelo.Descripcion,
                    FechaDesde = modelo.FechaDesde.Date,
                    FechaHasta = modelo.FechaHasta.Date,
                    Precio1Noche = modelo.Precio1Noche,
                    Precio2Noches = modelo.Precio2Noches,
                    Precio3Noches = modelo.Precio3Noches,
                    Activa = true
                });
            }

            await _db.SaveChangesAsync();
            TempData["Mensaje"] = cabanaIdsValidos.Count > 1
                ? $"Promoción creada para {cabanaIdsValidos.Count} cabañas."
                : "Promoción creada.";
            return RedirectToAction(nameof(Precios), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPromoEstadiaEnPrecios(int id, int? anio, int? mes)
        {
            var promo = await _db.PromosEstadia.FindAsync(id);
            if (promo is not null)
            {
                promo.Activa = !promo.Activa;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Precios), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPromoEstadiaEnPrecios(int id, int? anio, int? mes)
        {
            var promo = await _db.PromosEstadia.FindAsync(id);
            if (promo is not null)
            {
                _db.PromosEstadia.Remove(promo);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Precios), new { anio, mes });
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
