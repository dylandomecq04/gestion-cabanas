using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class InicioSitio
    {
        public int Id { get; set; }

        [StringLength(150)]
        [Display(Name = "Texto pequeño (arriba del título)")]
        public string? TextoEyebrow { get; set; }

        [StringLength(150)]
        [Display(Name = "Título principal")]
        public string? Titulo { get; set; }
    }
}
