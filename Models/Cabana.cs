using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class Cabana
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "La descripción no puede pasar de 2000 caracteres")]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        [Range(1, 50, ErrorMessage = "La capacidad debe ser mayor a 0")]
        public int Capacidad { get; set; }

        public bool Activa { get; set; } = true;

        /// <summary>
        /// Lugar que ocupa la cabaña en la fila física del complejo (1 = la primera). Sirve para saber
        /// cuáles quedan una al lado de la otra cuando un grupo se reparte en dos.
        /// </summary>
        [Display(Name = "Posición en la fila")]
        [Range(1, 999, ErrorMessage = "La posición debe ser un número mayor a 0")]
        public int Orden { get; set; }

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

        /// <summary>
        /// Grupo de precios al que pertenece (ej: "Sidharta 1", "Sidharta 2" y "Sidharta 5" son del
        /// grupo "Sidharta"; "Maia" es su propio grupo). Se usa para cargar precios por grupo en vez
        /// de cabaña por cabaña.
        /// </summary>
        public string Grupo => System.Text.RegularExpressions.Regex.Replace(Nombre, @"\s+\d+$", "").Trim();

        public List<FotoCabana> Fotos { get; set; } = new();
        public List<Reserva> Reservas { get; set; } = new();
        public List<TarifaDia> TarifasDias { get; set; } = new();
    }
}
