using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class PromoEstadia
    {
        public int Id { get; set; }

        public int CabanaId { get; set; }
        public Cabana? Cabana { get; set; }

        [StringLength(80)]
        [Display(Name = "Nombre de la promoción")]
        public string? Nombre { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Desde")]
        public DateTime FechaDesde { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Hasta")]
        public DateTime FechaHasta { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Precio 1 noche")]
        public decimal? Precio1Noche { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Precio 2 noches")]
        public decimal? Precio2Noches { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Precio 3 noches")]
        public decimal? Precio3Noches { get; set; }

        [Display(Name = "Activa")]
        public bool Activa { get; set; } = true;
    }
}
