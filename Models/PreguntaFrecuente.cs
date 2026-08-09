using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class PreguntaFrecuente
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La pregunta es obligatoria")]
        [StringLength(300)]
        public string Pregunta { get; set; } = string.Empty;

        [Required(ErrorMessage = "La respuesta es obligatoria")]
        [StringLength(2000)]
        public string Respuesta { get; set; } = string.Empty;

        public int Orden { get; set; }
    }
}
