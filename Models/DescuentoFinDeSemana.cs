using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    /// <summary>
    /// Monto que se le resta al total de una cabaña por cada sábado + domingo que se reservan juntos.
    /// Se define por mes y el mes que cuenta es el del sábado.
    /// </summary>
    public class DescuentoFinDeSemana
    {
        public int Id { get; set; }

        public int Anio { get; set; }

        public int Mes { get; set; }

        [Range(1, 999999999)]
        public decimal Monto { get; set; }
    }
}
