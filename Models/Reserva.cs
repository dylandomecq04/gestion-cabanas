using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [Display(Name = "Nombre")]
        public string NombreHuesped { get; set; } = string.Empty;

        [StringLength(50)]
        [TelefonoValido]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "Ingresá un email válido")]
        [StringLength(150)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "La fecha de entrada es obligatoria")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha desde")]
        public DateTime FechaDesde { get; set; }

        [Required(ErrorMessage = "La fecha de salida es obligatoria")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha hasta")]
        public DateTime FechaHasta { get; set; }

        [Range(1, 50, ErrorMessage = "La cantidad de personas debe ser mayor a 0")]
        [Display(Name = "Cantidad de personas")]
        public int CantidadPersonas { get; set; } = 1;

        [Range(0, 50, ErrorMessage = "La cantidad de menores no es válida")]
        [Display(Name = "Menores (ya incluidos en la cantidad de personas)")]
        public int CantidadMenores { get; set; }

        [NotMapped]
        public int CantidadAdultos => Math.Max(0, CantidadPersonas - CantidadMenores);

        public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;

        [Display(Name = "Contactado")]
        public bool Contactado { get; set; }

        [Display(Name = "Fecha contactado")]
        public DateTime? FechaContactado { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Pagó")]
        public decimal? Pago { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Pagar")]
        public decimal? Valor { get; set; }

        /// <summary>El huésped eligió salir el domingo a la noche en vez de quedarse hasta el lunes a la mañana.</summary>
        [Display(Name = "Sale domingo a la noche")]
        public bool SalidaAnticipada { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [StringLength(200)]
        public string? ExcelUbicacion { get; set; }
    }
}
