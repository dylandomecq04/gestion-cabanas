using GestionCabanas.Data;
using GestionCabanas.Models;
using GestionCabanas.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Controllers
{
    public class CabanasController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly DisponibilidadService _disponibilidad;
        private readonly INotificacionEmailService _email;

        public CabanasController(ApplicationDbContext db, DisponibilidadService disponibilidad, INotificacionEmailService email)
        {
            _db = db;
            _disponibilidad = disponibilidad;
            _email = email;
        }

        public async Task<IActionResult> Details(int id, int? anio, int? mes)
        {
            var cabana = await _db.Cabanas
                .Include(c => c.Fotos)
                .FirstOrDefaultAsync(c => c.Id == id && c.Activa);

            if (cabana is null)
            {
                return NotFound();
            }

            ViewBag.Cabana = cabana;
            await CargarCalendarioAsync(id, anio, mes);

            if (TempData["ReservaId"] is int reservaId)
            {
                var reservaConfirmada = await _db.Reservas.FindAsync(reservaId);
                ViewBag.ReservaConfirmada = reservaConfirmada;
                if (reservaConfirmada is not null)
                {
                    ViewBag.ValorTotalReserva = await _disponibilidad.CalcularValorTotalAsync(
                        id, reservaConfirmada.FechaDesde, reservaConfirmada.FechaHasta, cabana.PrecioPorNoche);
                }
            }

            return View(new SolicitarReservaViewModel
            {
                CabanaId = cabana.Id,
                NombreCabana = cabana.Nombre
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Solicitar(SolicitarReservaViewModel modelo)
        {
            var cabana = await _db.Cabanas.Include(c => c.Fotos).FirstOrDefaultAsync(c => c.Id == modelo.CabanaId && c.Activa);
            if (cabana is null)
            {
                return NotFound();
            }

            if (modelo.FechaHasta <= modelo.FechaDesde)
            {
                ModelState.AddModelError(nameof(modelo.FechaHasta), "La fecha de salida debe ser posterior a la de entrada");
            }
            else if (modelo.FechaDesde < DateTime.Today)
            {
                ModelState.AddModelError(nameof(modelo.FechaDesde), "La fecha de entrada no puede ser anterior a hoy");
            }
            else if (await _disponibilidad.HaySuperposicionAsync(modelo.CabanaId, modelo.FechaDesde, modelo.FechaHasta))
            {
                ModelState.AddModelError(string.Empty, "Esas fechas ya no están disponibles para esta cabaña. Elegí otro rango.");
            }

            if (modelo.CantidadPersonas > cabana.Capacidad)
            {
                ModelState.AddModelError(nameof(modelo.CantidadPersonas), $"Esta cabaña tiene capacidad para {cabana.Capacidad} personas");
            }

            if (!ModelState.IsValid)
            {
                modelo.NombreCabana = cabana.Nombre;
                ViewBag.Cabana = cabana;
                await CargarCalendarioAsync(modelo.CabanaId, modelo.FechaDesde.Year, modelo.FechaDesde.Month);
                return View("Details", modelo);
            }

            var reserva = new Reserva
            {
                CabanaId = modelo.CabanaId,
                NombreHuesped = modelo.NombreHuesped,
                Telefono = modelo.Telefono,
                FechaDesde = modelo.FechaDesde,
                FechaHasta = modelo.FechaHasta,
                CantidadPersonas = modelo.CantidadPersonas,
                Estado = EstadoReserva.Pendiente
            };
            _db.Reservas.Add(reserva);
            await _db.SaveChangesAsync();

            await _email.NotificarNuevaSolicitudAsync(cabana, reserva);

            TempData["SolicitudEnviada"] = $"¡Listo! Recibimos tu solicitud para {cabana.Nombre}.";
            TempData["ReservaId"] = reserva.Id;
            return RedirectToAction(nameof(Details), new { id = modelo.CabanaId });
        }

        [HttpGet]
        public async Task<IActionResult> CalcularTotal(int id, DateTime? desde, DateTime? hasta)
        {
            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == id && c.Activa);
            if (cabana is null || desde is null || hasta is null || hasta <= desde)
            {
                return Json(new { valido = false });
            }

            var disponible = !await _disponibilidad.HaySuperposicionAsync(id, desde.Value, hasta.Value);
            var detalle = await _disponibilidad.CalcularValorConDetalleAsync(id, desde.Value, hasta.Value, cabana.PrecioPorNoche);
            var noches = (hasta.Value - desde.Value).Days;

            return Json(new
            {
                valido = true,
                disponible,
                total = detalle.Total,
                noches,
                promoAplicada = detalle.PromoAplicada,
                etiquetaPromo = detalle.EtiquetaPromo
            });
        }

        [HttpGet]
        public async Task<IActionResult> BuscarOpciones(DateTime? desde, DateTime? hasta, int personas = 1)
        {
            if (desde is null || hasta is null || hasta <= desde)
            {
                return Json(new { valido = false, mensaje = "Elegí un rango de fechas válido." });
            }

            if (desde.Value.Date < DateTime.Today)
            {
                return Json(new { valido = false, mensaje = "La fecha de entrada no puede ser anterior a hoy." });
            }

            var resultado = await _disponibilidad.BuscarOpcionesAsync(desde.Value.Date, hasta.Value.Date, personas);

            return Json(new
            {
                valido = true,
                cobreTotal = resultado.CobreTotal,
                mensaje = resultado.Mensaje,
                diasSinCobertura = resultado.DiasSinCobertura.Select(d => d.ToString("yyyy-MM-dd")),
                opciones = resultado.Opciones.Select(o => new
                {
                    total = o.Total,
                    promoAplicada = o.Segmentos.Any(s => s.PromoAplicada),
                    segmentos = o.Segmentos.Select(s => new
                    {
                        cabanaId = s.CabanaId,
                        cabanaNombre = s.CabanaNombre,
                        desde = s.Desde.ToString("yyyy-MM-dd"),
                        hasta = s.Hasta.ToString("yyyy-MM-dd"),
                        subtotal = s.Subtotal,
                        promoAplicada = s.PromoAplicada,
                        etiquetaPromo = s.EtiquetaPromo
                    })
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarOpcion(SolicitarOpcionViewModel modelo)
        {
            if (!ModelState.IsValid || modelo.Segmentos.Count == 0)
            {
                return Json(new { exito = false, mensaje = "Faltan datos para completar la solicitud." });
            }

            var cabanaIds = modelo.Segmentos.Select(s => s.CabanaId).Distinct().ToList();
            var cabanas = await _db.Cabanas.Where(c => cabanaIds.Contains(c.Id) && c.Activa).ToListAsync();

            if (cabanas.Count != cabanaIds.Count)
            {
                return Json(new { exito = false, mensaje = "Una de las cabañas de esta opción ya no está disponible. Volvé a buscar." });
            }

            foreach (var segmento in modelo.Segmentos)
            {
                if (segmento.FechaHasta <= segmento.FechaDesde || segmento.FechaDesde < DateTime.Today)
                {
                    return Json(new { exito = false, mensaje = "Las fechas de esta opción ya no son válidas. Volvé a buscar." });
                }

                var cabana = cabanas.First(c => c.Id == segmento.CabanaId);
                if (modelo.CantidadPersonas > cabana.Capacidad)
                {
                    return Json(new { exito = false, mensaje = $"{cabana.Nombre} tiene capacidad para {cabana.Capacidad} personas." });
                }

                if (await _disponibilidad.HaySuperposicionAsync(segmento.CabanaId, segmento.FechaDesde, segmento.FechaHasta))
                {
                    return Json(new { exito = false, mensaje = $"{cabana.Nombre} ya no está disponible para esas fechas. Volvé a buscar." });
                }
            }

            var reservasCreadas = new List<Reserva>();
            foreach (var segmento in modelo.Segmentos)
            {
                var cabana = cabanas.First(c => c.Id == segmento.CabanaId);
                var valor = await _disponibilidad.CalcularValorTotalAsync(segmento.CabanaId, segmento.FechaDesde, segmento.FechaHasta, cabana.PrecioPorNoche);

                var reserva = new Reserva
                {
                    CabanaId = segmento.CabanaId,
                    NombreHuesped = modelo.NombreHuesped,
                    Telefono = modelo.Telefono,
                    FechaDesde = segmento.FechaDesde,
                    FechaHasta = segmento.FechaHasta,
                    CantidadPersonas = modelo.CantidadPersonas,
                    Estado = EstadoReserva.Pendiente,
                    Valor = valor
                };
                _db.Reservas.Add(reserva);
                reservasCreadas.Add(reserva);
            }

            await _db.SaveChangesAsync();

            foreach (var reserva in reservasCreadas)
            {
                var cabana = cabanas.First(c => c.Id == reserva.CabanaId);
                await _email.NotificarNuevaSolicitudAsync(cabana, reserva);
            }

            decimal? total = 0;
            foreach (var reserva in reservasCreadas)
            {
                total = total.HasValue && reserva.Valor.HasValue ? total + reserva.Valor : null;
            }

            return Json(new
            {
                exito = true,
                total,
                cabanas = cabanas.Select(c => c.Nombre),
                desde = modelo.Segmentos.Min(s => s.FechaDesde).ToString("dd/MM/yyyy"),
                hasta = modelo.Segmentos.Max(s => s.FechaHasta).ToString("dd/MM/yyyy"),
                nombreHuesped = modelo.NombreHuesped,
                cantidadPersonas = modelo.CantidadPersonas
            });
        }

        public async Task<IActionResult> Disponibilidad(int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();
            var reservas = await _disponibilidad.ObtenerConfirmadasEnRangoAsync(primerDia, ultimoDia);
            var bloqueadas = await _disponibilidad.ObtenerBloqueadasEnRangoAsync(primerDia, ultimoDia);
            var promos = await _disponibilidad.ObtenerPromosEnRangoTodasCabanasAsync(primerDia, ultimoDia);

            ViewBag.Cabanas = cabanas;
            ViewBag.Reservas = reservas;
            ViewBag.Bloqueadas = bloqueadas;
            ViewBag.Promos = promos;
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);

            return View();
        }

        private async Task CargarCalendarioAsync(int cabanaId, int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            ViewBag.Reservas = await _disponibilidad.ObtenerConfirmadasEnRangoAsync(primerDia, ultimoDia, cabanaId);
            ViewBag.Tarifas = await _disponibilidad.ObtenerTarifasEnRangoAsync(cabanaId, primerDia, ultimoDia);
            ViewBag.PromosEstadia = await _disponibilidad.ObtenerPromosEnRangoAsync(cabanaId, primerDia, ultimoDia);
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);
        }
    }
}
