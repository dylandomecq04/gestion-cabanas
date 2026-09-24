using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    /// <summary>
    /// Fila única: umbrales de horas, contadas desde la fecha en que se marcó una solicitud como
    /// contactada, que definen cuándo se resalta en amarillo o en rojo en el listado de solicitudes.
    /// </summary>
    public class ConfiguracionSolicitudes
    {
        public int Id { get; set; }

        [Range(1, 999)]
        [Display(Name = "Horas para amarillo")]
        public int HorasAmarillo { get; set; } = 24;

        [Range(1, 999)]
        [Display(Name = "Horas para rojo")]
        public int HorasRojo { get; set; } = 48;
    }
}
