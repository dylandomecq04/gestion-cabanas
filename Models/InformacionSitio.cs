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
        [Display(Name = "En auto (un paso por línea)")]
        public string? ComoLlegarAuto { get; set; }

        [StringLength(3000)]
        [Display(Name = "En tren (un paso por línea)")]
        public string? ComoLlegarTren { get; set; }

        [StringLength(100)]
        [Display(Name = "Lugar donde se toma la lancha")]
        public string? EmbarcaderoNombre { get; set; }

        [StringLength(300)]
        [Display(Name = "Dirección de ese lugar")]
        public string? EmbarcaderoDireccion { get; set; }

        [StringLength(60)]
        [Display(Name = "Teléfono de ese lugar")]
        public string? EmbarcaderoTelefono { get; set; }

        [StringLength(1000)]
        [Display(Name = "Lanchas remis (una por línea)")]
        public string? LanchasRemis { get; set; }

        [StringLength(3000)]
        [Display(Name = "Comodidades del complejo (una por línea)")]
        public string? Comodidades { get; set; }

        [StringLength(3000)]
        [Display(Name = "Dónde comprar (uno por línea)")]
        public string? DondeComprar { get; set; }

        [StringLength(3000)]
        [Display(Name = "Actividades en la isla (una por línea)")]
        public string? Actividades { get; set; }

        [StringLength(6000)]
        [Display(Name = "A tener en cuenta (uno por línea)")]
        public string? Normas { get; set; }

        [StringLength(3000)]
        [Display(Name = "Información adicional")]
        public string? InformacionAdicional { get; set; }
    }
}
