using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class PromoEstadiaInput
    {
        public int CabanaId { get; set; }

        [StringLength(80)]
        public string? Nombre { get; set; }

        [StringLength(300)]
        public string? Descripcion { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime FechaDesde { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime FechaHasta { get; set; }

        [Range(0, 999999999)]
        public decimal? Precio1Noche { get; set; }

        [Range(0, 999999999)]
        public decimal? Precio2Noches { get; set; }

        [Range(0, 999999999)]
        public decimal? Precio3Noches { get; set; }

        public bool AplicarATodas { get; set; }
    }
}
