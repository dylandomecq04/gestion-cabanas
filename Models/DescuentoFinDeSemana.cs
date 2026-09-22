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

        /// <summary>
        /// Monto del descuento que se anuncia en el calendario para un día: el del sábado o el del domingo
        /// siguiente (que cuenta el mes del sábado). Null si ese día no participa: no es fin de semana, no hay
        /// monto cargado o, para un domingo, su sábado ya pasó y no se puede reservar con él.
        /// </summary>
        public static decimal? MontoDelDia(IReadOnlyDictionary<(int Anio, int Mes), decimal> montos, DateTime dia, DateTime hoy)
        {
            var sabado = dia.DayOfWeek switch
            {
                DayOfWeek.Saturday => dia.Date,
                DayOfWeek.Sunday => dia.Date.AddDays(-1),
                _ => (DateTime?)null
            };

            if (sabado is null || sabado.Value < hoy.Date)
            {
                return null;
            }

            return montos.TryGetValue((sabado.Value.Year, sabado.Value.Month), out var monto) && monto > 0 ? monto : null;
        }

        /// <summary>Texto corto para la celda del calendario: "−20 mil" si es redondo, "Promo" si no entra.</summary>
        public static string TextoCorto(decimal monto) =>
            monto % 1000 == 0 ? $"−{monto / 1000:0} mil" : "Promo";
    }
}
