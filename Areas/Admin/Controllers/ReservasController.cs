using System.Security.Cryptography;
using System.Text;
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

        public ReservasController(ApplicationDbContext db, DisponibilidadService disponibilidad, GraphOneDriveService oneDrive, ExcelEscrituraService excelEscritura, IConfiguration config)
        {
            _db = db;
            _disponibilidad = disponibilidad;
            _oneDrive = oneDrive;
            _excelEscritura = excelEscritura;
            _config = config;
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
                sb.Append(t.CabanaId).Append('|').Append(t.Fecha.Ticks).Append('|').Append(t.Precio).Append(';');
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

        public async Task<IActionResult> Index(EstadoReserva? estado, string? busqueda, int? cabanaId, int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var anioActual = anio ?? hoy.Year;
            var mesActual = mes ?? hoy.Month;
            var primerDia = new DateTime(anioActual, mesActual, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var estadoEfectivo = estado ?? EstadoReserva.Confirmada;

            var query = _db.Reservas.Include(r => r.Cabana)
                .Where(r => r.FechaDesde >= primerDia && r.FechaDesde <= ultimoDia)
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

            ViewBag.EstadoFiltro = estadoEfectivo;
            ViewBag.Busqueda = busqueda;
            ViewBag.CabanaIdFiltro = cabanaId;
            ViewBag.Cabanas = await _db.Cabanas.OrderBy(c => c.Nombre).ToListAsync();
            ViewBag.Anio = anioActual;
            ViewBag.Mes = mesActual;
            ViewBag.PrimerDia = primerDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);

            var reservas = await query.OrderBy(r => r.FechaDesde).ThenBy(r => r.Cabana!.Nombre).ToListAsync();
            return View(reservas);
        }

        public async Task<IActionResult> Create(int? cabanaId, DateTime? fecha, DateTime? fechaHasta)
        {
            ViewBag.Cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();

            var fechaDesde = fecha ?? DateTime.Today;
            return View(new Reserva
            {
                CabanaId = cabanaId ?? 0,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta ?? fechaDesde.AddDays(1)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reserva modelo)
        {
            await ValidarFechasAsync(modelo, null);

            if (!ModelState.IsValid)
            {
                ViewBag.Cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();
                return View(modelo);
            }

            modelo.Cabana = null;
            _db.Reservas.Add(modelo);
            await _db.SaveChangesAsync();

            string? avisoExcel = modelo.Estado == EstadoReserva.Confirmada
                ? await _excelEscritura.EscribirReservaAsync(modelo)
                : null;
            TempData["Mensaje"] = avisoExcel is null
                ? "Reserva creada correctamente."
                : $"Reserva creada correctamente. {avisoExcel}";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is null)
            {
                return NotFound();
            }
            ViewBag.Cabanas = await _db.Cabanas.OrderBy(c => c.Nombre).ToListAsync();
            return View(reserva);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reserva modelo)
        {
            if (id != modelo.Id)
            {
                return NotFound();
            }

            await ValidarFechasAsync(modelo, id);

            if (!ModelState.IsValid)
            {
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
            reserva.Estado = modelo.Estado;
            reserva.Pago = modelo.Pago;
            reserva.Valor = modelo.Valor;

            await _db.SaveChangesAsync();

            string? avisoExcel = reserva.Estado == EstadoReserva.Confirmada
                ? await _excelEscritura.EscribirReservaAsync(reserva, anterior)
                : anterior.Estado == EstadoReserva.Confirmada
                    ? await _excelEscritura.LimpiarReservaAsync(reserva)
                    : null;
            TempData["Mensaje"] = avisoExcel is null
                ? "Reserva actualizada correctamente."
                : $"Reserva actualizada correctamente. {avisoExcel}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(int id)
        {
            var reserva = await _db.Reservas.FirstOrDefaultAsync(r => r.Id == id);
            if (reserva is null)
            {
                return NotFound();
            }

            if (await _disponibilidad.HaySuperposicionAsync(reserva.CabanaId, reserva.FechaDesde, reserva.FechaHasta, reserva.Id))
            {
                TempData["Mensaje"] = "No se puede confirmar: esas fechas se superponen con otra reserva ya confirmada.";
                return RedirectToAction(nameof(Index));
            }

            var anterior = new EstadoAnteriorReserva(reserva.CabanaId, reserva.FechaDesde, reserva.FechaHasta, reserva.Estado);

            reserva.Estado = EstadoReserva.Confirmada;
            await _db.SaveChangesAsync();

            var avisoExcel = await _excelEscritura.EscribirReservaAsync(reserva, anterior);
            TempData["Mensaje"] = avisoExcel is null
                ? "Reserva confirmada."
                : $"Reserva confirmada. {avisoExcel}";
            return RedirectToAction(nameof(Index));
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

            if (modelo.Estado == EstadoReserva.Confirmada &&
                await _disponibilidad.HaySuperposicionAsync(modelo.CabanaId, modelo.FechaDesde, modelo.FechaHasta, idExcluir))
            {
                ModelState.AddModelError(string.Empty, "Esas fechas se superponen con otra reserva confirmada para esta cabaña.");
            }
        }
    }
}
