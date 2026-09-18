using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class TarifaDia
    {
        public int Id { get; set; }

        public int CabanaId { get; set; }
        public Cabana? Cabana { get; set; }

        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }

        [Range(0, 999999999)]
        public decimal? Precio2 { get; set; }

        [Range(0, 999999999)]
        public decimal? Precio4 { get; set; }

        [Range(0, 999999999)]
        public decimal? Precio6 { get; set; }

        [StringLength(200)]
        public string? ExcelUbicacion { get; set; }

        public decimal? PrecioDelTramo(int tramo) => tramo switch
        {
            2 => Precio2,
            4 => Precio4,
            6 => Precio6,
            _ => null
        };
    }
}
