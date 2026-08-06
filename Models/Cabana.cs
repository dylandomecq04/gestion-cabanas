using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class Cabana
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Descripcion { get; set; }

        [Range(1, 50, ErrorMessage = "La capacidad debe ser mayor a 0")]
        public int Capacidad { get; set; }

        [Range(0, 999999999)]
        public decimal? PrecioPorNoche { get; set; }

        public bool Activa { get; set; } = true;

        public List<FotoCabana> Fotos { get; set; } = new();
        public List<Reserva> Reservas { get; set; } = new();
    }
}
