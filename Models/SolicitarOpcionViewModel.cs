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
        [RegularExpression(ValidacionesContacto.PatronTelefono, ErrorMessage = ValidacionesContacto.MensajeTelefono)]
        public string? Telefono { get; set; }

        [StringLength(150)]
        [RegularExpression(ValidacionesContacto.PatronEmail, ErrorMessage = ValidacionesContacto.MensajeEmail)]
        public string? Email { get; set; }

        [Range(1, PoliticaPrecios.MaxPersonasPorReserva, ErrorMessage = "Tiene que haber al menos un adulto (y hasta 8 personas en total)")]
        public int CantidadAdultos { get; set; } = 2;

        [Range(0, PoliticaPrecios.MaxPersonasPorReserva - 1, ErrorMessage = "La cantidad de menores no es válida")]
        public int CantidadMenores { get; set; }

        public int CantidadPersonas => CantidadAdultos + CantidadMenores;

        public List<SegmentoInput> Segmentos { get; set; } = new();
    }

    public class SegmentoInput
    {
        public int CabanaId { get; set; }
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }

        /// <summary>
        /// Personas que se alojan en esta cabaña según la opción elegida. Sólo importa cuando el grupo se
        /// reparte en dos cabañas, para saber cuál de los repartos ofrecidos se eligió; el servidor lo
        /// compara con los que él mismo calcula. En 0 y 0 se toma el reparto más parejo.
        /// </summary>
        public int Adultos { get; set; }
        public int Menores { get; set; }
    }
}
