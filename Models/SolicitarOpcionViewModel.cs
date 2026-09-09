using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class SolicitarOpcionViewModel
    {
        [Required(ErrorMessage = "Ingresá tu nombre")]
        [StringLength(150)]
        public string NombreHuesped { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresá un teléfono de contacto")]
        [StringLength(50)]
        public string? Telefono { get; set; }

        [Range(1, 50, ErrorMessage = "La cantidad de personas debe ser mayor a 0")]
        public int CantidadPersonas { get; set; } = 1;

        public List<SegmentoInput> Segmentos { get; set; } = new();
    }

    public class SegmentoInput
    {
        public int CabanaId { get; set; }
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
    }
}
