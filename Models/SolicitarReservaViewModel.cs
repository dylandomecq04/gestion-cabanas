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

        [Required(ErrorMessage = "Indicá la cantidad de personas")]
        [Range(1, 50, ErrorMessage = "La cantidad de personas debe ser mayor a 0")]
        [Display(Name = "Cantidad de personas")]
        public int CantidadPersonas { get; set; } = 1;

        [StringLength(1000)]
        [Display(Name = "Comentarios (opcional)")]
        public string? Notas { get; set; }
    }
}
