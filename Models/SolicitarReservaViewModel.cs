using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class SolicitarReservaViewModel
    {
        public int CabanaId { get; set; }
        public string? NombreCabana { get; set; }

        [Required(ErrorMessage = "Ingresá tu nombre")]
        [StringLength(150)]
        [Display(Name = "Nombre y apellido")]
        public string NombreHuesped { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresá un teléfono de contacto")]
        [StringLength(50)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "Elegí la fecha de entrada")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de entrada")]
        public DateTime FechaDesde { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Elegí la fecha de salida")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de salida")]
        public DateTime FechaHasta { get; set; } = DateTime.Today.AddDays(2);

        [Required(ErrorMessage = "Indicá la cantidad de adultos")]
        [Range(1, PoliticaPrecios.MaxPersonasPorReserva, ErrorMessage = "Tiene que haber al menos un adulto (y hasta 8 personas en total)")]
        [Display(Name = "Adultos")]
        public int CantidadAdultos { get; set; } = 2;

        [Range(0, PoliticaPrecios.MaxPersonasPorReserva - 1, ErrorMessage = "La cantidad de menores no es válida")]
        [Display(Name = "Menores")]
        public int CantidadMenores { get; set; }

        public int CantidadPersonas => CantidadAdultos + CantidadMenores;
    }
}
