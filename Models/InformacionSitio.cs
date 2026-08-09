using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class InformacionSitio
    {
        public int Id { get; set; }

        [StringLength(300)]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }

        [StringLength(3000)]
        [Display(Name = "Cómo llegar")]
        public string? ComoLlegar { get; set; }

        [StringLength(3000)]
        [Display(Name = "Comodidades del complejo (una por línea)")]
        public string? Comodidades { get; set; }

        [StringLength(3000)]
        [Display(Name = "Información adicional")]
        public string? InformacionAdicional { get; set; }
    }
}
