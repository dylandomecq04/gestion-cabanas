using GestionCabanas.Models;

namespace GestionCabanas.Services
{
    public interface INotificacionEmailService
    {
        Task NotificarNuevaSolicitudAsync(Cabana cabana, Reserva reserva);
    }
}
