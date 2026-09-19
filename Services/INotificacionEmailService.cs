using GestionCabanas.Models;

namespace GestionCabanas.Services
{
    public interface INotificacionEmailService
    {
        Task NotificarNuevaSolicitudAsync(Cabana cabana, Reserva reserva);
        Task NotificarConfirmacionHuespedAsync(Cabana cabana, Reserva reserva);
        Task NotificarReservaConfirmadaAsync(Cabana cabana, Reserva reserva);
    }
}
