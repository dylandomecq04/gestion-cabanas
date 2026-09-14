using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public enum EstadoReserva
    {
        [Display(Name = "Solicitud")]
        Pendiente = 0,
        Confirmada = 1
    }
}
