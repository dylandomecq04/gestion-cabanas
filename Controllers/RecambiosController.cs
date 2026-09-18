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

        private bool TokenValido(string token)
        {
            var tokenConfigurado = _config["Recambios:Token"];
            return !string.IsNullOrEmpty(tokenConfigurado) && token == tokenConfigurado;
        }

        // Vista principal: un día a la vez, con las cabañas donde se va gente ese día.
        [HttpGet("{token}")]
        public async Task<IActionResult> Index(string token, DateTime? fecha)
        {
            if (!TokenValido(token))
            {
                return NotFound();
            }

            var dia = (fecha ?? DateTime.Today).Date;

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Id).ToListAsync();
            var diaSiguiente = dia.AddDays(1);
            var salidas = await _db.Reservas
                .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaHasta >= dia && r.FechaHasta < diaSiguiente)
                .ToListAsync();

            ViewBag.Token = token;
            ViewBag.Cabanas = cabanas;
            ViewBag.Dia = dia;
            ViewBag.Salidas = salidas;

            return View();
        }

        // Detalle de una cabaña: calendario del mes con quién entra y quién sale cada día.
        [HttpGet("{token}/cabana/{id:int}")]
        public async Task<IActionResult> Cabana(string token, int id, int? anio, int? mes)
        {
            if (!TokenValido(token))
            {
                return NotFound();
            }

            var cabanas = await _db.Cabanas.Where(c => c.Activa).OrderBy(c => c.Id).ToListAsync();
            var cabana = cabanas.FirstOrDefault(c => c.Id == id);
            if (cabana is null)
            {
                return NotFound();
            }

            var hoy = DateTime.Today;
            var primerDia = new DateTime(anio ?? hoy.Year, mes ?? hoy.Month, 1);
            var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

            var mesSiguiente = primerDia.AddMonths(1);
            var reservas = await _db.Reservas
                .Where(r => r.CabanaId == id && r.Estado == EstadoReserva.Confirmada
                    && ((r.FechaHasta >= primerDia && r.FechaHasta < mesSiguiente)
                        || (r.FechaDesde >= primerDia && r.FechaDesde < mesSiguiente)))
                .ToListAsync();

            ViewBag.Token = token;
            ViewBag.Cabanas = cabanas;
            ViewBag.Cabana = cabana;
            ViewBag.Reservas = reservas;
            ViewBag.PrimerDia = primerDia;
            ViewBag.UltimoDia = ultimoDia;
            ViewBag.MesAnterior = primerDia.AddMonths(-1);
            ViewBag.MesSiguiente = mesSiguiente;

            return View();
        }
    }
}
