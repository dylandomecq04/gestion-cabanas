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
        public decimal? Precio { get; set; }

        public bool Bloqueada { get; set; }
    }
}
