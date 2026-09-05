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

        public ReservasController(ApplicationDbContext db, DisponibilidadService disponibilidad, GraphOneDriveService oneDrive, ExcelEscrituraService excelEscritura)
        {
            _db = db;
            _disponibilidad = disponibilidad;
            _oneDrive = oneDrive;
            _excelEscritura = excelEscritura;
        }

        public async Task<IActionResult> Index(EstadoReserva? estado, string? busqueda, int? cabanaId, int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var anioActual = anio ?? hoy.Year;
            var mesActual = mes ?? hoy.Month;
            var primerDia = new DateTime(anioActual, mesActual, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var query = _db.Reservas.Include(r => r.Cabana)
                .Where(r => r.FechaDesde >= primerDia && r.FechaDesde <= ultimoDia)
                .AsQueryable();
            if (estado.HasValue)
            {
                query = query.Where(r => r.Estado == estado.Value);
            }
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                query = query.Where(r => EF.Functions.Like(r.NombreHuesped, $"%{busqueda}%"));
            }
            if (cabanaId.HasValue)
            {
                query = query.Where(r => r.CabanaId == cabanaId.Value);
            }

            ViewBag.EstadoFiltro = estado;
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

        public async Task<IActionResult> Create(int? cabanaId, DateTime? fecha)
        {
            ViewBag.Cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();

            var fechaDesde = fecha ?? DateTime.Today;
            return View(new Reserva
            {
                CabanaId = cabanaId ?? 0,
                FechaDesde = fechaDesde,
                FechaHasta = fechaDesde.AddDays(1)
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

            var avisoExcel = await _excelEscritura.EscribirReservaAsync(modelo);
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

            reserva.CabanaId = modelo.CabanaId;
            reserva.NombreHuesped = modelo.NombreHuesped;
            reserva.Telefono = modelo.Telefono;
            reserva.FechaDesde = modelo.FechaDesde;
            reserva.FechaHasta = modelo.FechaHasta;
            reserva.Estado = modelo.Estado;
            reserva.Valor = modelo.Valor;

            await _db.SaveChangesAsync();

            var avisoExcel = await _excelEscritura.EscribirReservaAsync(reserva);
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

            reserva.Estado = EstadoReserva.Confirmada;
            await _db.SaveChangesAsync();

            var avisoExcel = await _excelEscritura.EscribirReservaAsync(reserva);
            TempData["Mensaje"] = avisoExcel is null
                ? "Reserva confirmada."
                : $"Reserva confirmada. {avisoExcel}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
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
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarBloqueo(int cabanaId, DateTime fecha, int? anio, int? mes)
        {
            var existente = await _db.TarifasDias.FirstOrDefaultAsync(t => t.CabanaId == cabanaId && t.Fecha.Date == fecha.Date);

            if (existente is not null && existente.Bloqueada)
            {
                if (existente.Precio.HasValue)
                {
                    existente.Bloqueada = false;
                }
                else
                {
                    _db.TarifasDias.Remove(existente);
                }
                TempData["Mensaje"] = "Día desbloqueado.";
            }
            else if (existente is not null)
            {
                existente.Bloqueada = true;
                TempData["Mensaje"] = "Día bloqueado.";
            }
            else
            {
                _db.TarifasDias.Add(new TarifaDia { CabanaId = cabanaId, Fecha = fecha.Date, Bloqueada = true });
                TempData["Mensaje"] = "Día bloqueado.";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Calendario), new { anio, mes });
        }

        public async Task<IActionResult> Calendario(int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();
            var reservas = await _db.Reservas
                .Where(r => r.FechaDesde <= ultimoDia && r.FechaHasta >= primerDia)
                .ToListAsync();
            var tarifas = await _disponibilidad.ObtenerTarifasEnRangoTodasCabanasAsync(primerDia, ultimoDia);

            ViewBag.Cabanas = cabanas;
            ViewBag.Reservas = reservas;
            ViewBag.Tarifas = tarifas;
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);

            ViewBag.SincronizacionConfigurada = _oneDrive.EstaConfigurado;
            ViewBag.SincronizacionConexion = await _oneDrive.ObtenerConexionAsync();

            return View();
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
