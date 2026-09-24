using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    public class ReservasController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly DisponibilidadService _disponibilidad;
        private readonly GraphOneDriveService _oneDrive;
        private readonly ExcelEscrituraService _excelEscritura;
        private readonly IConfiguration _config;
        private readonly INotificacionEmailService _email;

        public ReservasController(ApplicationDbContext db, DisponibilidadService disponibilidad, GraphOneDriveService oneDrive, ExcelEscrituraService excelEscritura, IConfiguration config, INotificacionEmailService email)
        {
            _db = db;
            _disponibilidad = disponibilidad;
            _oneDrive = oneDrive;
            _excelEscritura = excelEscritura;
            _config = config;
            _email = email;
        }

        private async Task NotificarConfirmacionAsync(Reserva reserva)
        {
            if (string.IsNullOrWhiteSpace(reserva.Email))
            {
                return;
            }

            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == reserva.CabanaId);
            if (cabana is not null)
            {
                await _email.NotificarReservaConfirmadaAsync(cabana, reserva);
            }
        }

        private static string CalcularFirmaCalendario(List<Reserva> reservas, List<TarifaDia> tarifas, List<PromoEstadia> promos)
        {
            var sb = new StringBuilder();
            foreach (var r in reservas.OrderBy(r => r.Id))
            {
                sb.Append(r.Id).Append('|').Append(r.CabanaId).Append('|').Append(r.Estado).Append('|')
                  .Append(r.FechaDesde.Ticks).Append('|').Append(r.FechaHasta.Ticks).Append('|')
                  .Append(r.NombreHuesped).Append('|').Append(r.Pago).Append('|').Append(r.Valor).Append(';');
            }
            foreach (var t in tarifas.OrderBy(t => t.CabanaId).ThenBy(t => t.Fecha))
            {
                sb.Append(t.CabanaId).Append('|').Append(t.Fecha.Ticks).Append('|').Append(t.Precio2).Append('|').Append(t.Precio4).Append('|').Append(t.Precio6).Append(';');
            }
            foreach (var p in promos.OrderBy(p => p.Id))
            {
                sb.Append(p.Id).Append('|').Append(p.CabanaId).Append('|').Append(p.FechaDesde.Ticks).Append('|').Append(p.FechaHasta.Ticks).Append('|')
                  .Append(p.Precio1Noche).Append('|').Append(p.Precio2Noches).Append('|').Append(p.Precio3Noches).Append('|').Append(p.Activa).Append(';');
            }

            var hash = MD5.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash);
        }

        private async Task<DateTime?> ObtenerFechaModificacionExcelSilenciosaAsync()
        {
            var urlArchivo = _config["OneDrive:ArchivoUrl"];
            if (string.IsNullOrWhiteSpace(urlArchivo))
            {
                return null;
            }

            try
            {
                return await _oneDrive.ObtenerFechaModificacionAsync(urlArchivo);
            }
            catch
            {
                // Si falla la consulta a Graph (token vencido, etc.) simplemente no se detecta cambio en el Excel esta vez.
                return null;
            }
        }

        public async Task<IActionResult> EstadoCalendario(int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var reservas = await _db.Reservas
                .Where(r => r.FechaDesde <= ultimoDia && r.FechaHasta >= primerDia)
                .ToListAsync();
            var tarifas = await _disponibilidad.ObtenerTarifasEnRangoTodasCabanasAsync(primerDia, ultimoDia);
            var promos = await _disponibilidad.ObtenerPromosEnRangoTodasCabanasAsync(primerDia, ultimoDia);

            var firma = CalcularFirmaCalendario(reservas, tarifas, promos);

            var conexion = await _oneDrive.ObtenerConexionAsync();
            var excelModificado = conexion?.RefreshTokenCifrado is not null
                ? await ObtenerFechaModificacionExcelSilenciosaAsync()
                : null;

            return Json(new { firma, excelModificado });
        }

        public async Task<IActionResult> Index(EstadoReserva? estado, string? busqueda, int? cabanaId, int? anio, int? mes, bool? contactado, string? colorContacto, string? orden, bool desc = false)
        {
            var estadoEfectivo = estado ?? EstadoReserva.Confirmada;
            var esSolicitudes = estadoEfectivo == EstadoReserva.Pendiente;

            ConfiguracionSolicitudes? configSolicitudes = null;
            if (esSolicitudes)
            {
                configSolicitudes = await _db.ConfiguracionesSolicitudes.FirstOrDefaultAsync();
                if (configSolicitudes is null)
                {
                    configSolicitudes = new ConfiguracionSolicitudes();
                    _db.ConfiguracionesSolicitudes.Add(configSolicitudes);
                    await _db.SaveChangesAsync();
                }
            }

            var query = _db.Reservas.Include(r => r.Cabana)
                .Where(r => r.Estado == estadoEfectivo)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                query = query.Where(r => EF.Functions.Like(r.NombreHuesped, $"%{busqueda}%"));
            }
            if (cabanaId.HasValue)
            {
                query = query.Where(r => r.CabanaId == cabanaId.Value);
            }
            if (esSolicitudes && contactado.HasValue)
            {
                query = query.Where(r => r.Contactado == contactado.Value);
            }
            if (esSolicitudes && !string.IsNullOrWhiteSpace(colorContacto) && configSolicitudes is not null)
            {
                var ahora = DateTime.Now;
                var corteAmarillo = ahora.AddHours(-configSolicitudes.HorasAmarillo);
                var corteRojo = ahora.AddHours(-configSolicitudes.HorasRojo);
                query = colorContacto switch
                {
                    "amarillo" => query.Where(r => r.FechaContactado.HasValue && r.FechaContactado.Value <= corteAmarillo && r.FechaContactado.Value > corteRojo),
                    "rojo" => query.Where(r => r.FechaContactado.HasValue && r.FechaContactado.Value <= corteRojo),
                    _ => query
                };
            }

            // Las solicitudes se listan todas por defecto (más vieja primero); el mes es un filtro
            // opcional. Las reservas confirmadas siguen mostrándose mes a mes, como antes.
            DateTime? primerDia = null;
            if (esSolicitudes)
            {
                if (anio.HasValue && mes.HasValue)
                {
                    primerDia = new DateTime(anio.Value, mes.Value, 1);
                }
            }
            else
            {
                var hoy = DateTime.Today;
                primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            }
            if (primerDia.HasValue)
            {
                var ultimoDia = primerDia.Value.AddMonths(1).AddDays(-1);
                query = query.Where(r => r.FechaDesde >= primerDia && r.FechaDesde <= ultimoDia);
            }

            ViewBag.EstadoFiltro = estadoEfectivo;
            ViewBag.Busqueda = busqueda;
            ViewBag.CabanaIdFiltro = cabanaId;
            ViewBag.ContactadoFiltro = contactado;
            ViewBag.ColorContactoFiltro = colorContacto;
            ViewBag.HorasAmarillo = configSolicitudes?.HorasAmarillo ?? 24;
            ViewBag.HorasRojo = configSolicitudes?.HorasRojo ?? 48;
            ViewBag.Orden = orden;
            ViewBag.OrdenDesc = desc;
            ViewBag.Cabanas = await _db.Cabanas.OrderBy(c => c.Nombre).ToListAsync();
            ViewBag.Anio = primerDia?.Year;
            ViewBag.Mes = primerDia?.Month;
            ViewBag.PrimerDia = primerDia;
            ViewBag.MesAnterior = primerDia?.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia?.AddMonths(1);

            if (esSolicitudes)
            {
                var fechasSolicitudes = await _db.Reservas
                    .Where(r => r.Estado == EstadoReserva.Pendiente)
                    .Select(r => r.FechaDesde)
                    .ToListAsync();
                var meses = fechasSolicitudes.Select(f => new DateTime(f.Year, f.Month, 1)).Distinct();
                if (primerDia.HasValue)
                {
                    meses = meses.Append(primerDia.Value).Distinct();
                }
                ViewBag.MesesConSolicitudes = meses.OrderBy(m => m).ToList();
            }

            // En Solicitudes se puede reordenar clickeando cualquier columna; por defecto queda la
            // más nueva primero. Clickear de nuevo la misma columna invierte el orden (A-Z / Z-A).
            // En Reservas confirmadas el orden no cambia porque siguen agrupadas por cabaña en la vista.
            var querySolicitudes = orden switch
            {
                "cabana" => desc ? query.OrderByDescending(r => r.Cabana!.Nombre) : query.OrderBy(r => r.Cabana!.Nombre),
                "huesped" => desc ? query.OrderByDescending(r => r.NombreHuesped) : query.OrderBy(r => r.NombreHuesped),
                "fecha" => desc ? query.OrderByDescending(r => r.FechaDesde) : query.OrderBy(r => r.FechaDesde),
                "personas" => desc ? query.OrderByDescending(r => r.CantidadPersonas) : query.OrderBy(r => r.CantidadPersonas),
                "pago" => desc ? query.OrderByDescending(r => r.Pago) : query.OrderBy(r => r.Pago),
                "pagar" => desc ? query.OrderByDescending(r => r.Valor) : query.OrderBy(r => r.Valor),
                "contacto" => desc ? query.OrderByDescending(r => r.Telefono) : query.OrderBy(r => r.Telefono),
                "creada" => desc ? query.OrderByDescending(r => r.FechaCreacion) : query.OrderBy(r => r.FechaCreacion),
                "contactado" => desc ? query.OrderByDescending(r => r.Contactado) : query.OrderBy(r => r.Contactado),
                "fechacontactado" => desc ? query.OrderByDescending(r => r.FechaContactado) : query.OrderBy(r => r.FechaContactado),
                _ => query.OrderByDescending(r => r.FechaCreacion)
            };

            var reservas = esSolicitudes
                ? await querySolicitudes.ToListAsync()
                : await query.OrderBy(r => r.FechaDesde).ThenBy(r => r.Cabana!.Nombre).ToListAsync();
            return View(reservas);
        }

        /// <summary>
        /// Recuerda desde qué vista se llegó (por ahora sólo "Calendario" del mes en pantalla) para
        /// poder volver ahí después de guardar, en vez de mandar siempre al listado.
        /// </summary>
        private void GuardarOrigen(string? vista, int? anio, int? mes)
        {
            ViewBag.Vista = vista;
            ViewBag.Anio = anio;
            ViewBag.Mes = mes;
        }

        private IActionResult VolverAlOrigen(string? vista, int? anio, int? mes, EstadoReserva? estadoListado = null)
        {
            if (vista == "Calendario" && anio.HasValue && mes.HasValue)
            {
                return RedirectToAction(nameof(Calendario), new { anio, mes });
            }
            return estadoListado.HasValue
                ? RedirectToAction(nameof(Index), new { estado = estadoListado })
                : RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Cabañas activas sin reservas confirmadas que se superpongan con el rango dado.
        /// </summary>
        private async Task<List<Cabana>> ObtenerCabanasLibresAsync(DateTime desde, DateTime hasta, int? idExcluir = null)
        {
            var libres = new List<Cabana>();
            if (hasta <= desde)
            {
                return libres;
            }

            var activas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();
            foreach (var cabana in activas)
            {
                if (!await _disponibilidad.HaySuperposicionAsync(cabana.Id, desde, hasta, idExcluir))
                {
                    libres.Add(cabana);
                }
            }
            return libres;
        }

        /// <summary>
        /// Lo usa el formulario de carga para mostrar, al cambiar las fechas, qué cabañas están libres.
        /// </summary>
        public async Task<IActionResult> CabanasDisponibles(DateTime desde, DateTime hasta, int? idExcluir)
        {
            var libres = await ObtenerCabanasLibresAsync(desde.Date, hasta.Date, idExcluir);
            return Json(libres.Select(c => new { id = c.Id, nombre = c.Nombre }));
        }

        /// <summary>
        /// Lo usa el formulario de carga para sugerir Pagó / Pagar: el valor total de la estadía
        /// según las tarifas cargadas, o null si no se puede calcular (sin tarifa, sin adultos, etc.).
        /// </summary>
        public async Task<IActionResult> TotalSugerido(int cabanaId, DateTime desde, DateTime hasta, int personas, int menores)
        {
            var adultos = personas - menores;
            if (adultos < 1 || menores < 0 || hasta.Date <= desde.Date)
            {
                return Json(new { total = (decimal?)null });
            }

            var total = await _disponibilidad.CalcularValorTotalAsync(cabanaId, desde.Date, hasta.Date, new Huespedes(adultos, menores));
            return Json(new { total });
        }

        public async Task<IActionResult> Create(int? cabanaId, DateTime? fecha, DateTime? fechaHasta, string? vista, int? anio, int? mes, EstadoReserva? estado)
        {
            GuardarOrigen(vista, anio, mes);
            ViewBag.Cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();

            var fechaDesde = fecha ?? DateTime.Today;
            var modelo = new Reserva
            {
                CabanaId = cabanaId ?? 0,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta ?? fechaDesde.AddDays(1),
                Estado = estado ?? EstadoReserva.Confirmada
            };
            ViewBag.Libres = await ObtenerCabanasLibresAsync(modelo.FechaDesde, modelo.FechaHasta);
            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reserva modelo, string? vista, int? anio, int? mes)
        {
            await ValidarFechasAsync(modelo, null);

            if (!ModelState.IsValid)
            {
                GuardarOrigen(vista, anio, mes);
                ViewBag.Cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();
                ViewBag.Libres = await ObtenerCabanasLibresAsync(modelo.FechaDesde, modelo.FechaHasta);
                return View(modelo);
            }

            modelo.Cabana = null;
            _db.Reservas.Add(modelo);
            await _db.SaveChangesAsync();

            string? avisoExcel = modelo.Estado == EstadoReserva.Confirmada
                ? await _excelEscritura.EscribirReservaAsync(modelo)
                : null;
            var creada = modelo.Estado == EstadoReserva.Pendiente ? "Solicitud creada" : "Reserva creada";
            TempData["Mensaje"] = avisoExcel is null
                ? $"{creada} correctamente."
                : $"{creada} correctamente. {avisoExcel}";
            return VolverAlOrigen(vista, anio, mes, modelo.Estado);
        }

        public async Task<IActionResult> Edit(int id, string? vista, int? anio, int? mes)
        {
            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is null)
            {
                return NotFound();
            }
            GuardarOrigen(vista, anio, mes);
            ViewBag.Cabanas = await _db.Cabanas.OrderBy(c => c.Nombre).ToListAsync();
            return View(reserva);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reserva modelo, string? vista, int? anio, int? mes)
        {
            if (id != modelo.Id)
            {
                return NotFound();
            }

            await ValidarFechasAsync(modelo, id);

            if (!ModelState.IsValid)
            {
                GuardarOrigen(vista, anio, mes);
                ViewBag.Cabanas = await _db.Cabanas.OrderBy(c => c.Nombre).ToListAsync();
                return View(modelo);
            }

            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is null)
            {
                return NotFound();
            }

            var anterior = new EstadoAnteriorReserva(reserva.CabanaId, reserva.FechaDesde, reserva.FechaHasta, reserva.Estado);

            reserva.CabanaId = modelo.CabanaId;
            reserva.NombreHuesped = modelo.NombreHuesped;
            reserva.Telefono = modelo.Telefono;
            reserva.FechaDesde = modelo.FechaDesde;
            reserva.FechaHasta = modelo.FechaHasta;
            reserva.CantidadPersonas = modelo.CantidadPersonas;
            reserva.CantidadMenores = modelo.CantidadMenores;
            reserva.Estado = modelo.Estado;
            reserva.Pago = modelo.Pago;
            reserva.Valor = modelo.Valor;

            await _db.SaveChangesAsync();

            string? avisoExcel = reserva.Estado == EstadoReserva.Confirmada
                ? await _excelEscritura.EscribirReservaAsync(reserva, anterior)
                : anterior.Estado == EstadoReserva.Confirmada
                    ? await _excelEscritura.LimpiarReservaAsync(reserva, anterior.CabanaId)
                    : null;
            if (reserva.Estado == EstadoReserva.Confirmada && anterior.Estado != EstadoReserva.Confirmada)
            {
                await NotificarConfirmacionAsync(reserva);
            }

            TempData["Mensaje"] = avisoExcel is null
                ? "Reserva actualizada correctamente."
                : $"Reserva actualizada correctamente. {avisoExcel}";
            return VolverAlOrigen(vista, anio, mes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(int id, string? vista, int? anio, int? mes)
        {
            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is null)
            {
                return NotFound();
            }

            if (await _disponibilidad.HaySuperposicionAsync(reserva.CabanaId, reserva.FechaDesde, reserva.FechaHasta, reserva.Id))
            {
                TempData["Alerta"] = "No se puede confirmar: esas fechas se superponen con otra reserva ya confirmada. La reserva NO fue confirmada.";
                return VolverAlOrigen(vista, anio, mes);
            }

            var anterior = new EstadoAnteriorReserva(reserva.CabanaId, reserva.FechaDesde, reserva.FechaHasta, reserva.Estado);

            reserva.Estado = EstadoReserva.Confirmada;
            await _db.SaveChangesAsync();

            var avisoExcel = await _excelEscritura.EscribirReservaAsync(reserva, anterior);
            await NotificarConfirmacionAsync(reserva);
            TempData["Mensaje"] = avisoExcel is null
                ? "Reserva confirmada."
                : $"Reserva confirmada. {avisoExcel}";

            var conflictos = await ObtenerConflictosAsync(reserva);
            if (conflictos.Count > 0)
            {
                TempData["ConflictosJson"] = JsonSerializer.Serialize(conflictos);
            }

            return VolverAlOrigen(vista, anio, mes);
        }

        /// <summary>
        /// Busca, entre las solicitudes pendientes de la misma cabaña que se superponen con las
        /// fechas recién confirmadas, cuáles quedaron en conflicto y qué cabañas alternativas
        /// (con su precio) están libres para ofrecer como reemplazo.
        /// </summary>
        private async Task<List<ConflictoSolicitud>> ObtenerConflictosAsync(Reserva reservaConfirmada)
        {
            var solicitudesEnConflicto = await _db.Reservas
                .Where(r => r.Id != reservaConfirmada.Id &&
                            r.CabanaId == reservaConfirmada.CabanaId &&
                            r.Estado == EstadoReserva.Pendiente &&
                            r.FechaDesde < reservaConfirmada.FechaHasta &&
                            r.FechaHasta > reservaConfirmada.FechaDesde)
                .OrderBy(r => r.FechaDesde)
                .ToListAsync();

            var conflictos = new List<ConflictoSolicitud>();
            foreach (var solicitud in solicitudesEnConflicto)
            {
                var alternativas = await _disponibilidad.ObtenerCabanasAlternativasAsync(
                    solicitud.FechaDesde, solicitud.FechaHasta, solicitud.CabanaId,
                    new Huespedes(solicitud.CantidadAdultos, solicitud.CantidadMenores));

                conflictos.Add(new ConflictoSolicitud
                {
                    ReservaId = solicitud.Id,
                    NombreHuesped = solicitud.NombreHuesped,
                    FechaDesde = solicitud.FechaDesde,
                    FechaHasta = solicitud.FechaHasta,
                    Valor = solicitud.Valor,
                    Alternativas = alternativas
                });
            }

            return conflictos;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleContactado(int id, bool contactado)
        {
            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is null)
            {
                return NotFound();
            }

            reserva.Contactado = contactado;
            reserva.FechaContactado = contactado ? DateTime.Now : null;
            await _db.SaveChangesAsync();

            return Json(new { ok = true, contactado = reserva.Contactado, fechaContactado = reserva.FechaContactado });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarConfiguracionSolicitudes(int horasAmarillo, int horasRojo, string? busqueda, int? cabanaId, int? anio, int? mes, bool? contactado, string? colorContacto, string? orden, bool desc = false)
        {
            if (horasAmarillo < 1) horasAmarillo = 1;
            if (horasRojo < 1) horasRojo = 1;

            var config = await _db.ConfiguracionesSolicitudes.FirstOrDefaultAsync();
            if (config is null)
            {
                config = new ConfiguracionSolicitudes();
                _db.ConfiguracionesSolicitudes.Add(config);
            }
            config.HorasAmarillo = horasAmarillo;
            config.HorasRojo = horasRojo;
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = "Configuración de colores actualizada.";
            return RedirectToAction(nameof(Index), new { estado = EstadoReserva.Pendiente, busqueda, cabanaId, anio, mes, contactado, colorContacto, orden, desc });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoverCabana(int id, int nuevaCabanaId, string? vista, int? anio, int? mes)
        {
            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is null)
            {
                return NotFound();
            }

            if (await _disponibilidad.HaySuperposicionAsync(nuevaCabanaId, reserva.FechaDesde, reserva.FechaHasta))
            {
                TempData["Alerta"] = "No se puede mover: esas fechas no están disponibles en la otra cabaña. La solicitud NO fue movida.";
                return VolverAlOrigen(vista, anio, mes, EstadoReserva.Pendiente);
            }

            reserva.CabanaId = nuevaCabanaId;
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = "Solicitud movida a otra cabaña.";
            return VolverAlOrigen(vista, anio, mes, EstadoReserva.Pendiente);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? anio, int? mes, string? vista, EstadoReserva? estado, string? busqueda, int? cabanaId)
        {
            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is not null)
            {
                var avisoExcel = await _excelEscritura.LimpiarReservaAsync(reserva);
                _db.Reservas.Remove(reserva);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = avisoExcel is null
                    ? "Reserva eliminada."
                    : $"Reserva eliminada. {avisoExcel}";
            }

            if (vista == "Lista")
            {
                return RedirectToAction(nameof(Index), new { estado, busqueda, cabanaId, anio, mes });
            }
            if (anio.HasValue && mes.HasValue)
            {
                return RedirectToAction(nameof(Calendario), new { anio, mes });
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Calendario(int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Id).ToListAsync();
            var reservas = await _db.Reservas
                .Where(r => r.FechaDesde <= ultimoDia && r.FechaHasta >= primerDia)
                .ToListAsync();
            var tarifas = await _disponibilidad.ObtenerTarifasEnRangoTodasCabanasAsync(primerDia, ultimoDia);
            var promos = await _disponibilidad.ObtenerPromosEnRangoTodasCabanasAsync(primerDia, ultimoDia);

            ViewBag.Cabanas = cabanas;
            ViewBag.Reservas = reservas;
            ViewBag.Tarifas = tarifas;
            ViewBag.Promos = promos;
            ViewBag.FinesDeSemanaLargos = await _disponibilidad.ObtenerFinesDeSemanaLargosExigidosAsync(primerDia, ultimoDia.AddDays(1));
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);

            ViewBag.SincronizacionConfigurada = _oneDrive.EstaConfigurado;
            var conexion = await _oneDrive.ObtenerConexionAsync();
            ViewBag.SincronizacionConexion = conexion;

            ViewBag.FirmaCalendario = CalcularFirmaCalendario(reservas, tarifas, promos);
            ViewBag.ExcelModificado = conexion?.RefreshTokenCifrado is not null
                ? await ObtenerFechaModificacionExcelSilenciosaAsync()
                : null;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPromoEstadia(PromoEstadiaMultipleInput modelo, int? anio, int? mes)
        {
            if (modelo.CabanaIds is null || modelo.CabanaIds.Count == 0)
            {
                TempData["Mensaje"] = "Elegí al menos una cabaña para la promoción.";
                return RedirectToAction(nameof(Calendario), new { anio, mes });
            }

            if (modelo.FechaHasta.Date < modelo.FechaDesde.Date)
            {
                TempData["Mensaje"] = "El rango de fechas de la promoción no es válido.";
                return RedirectToAction(nameof(Calendario), new { anio, mes });
            }

            if (!modelo.Precio1Noche.HasValue && !modelo.Precio2Noches.HasValue && !modelo.Precio3Noches.HasValue)
            {
                TempData["Mensaje"] = "Cargá al menos un precio (1, 2 o 3 noches) para la promoción.";
                return RedirectToAction(nameof(Calendario), new { anio, mes });
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
            return RedirectToAction(nameof(Calendario), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPromoCalendario(List<int> ids, int? anio, int? mes)
        {
            if (ids is not null && ids.Count > 0)
            {
                var promosAEliminar = await _db.PromosEstadia.Where(p => ids.Contains(p.Id)).ToListAsync();
                _db.PromosEstadia.RemoveRange(promosAEliminar);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = promosAEliminar.Count > 1
                    ? $"{promosAEliminar.Count} promociones eliminadas."
                    : "Promoción eliminada.";
            }
            return RedirectToAction(nameof(Calendario), new { anio, mes });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarTarifasCalendario(int anio, int mes, List<TarifaDiaInput> dias)
        {
            await _disponibilidad.GuardarTarifasAsync(dias ?? new List<TarifaDiaInput>());
            TempData["Mensaje"] = "Cambios guardados.";
            return RedirectToAction(nameof(Calendario), new { anio, mes });
        }

        private async Task ValidarFechasAsync(Reserva modelo, int? idExcluir)
        {
            ModelState.Remove(nameof(Reserva.Cabana));

            if (modelo.FechaHasta <= modelo.FechaDesde)
            {
                ModelState.AddModelError(nameof(Reserva.FechaHasta), "La fecha de salida debe ser posterior a la de entrada");
                return;
            }

            if (modelo.CantidadMenores >= modelo.CantidadPersonas)
            {
                ModelState.AddModelError(nameof(Reserva.CantidadMenores), "Tiene que haber al menos un adulto: los menores ya están incluidos en la cantidad de personas.");
            }

            // Tanto una reserva confirmada como una solicitud necesitan que esos días estén libres de
            // reservas confirmadas (las solicitudes entre sí sí pueden superponerse).
            if (await _disponibilidad.HaySuperposicionAsync(modelo.CabanaId, modelo.FechaDesde, modelo.FechaHasta, idExcluir))
            {
                ModelState.AddModelError(string.Empty, "Esas fechas se superponen con otra reserva confirmada para esta cabaña.");
            }
        }
    }
}
