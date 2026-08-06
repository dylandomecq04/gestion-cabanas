using GestionCabanas.Data;
using Microsoft.AspNetCore.Mvc;

namespace GestionCabanas.ViewComponents
{
    public class PendientesBadgeViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;

        public PendientesBadgeViewComponent(ApplicationDbContext db)
        {
            _db = db;
        }

        public IViewComponentResult Invoke()
        {
            var cantidad = _db.Reservas.Count(r => r.Estado == Models.EstadoReserva.Pendiente);
            return View(cantidad);
        }
    }
}
