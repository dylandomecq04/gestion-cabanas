using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class Cabana
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Range(1, 50, ErrorMessage = "La capacidad debe ser mayor a 0")]
        public int Capacidad { get; set; }

        public bool Activa { get; set; } = true;

        /// <summary>Tramos de precio por cantidad de personas que existen en total.</summary>
        public static readonly int[] TodosLosTramos = { 2, 4, 6 };

        /// <summary>
        /// Si tiene sentido cargar el precio de un tramo en esta cabaña: el tramo de 2 siempre, y los
        /// de 4 y 6 solo si la cabaña tiene lugar para más de 2 y más de 4 personas. Así las cabañas
        /// para 4 no piden un precio para 6.
        /// </summary>
        public bool AdmiteTramo(int tramo) => tramo <= 2 || Capacidad > tramo - 2;

        /// <summary>Los tramos de precio que se cargan en esta cabaña.</summary>
        public IEnumerable<int> TramosDePrecio() => TodosLosTramos.Where(AdmiteTramo);

        public List<FotoCabana> Fotos { get; set; } = new();
        public List<Reserva> Reservas { get; set; } = new();
        public List<TarifaDia> TarifasDias { get; set; } = new();
    }
}
