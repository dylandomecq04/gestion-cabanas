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

        [Range(1, 50, ErrorMessage = "Tiene que haber al menos un adulto")]
        public int CantidadAdultos { get; set; } = 2;

        [Range(0, 50, ErrorMessage = "La cantidad de menores no es válida")]
        public int CantidadMenores { get; set; }

        public int CantidadPersonas => CantidadAdultos + CantidadMenores;

        public List<SegmentoInput> Segmentos { get; set; } = new();
    }

    public class SegmentoInput
    {
        public int CabanaId { get; set; }
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
    }
}
