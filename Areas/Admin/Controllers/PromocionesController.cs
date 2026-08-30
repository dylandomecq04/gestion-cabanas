using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class PromocionesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public PromocionesController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Izquierda = await ObtenerOCrearAsync(LadoPromocion.Izquierda);
            ViewBag.Derecha = await ObtenerOCrearAsync(LadoPromocion.Derecha);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(Promocion izquierda, Promocion derecha)
        {
            await GuardarLadoAsync(LadoPromocion.Izquierda, izquierda);
            await GuardarLadoAsync(LadoPromocion.Derecha, derecha);

            TempData["Mensaje"] = "Promociones actualizadas correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<Promocion> ObtenerOCrearAsync(LadoPromocion lado)
        {
            var promo = await _db.Promociones.FirstOrDefaultAsync(p => p.Lado == lado);
            if (promo is null)
            {
                promo = new Promocion
                {
                    Lado = lado,
                    Activa = false,
                    Etiqueta = "OFERTA",
                    Titulo = lado == LadoPromocion.Izquierda ? "3 noches, la 3ra bonificada" : "20% OFF de domingo a jueves",
                    Descripcion = lado == LadoPromocion.Izquierda
                        ? "Reservando 3 noches o más, la última corre por nuestra cuenta."
                        : "Descuento especial reservando entre semana. Consultanos las fechas."
                };
                _db.Promociones.Add(promo);
                await _db.SaveChangesAsync();
            }
            return promo;
        }

        private async Task GuardarLadoAsync(LadoPromocion lado, Promocion modelo)
        {
            var promo = await ObtenerOCrearAsync(lado);
            promo.Activa = modelo.Activa;
            promo.Etiqueta = modelo.Etiqueta;
            promo.Titulo = modelo.Titulo;
            promo.Descripcion = modelo.Descripcion;
            await _db.SaveChangesAsync();
        }
    }
}
