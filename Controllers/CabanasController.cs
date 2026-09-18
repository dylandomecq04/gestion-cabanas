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

        public async Task<IActionResult> Index()
        {
            var cabanas = await _db.Cabanas
                .Where(c => c.Activa)
                .Include(c => c.Fotos)
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            ViewBag.PreciosDesde = await _disponibilidad.ObtenerPreciosDesdeAsync();

            return View(cabanas);
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
            await CargarPrecioDesdeAsync(id);

            if (TempData["ReservaId"] is int reservaId)
            {
                var reservaConfirmada = await _db.Reservas.FindAsync(reservaId);
                ViewBag.ReservaConfirmada = reservaConfirmada;
                if (reservaConfirmada is not null)
                {
                    ViewBag.ValorTotalReserva = await _disponibilidad.CalcularValorTotalAsync(
                        id, reservaConfirmada.FechaDesde, reservaConfirmada.FechaHasta,
                        new Huespedes(reservaConfirmada.CantidadAdultos, reservaConfirmada.CantidadMenores));
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
                ModelState.AddModelError(nameof(modelo.CantidadAdultos), $"Esta cabaña tiene capacidad para {cabana.Capacidad} personas (contando a los menores)");
            }
            else if (_disponibilidad.Politica.Resolver(modelo.CantidadAdultos, modelo.CantidadMenores) is null)
            {
                ModelState.AddModelError(nameof(modelo.CantidadAdultos), "Para un grupo de este tamaño hay que armar la reserva en más de una cabaña. Escribinos y lo coordinamos.");
            }

            if (!ModelState.IsValid)
            {
                modelo.NombreCabana = cabana.Nombre;
                ViewBag.Cabana = cabana;
                await CargarCalendarioAsync(modelo.CabanaId, modelo.FechaDesde.Year, modelo.FechaDesde.Month);
                await CargarPrecioDesdeAsync(modelo.CabanaId);
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
                CantidadMenores = modelo.CantidadMenores,
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
        public async Task<IActionResult> CalcularTotal(int id, DateTime? desde, DateTime? hasta, int adultos = 2, int menores = 0)
        {
            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == id && c.Activa);
            if (cabana is null || desde is null || hasta is null || hasta <= desde)
            {
                return Json(new { valido = false });
            }

            var disponible = !await _disponibilidad.HaySuperposicionAsync(id, desde.Value, hasta.Value);
            var huespedes = new Huespedes(adultos, menores);
            var detalle = await _disponibilidad.CalcularValorConDetalleAsync(id, desde.Value, hasta.Value, huespedes);
            var noches = (hasta.Value - desde.Value).Days;

            string? aviso = null;
            if (adultos < 1)
            {
                aviso = "Tiene que haber al menos un adulto.";
            }
            else if (huespedes.Total > cabana.Capacidad)
            {
                aviso = $"Esta cabaña tiene capacidad para {cabana.Capacidad} personas (contando a los menores).";
            }
            else if (_disponibilidad.Politica.Resolver(adultos, menores) is null)
            {
                aviso = "Para un grupo de este tamaño hay que armar la reserva en más de una cabaña. Escribinos y lo coordinamos.";
            }

            return Json(new
            {
                valido = true,
                disponible,
                total = aviso is null ? detalle.Total : null,
                noches,
                promoAplicada = detalle.PromoAplicada,
                etiquetaPromo = detalle.EtiquetaPromo,
                etiquetaTarifa = aviso is null ? detalle.EtiquetaTarifa : null,
                aviso
            });
        }

        [HttpGet]
        public async Task<IActionResult> BuscarOpciones(DateTime? desde, DateTime? hasta, int adultos = 2, int menores = 0)
        {
            if (desde is null || hasta is null || hasta <= desde)
            {
                return Json(new { valido = false, mensaje = "Elegí un rango de fechas válido." });
            }

            if (desde.Value.Date < DateTime.Today)
            {
                return Json(new { valido = false, mensaje = "La fecha de entrada no puede ser anterior a hoy." });
            }

            if (adultos < 1 || menores < 0)
            {
                return Json(new { valido = false, mensaje = "Tiene que haber al menos un adulto." });
            }

            if (_disponibilidad.Politica.Resolver(adultos, menores) is null)
            {
                return Json(new { valido = false, mensaje = "Para un grupo de este tamaño hay que armar la reserva en más de una cabaña. Escribinos y lo coordinamos." });
            }

            var resultado = await _disponibilidad.BuscarOpcionesAsync(desde.Value.Date, hasta.Value.Date, new Huespedes(adultos, menores));

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
                        etiquetaPromo = s.EtiquetaPromo,
                        etiquetaTarifa = s.EtiquetaTarifa
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

                if (_disponibilidad.Politica.Resolver(modelo.CantidadAdultos, modelo.CantidadMenores) is null)
                {
                    return Json(new { exito = false, mensaje = "Para un grupo de este tamaño hay que armar la reserva en más de una cabaña. Escribinos y lo coordinamos." });
                }

                if (await _disponibilidad.HaySuperposicionAsync(segmento.CabanaId, segmento.FechaDesde, segmento.FechaHasta))
                {
                    return Json(new { exito = false, mensaje = $"{cabana.Nombre} ya no está disponible para esas fechas. Volvé a buscar." });
                }
            }

            var huespedes = new Huespedes(modelo.CantidadAdultos, modelo.CantidadMenores);
            var reservasCreadas = new List<Reserva>();
            foreach (var segmento in modelo.Segmentos)
            {
                var valor = await _disponibilidad.CalcularValorTotalAsync(segmento.CabanaId, segmento.FechaDesde, segmento.FechaHasta, huespedes);

                var reserva = new Reserva
                {
                    CabanaId = segmento.CabanaId,
                    NombreHuesped = modelo.NombreHuesped,
                    Telefono = modelo.Telefono,
                    FechaDesde = segmento.FechaDesde,
                    FechaHasta = segmento.FechaHasta,
                    CantidadPersonas = modelo.CantidadPersonas,
                    CantidadMenores = modelo.CantidadMenores,
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
            var promos = await _disponibilidad.ObtenerPromosEnRangoTodasCabanasAsync(primerDia, ultimoDia);

            ViewBag.Cabanas = cabanas;
            ViewBag.Reservas = reservas;
            ViewBag.Promos = promos;
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);

            return View();
        }

        private async Task CargarPrecioDesdeAsync(int cabanaId)
        {
            var preciosDesde = await _disponibilidad.ObtenerPreciosDesdeAsync();
            ViewBag.PrecioDesde = preciosDesde.TryGetValue(cabanaId, out var precio) ? precio : (decimal?)null;
        }

        private async Task CargarCalendarioAsync(int cabanaId, int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            ViewBag.Reservas = await _disponibilidad.ObtenerConfirmadasEnRangoAsync(primerDia, ultimoDia, cabanaId);
            ViewBag.PromosEstadia = await _disponibilidad.ObtenerPromosEnRangoAsync(cabanaId, primerDia, ultimoDia);
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);
        }
    }
}
