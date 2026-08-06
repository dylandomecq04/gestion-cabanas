using GestionCabanas.Models;

namespace GestionCabanas.Services
{
    public interface INotificacionWhatsAppService
    {
        Task NotificarNuevaSolicitudAsync(Cabana cabana, Reserva reserva);
    }
}
