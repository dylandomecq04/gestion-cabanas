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

        private static readonly string MensajeMaximoPersonas =
            $"Podemos recibir hasta {PoliticaPrecios.MaxPersonasPorReserva} personas por reserva (contando a los menores).";

        private const string MensajeGrupoGrande =
            "Para un grupo más grande, usá «Reservar» en el menú: te ofrecemos una cabaña más grande o repartirse en dos cabañas.";

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

        public async Task<IActionResult> Details(int id, int? anio, int? mes, DateTime? desde, DateTime? hasta)
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
            ViewBag.SeleccionDesde = desde?.ToString("yyyy-MM-dd");
            ViewBag.SeleccionHasta = hasta?.ToString("yyyy-MM-dd");

            if (TempData["ReservaId"] is int reservaId)
            {
                var reservaConfirmada = await _db.Reservas.FindAsync(reservaId);
                ViewBag.ReservaConfirmada = reservaConfirmada;
                if (reservaConfirmada is not null)
                {
                    ViewBag.ValorTotalReserva = await _disponibilidad.CalcularValorTotalAsync(
                        id, reservaConfirmada.FechaDesde, reservaConfirmada.FechaHasta,
                        new Huespedes(reservaConfirmada.CantidadAdultos, reservaConfirmada.CantidadMenores),
                        reservaConfirmada.SalidaAnticipada);
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
            else if (await _disponibilidad.ValidarFinDeSemanaLargoAsync(modelo.FechaDesde, modelo.FechaHasta) is { } avisoFinLargo)
            {
                ModelState.AddModelError(string.Empty, avisoFinLargo);
            }
            else if (await _disponibilidad.ValidarMinimoNochesAsync(modelo.FechaDesde, modelo.FechaHasta) is { } avisoMinimoNoches)
            {
                ModelState.AddModelError(string.Empty, avisoMinimoNoches);
            }

            if (modelo.CantidadPersonas > PoliticaPrecios.MaxPersonasPorReserva)
            {
                ModelState.AddModelError(nameof(modelo.CantidadAdultos), MensajeMaximoPersonas);
            }
            else if (modelo.CantidadPersonas > cabana.Capacidad)
            {
                ModelState.AddModelError(nameof(modelo.CantidadAdultos), $"Esta cabaña tiene capacidad para {cabana.Capacidad} personas (contando a los menores). {MensajeGrupoGrande}");
            }
            else if (_disponibilidad.Politica.Resolver(modelo.CantidadAdultos, modelo.CantidadMenores) is null)
            {
                ModelState.AddModelError(nameof(modelo.CantidadAdultos), "Tiene que haber al menos un adulto.");
            }

            if (!ModelState.IsValid)
            {
                modelo.NombreCabana = cabana.Nombre;
                ViewBag.Cabana = cabana;
                await CargarCalendarioAsync(modelo.CabanaId, modelo.FechaDesde.Year, modelo.FechaDesde.Month);
                await CargarPrecioDesdeAsync(modelo.CabanaId);
                return View("Details", modelo);
            }

            var salidaAnticipada = modelo.SalidaAnticipada
                && DisponibilidadService.EsElegibleSalidaAnticipada(modelo.FechaDesde, modelo.FechaHasta)
                && await _disponibilidad.MontoSalidaAnticipadaAsync(modelo.FechaHasta) > 0;

            var reserva = new Reserva
            {
                CabanaId = modelo.CabanaId,
                NombreHuesped = modelo.NombreHuesped,
                Telefono = modelo.Telefono,
                Email = modelo.Email,
                FechaDesde = modelo.FechaDesde,
                FechaHasta = modelo.FechaHasta,
                CantidadPersonas = modelo.CantidadPersonas,
                CantidadMenores = modelo.CantidadMenores,
                Estado = EstadoReserva.Pendiente,
                SalidaAnticipada = salidaAnticipada
            };
            _db.Reservas.Add(reserva);
            await _db.SaveChangesAsync();

            await _email.NotificarNuevaSolicitudAsync(cabana, reserva, $"{Request.Scheme}://{Request.Host}");
            if (!string.IsNullOrWhiteSpace(reserva.Email))
            {
                await _email.NotificarConfirmacionHuespedAsync(cabana, reserva);
            }

            TempData["SolicitudEnviada"] = $"¡Listo! Recibimos tu solicitud para {cabana.Nombre}.";
            TempData["ReservaId"] = reserva.Id;
            return RedirectToAction(nameof(Details), new { id = modelo.CabanaId });
        }

        [HttpGet]
        public async Task<IActionResult> CalcularTotal(int id, DateTime? desde, DateTime? hasta, int adultos = 2, int menores = 0, bool salidaAnticipada = false)
        {
            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == id && c.Activa);
            if (cabana is null || desde is null || hasta is null || hasta <= desde)
            {
                return Json(new { valido = false });
            }

            var disponible = !await _disponibilidad.HaySuperposicionAsync(id, desde.Value, hasta.Value);
            var huespedes = new Huespedes(adultos, menores);
            var detalle = await _disponibilidad.CalcularValorConDetalleAsync(id, desde.Value, hasta.Value, huespedes, salidaAnticipada);
            var noches = (hasta.Value - desde.Value).Days;

            var avisoFinLargo = await _disponibilidad.ValidarFinDeSemanaLargoAsync(desde.Value, hasta.Value);

            string? aviso = null;
            if (avisoFinLargo is not null)
            {
                aviso = avisoFinLargo;
            }
            else if (await _disponibilidad.ValidarMinimoNochesAsync(desde.Value, hasta.Value) is { } avisoMinimoNoches)
            {
                aviso = avisoMinimoNoches;
            }
            else if (adultos < 1)
            {
                aviso = "Tiene que haber al menos un adulto.";
            }
            else if (huespedes.Total > PoliticaPrecios.MaxPersonasPorReserva)
            {
                aviso = MensajeMaximoPersonas;
            }
            else if (huespedes.Total > cabana.Capacidad)
            {
                aviso = $"Esta cabaña tiene capacidad para {cabana.Capacidad} personas (contando a los menores). {MensajeGrupoGrande}";
            }
            else if (_disponibilidad.Politica.Resolver(adultos, menores) is null)
            {
                aviso = "Tiene que haber al menos un adulto.";
            }

            return Json(new
            {
                valido = true,
                disponible,
                total = aviso is null ? detalle.Total : null,
                totalSinPromo = aviso is null ? detalle.TotalSinPromo : null,
                noches,
                promoAplicada = detalle.PromoAplicada,
                etiquetaPromo = detalle.EtiquetaPromo,
                etiquetaTarifa = aviso is null ? detalle.EtiquetaTarifa : null,
                elegibleSalidaAnticipada = aviso is null && detalle.ElegibleSalidaAnticipada,
                montoSalidaAnticipada = detalle.MontoSalidaAnticipada,
                salidaAnticipadaAplicada = detalle.SalidaAnticipadaAplicada,
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

            if (adultos + menores > PoliticaPrecios.MaxPersonasPorReserva)
            {
                return Json(new { valido = false, mensaje = MensajeMaximoPersonas });
            }

            var avisoFinLargo = await _disponibilidad.ValidarFinDeSemanaLargoAsync(desde.Value, hasta.Value);
            if (avisoFinLargo is not null)
            {
                return Json(new { valido = false, mensaje = avisoFinLargo });
            }

            if (await _disponibilidad.ValidarMinimoNochesAsync(desde.Value, hasta.Value) is { } avisoMinimoNoches)
            {
                return Json(new { valido = false, mensaje = avisoMinimoNoches });
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
                    totalSinPromo = o.TotalSinPromo,
                    repartida = o.Repartida,
                    contiguas = o.Contiguas,
                    promoAplicada = o.Segmentos.Any(s => s.PromoAplicada),
                    elegibleSalidaAnticipada = o.ElegibleSalidaAnticipada,
                    montoSalidaAnticipada = o.MontoSalidaAnticipada,
                    segmentos = o.Segmentos.Select(s => new
                    {
                        cabanaId = s.CabanaId,
                        cabanaNombre = s.CabanaNombre,
                        adultos = s.Adultos,
                        menores = s.Menores,
                        personas = s.Personas,
                        desde = s.Desde.ToString("yyyy-MM-dd"),
                        hasta = s.Hasta.ToString("yyyy-MM-dd"),
                        subtotal = s.Subtotal,
                        subtotalSinPromo = s.SubtotalSinPromo,
                        promoAplicada = s.PromoAplicada,
                        etiquetaPromo = s.EtiquetaPromo,
                        etiquetaTarifa = s.EtiquetaTarifa,
                        elegibleSalidaAnticipada = s.ElegibleSalidaAnticipada,
                        montoSalidaAnticipada = s.MontoSalidaAnticipada
                    })
                })
            });
        }

        /// <summary>
        /// De los repartos que el servidor ofrece para el par de cabañas, el que eligió el cliente (por las
        /// personas de cada cabaña). Si el cliente no mandó personas, se toma el más parejo. Null si lo que
        /// mandó no coincide con ninguno de los ofrecidos.
        /// </summary>
        private static RepartoEnCabanas? ElegirReparto(List<RepartoEnCabanas> repartos, List<SegmentoInput> segmentos)
        {
            if (segmentos.All(s => s.Adultos == 0 && s.Menores == 0))
            {
                return repartos.FirstOrDefault();
            }

            return repartos.FirstOrDefault(r => segmentos.All(s =>
            {
                var grupo = s.CabanaId == r.Primera.Id ? r.HuespedesPrimera : r.HuespedesSegunda;
                return grupo.Adultos == s.Adultos && grupo.Menores == s.Menores;
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarOpcion(SolicitarOpcionViewModel modelo)
        {
            if (!ModelState.IsValid || modelo.Segmentos.Count == 0)
            {
                return Json(new { exito = false, mensaje = "Faltan datos para completar la solicitud." });
            }

            var huespedes = new Huespedes(modelo.CantidadAdultos, modelo.CantidadMenores);
            if (huespedes.Total > PoliticaPrecios.MaxPersonasPorReserva)
            {
                return Json(new { exito = false, mensaje = MensajeMaximoPersonas });
            }

            var cabanaIds = modelo.Segmentos.Select(s => s.CabanaId).Distinct().ToList();
            var cabanas = await _db.Cabanas.Where(c => cabanaIds.Contains(c.Id) && c.Activa).ToListAsync();

            if (cabanas.Count != cabanaIds.Count)
            {
                return Json(new { exito = false, mensaje = "Una de las cabañas de esta opción ya no está disponible. Volvé a buscar." });
            }

            // Segmentos que coinciden en fechas = el grupo se reparte en dos cabañas a la vez. El reparto
            // se recalcula acá, igual que en la búsqueda, en lugar de confiar en lo que mande el navegador.
            var repartida = modelo.Segmentos.Count > 1 && modelo.Segmentos.Any(a => modelo.Segmentos.Any(b =>
                !ReferenceEquals(a, b) && a.FechaDesde < b.FechaHasta && b.FechaDesde < a.FechaHasta));

            var grupoPorCabana = new Dictionary<int, Huespedes>();
            if (repartida)
            {
                var primero = modelo.Segmentos[0];
                var mismasFechas = modelo.Segmentos.Count == 2 && cabanas.Count == 2
                    && primero.FechaDesde == modelo.Segmentos[1].FechaDesde && primero.FechaHasta == modelo.Segmentos[1].FechaHasta;
                var reparto = mismasFechas && huespedes.Total >= PoliticaPrecios.DosCabanasDesdePersonas
                    ? ElegirReparto(DisponibilidadService.RepartirEnCabanas(cabanas[0], cabanas[1], huespedes), modelo.Segmentos)
                    : null;

                if (reparto is null)
                {
                    return Json(new { exito = false, mensaje = "No podemos repartir al grupo en esas cabañas. Volvé a buscar." });
                }

                grupoPorCabana[reparto.Primera.Id] = reparto.HuespedesPrimera;
                grupoPorCabana[reparto.Segunda.Id] = reparto.HuespedesSegunda;
            }
            else
            {
                foreach (var cabana in cabanas)
                {
                    grupoPorCabana[cabana.Id] = huespedes;
                }
            }

            foreach (var segmento in modelo.Segmentos)
            {
                if (segmento.FechaHasta <= segmento.FechaDesde || segmento.FechaDesde < DateTime.Today)
                {
                    return Json(new { exito = false, mensaje = "Las fechas de esta opción ya no son válidas. Volvé a buscar." });
                }

                var cabana = cabanas.First(c => c.Id == segmento.CabanaId);
                var grupo = grupoPorCabana[cabana.Id];
                if (grupo.Total > cabana.Capacidad)
                {
                    return Json(new { exito = false, mensaje = $"{cabana.Nombre} tiene capacidad para {cabana.Capacidad} personas." });
                }

                if (_disponibilidad.Politica.Resolver(grupo.Adultos, grupo.Menores) is null)
                {
                    return Json(new { exito = false, mensaje = "Tiene que haber al menos un adulto." });
                }

                if (await _disponibilidad.HaySuperposicionAsync(segmento.CabanaId, segmento.FechaDesde, segmento.FechaHasta))
                {
                    return Json(new { exito = false, mensaje = $"{cabana.Nombre} ya no está disponible para esas fechas. Volvé a buscar." });
                }

                if (await _disponibilidad.ValidarFinDeSemanaLargoAsync(segmento.FechaDesde, segmento.FechaHasta) is { } avisoFinLargo)
                {
                    return Json(new { exito = false, mensaje = avisoFinLargo });
                }

                if (await _disponibilidad.ValidarMinimoNochesAsync(segmento.FechaDesde, segmento.FechaHasta) is { } avisoMinimoNoches)
                {
                    return Json(new { exito = false, mensaje = avisoMinimoNoches });
                }
            }

            var reservasCreadas = new List<Reserva>();
            foreach (var segmento in modelo.Segmentos)
            {
                var grupo = grupoPorCabana[segmento.CabanaId];
                var detalle = await _disponibilidad.CalcularValorConDetalleAsync(
                    segmento.CabanaId, segmento.FechaDesde, segmento.FechaHasta, grupo, modelo.SalidaAnticipada);

                var reserva = new Reserva
                {
                    CabanaId = segmento.CabanaId,
                    NombreHuesped = modelo.NombreHuesped,
                    Telefono = modelo.Telefono,
                    Email = modelo.Email,
                    FechaDesde = segmento.FechaDesde,
                    FechaHasta = segmento.FechaHasta,
                    CantidadPersonas = grupo.Total,
                    CantidadMenores = grupo.Menores,
                    Estado = EstadoReserva.Pendiente,
                    Valor = detalle.Total,
                    SalidaAnticipada = detalle.SalidaAnticipadaAplicada
                };
                _db.Reservas.Add(reserva);
                reservasCreadas.Add(reserva);
            }

            await _db.SaveChangesAsync();

            foreach (var reserva in reservasCreadas)
            {
                var cabana = cabanas.First(c => c.Id == reserva.CabanaId);
                await _email.NotificarNuevaSolicitudAsync(cabana, reserva, $"{Request.Scheme}://{Request.Host}");
                if (!string.IsNullOrWhiteSpace(reserva.Email))
                {
                    await _email.NotificarConfirmacionHuespedAsync(cabana, reserva);
                }
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
                nombreHuesped = reservasCreadas[0].NombreHuesped,
                cantidadPersonas = modelo.CantidadPersonas,
                tieneEmail = !string.IsNullOrWhiteSpace(modelo.Email),
                salidaAnticipada = reservasCreadas.Any(r => r.SalidaAnticipada)
            });
        }

        public async Task<IActionResult> Disponibilidad(int? anio, int? mes, DateTime? desde, DateTime? hasta)
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
            ViewBag.FinesDeSemanaLargos = await CargarFinesDeSemanaLargosAsync(primerDia, ultimoDia);
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);
            ViewBag.SeleccionDesde = desde?.ToString("yyyy-MM-dd");
            ViewBag.SeleccionHasta = hasta?.ToString("yyyy-MM-dd");

            return View();
        }

        /// <summary>Los fines de semana largos que se reservan completos y caen en el mes que se está viendo (sin contar los que ya pasaron).</summary>
        private async Task<List<FinDeSemanaLargo>> CargarFinesDeSemanaLargosAsync(DateTime primerDia, DateTime ultimoDia)
        {
            var desde = primerDia > DateTime.Today ? primerDia : DateTime.Today;
            return await _disponibilidad.ObtenerFinesDeSemanaLargosExigidosAsync(desde, ultimoDia.AddDays(1));
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
            ViewBag.FinesDeSemanaLargos = await CargarFinesDeSemanaLargosAsync(primerDia, ultimoDia);
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);
        }
    }
}
