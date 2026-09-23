using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    /// <summary>
    /// Monto que se resta cuando el huésped elige salir el domingo a la noche en vez de quedarse hasta
    /// el lunes a la mañana. Se define por mes y el mes que cuenta es el del sábado.
    /// </summary>
    public class DescuentoSalidaAnticipada
    {
        public int Id { get; set; }

        public int Anio { get; set; }

        public int Mes { get; set; }

        [Range(1, 999999999)]
        public decimal Monto { get; set; }
    }
}
