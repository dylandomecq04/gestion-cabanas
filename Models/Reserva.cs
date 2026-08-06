using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class Reserva
    {
        public int Id { get; set; }

        [Required]
        public int CabanaId { get; set; }
        public Cabana? Cabana { get; set; }

        [Required(ErrorMessage = "El nombre del huésped es obligatorio")]
        [StringLength(150)]
        public string NombreHuesped { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Telefono { get; set; }

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "El email no es válido")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "La fecha de entrada es obligatoria")]
        [DataType(DataType.Date)]
        public DateTime FechaDesde { get; set; }

        [Required(ErrorMessage = "La fecha de salida es obligatoria")]
        [DataType(DataType.Date)]
        public DateTime FechaHasta { get; set; }

        [Range(1, 50, ErrorMessage = "La cantidad de personas debe ser mayor a 0")]
        [Display(Name = "Cantidad de personas")]
        public int CantidadPersonas { get; set; } = 1;

        public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;

        [Range(0, 999999999)]
        public decimal? Valor { get; set; }

        [StringLength(1000)]
        public string? Notas { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
