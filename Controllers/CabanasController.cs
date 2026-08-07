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
        private readonly INotificacionWhatsAppService _whatsApp;
        private readonly INotificacionEmailService _email;

        public CabanasController(ApplicationDbContext db, DisponibilidadService disponibilidad, INotificacionWhatsAppService whatsApp, INotificacionEmailService email)
        {
            _db = db;
            _disponibilidad = disponibilidad;
            _whatsApp = whatsApp;
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
            else if (await _disponibilidad.HaySuperposicionAsync(modelo.CabanaId, modelo.FechaDesde, modelo.FechaHasta, incluirPendientes: true))
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
                Email = modelo.Email,
                FechaDesde = modelo.FechaDesde,
                FechaHasta = modelo.FechaHasta,
                CantidadPersonas = modelo.CantidadPersonas,
                Notas = modelo.Notas,
                Estado = EstadoReserva.Pendiente
            };
            _db.Reservas.Add(reserva);
            await _db.SaveChangesAsync();

            await _whatsApp.NotificarNuevaSolicitudAsync(cabana, reserva);
            await _email.NotificarNuevaSolicitudAsync(cabana, reserva);

            TempData["SolicitudEnviada"] = $"¡Listo! Recibimos tu solicitud para {cabana.Nombre}.";
            TempData["ReservaId"] = reserva.Id;
            return RedirectToAction(nameof(Details), new { id = modelo.CabanaId });
        }

        public async Task<IActionResult> Disponibilidad(int? anio, int? mes)
        {
            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync();
            var reservas = await _disponibilidad.ObtenerConfirmadasEnRangoAsync(primerDia, ultimoDia, incluirPendientes: true);
            var bloqueadas = await _disponibilidad.ObtenerBloqueadasEnRangoAsync(primerDia, ultimoDia);

            ViewBag.Cabanas = cabanas;
            ViewBag.Reservas = reservas;
            ViewBag.Bloqueadas = bloqueadas;
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

            ViewBag.Reservas = await _disponibilidad.ObtenerConfirmadasEnRangoAsync(primerDia, ultimoDia, cabanaId, incluirPendientes: true);
            ViewBag.Bloqueadas = await _disponibilidad.ObtenerBloqueadasEnRangoAsync(primerDia, ultimoDia, cabanaId);
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);
            ViewBag.PermitirMesAnterior = primerDia > new DateTime(hoy.Year, hoy.Month, 1);
        }
    }
}
