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

        [StringLength(300)]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

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

        public decimal? PrecioPorNoches(int noches) => noches switch
        {
            1 => Precio1Noche,
            2 => Precio2Noches,
            3 => Precio3Noches,
            _ => null
        };

        /// <summary>Precio de referencia por noche para el calendario: usa el paquete más corto cargado
        /// (1 noche si está, si no promedia el de 2 o el de 3).</summary>
        public decimal? PrecioPromedioPorNoche()
        {
            if (Precio1Noche.HasValue) return Precio1Noche;
            if (Precio2Noches.HasValue) return Math.Round(Precio2Noches.Value / 2, MidpointRounding.AwayFromZero);
            if (Precio3Noches.HasValue) return Math.Round(Precio3Noches.Value / 3, MidpointRounding.AwayFromZero);
            return null;
        }
    }
}
