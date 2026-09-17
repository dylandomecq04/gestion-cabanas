using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Controllers
{
    // Sin [Authorize]: pensado para compartir por link directo con el personal de limpieza,
    // sin que tengan que entrar al panel de administración. El único control de acceso es
    // que el token de la URL coincida con el configurado (si no, no existe la página).
    [Route("Recambios")]
    public class RecambiosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public RecambiosController(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpGet("{token}")]
        public async Task<IActionResult> Index(string token, int? anio, int? mes)
        {
            var tokenConfigurado = _config["Recambios:Token"];
            if (string.IsNullOrEmpty(tokenConfigurado) || token != tokenConfigurado)
            {
                return NotFound();
            }

            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Id).ToListAsync();
            var reservas = await _db.Reservas
                .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaDesde <= ultimoDia && r.FechaHasta >= primerDia)
                .ToListAsync();

            ViewBag.Token = token;
            ViewBag.Cabanas = cabanas;
            ViewBag.Reservas = reservas;
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = primerDia.AddMonths(1);

            return View();
        }
    }
}
