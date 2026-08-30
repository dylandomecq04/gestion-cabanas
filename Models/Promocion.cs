using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public enum LadoPromocion
    {
        Izquierda,
        Derecha
    }

    public class Promocion
    {
        public int Id { get; set; }

        public LadoPromocion Lado { get; set; }

        [Display(Name = "Mostrar en el sitio")]
        public bool Activa { get; set; }

        [StringLength(40)]
        [Display(Name = "Etiqueta destacada")]
        public string? Etiqueta { get; set; }

        [StringLength(120)]
        [Display(Name = "Título")]
        public string? Titulo { get; set; }

        [StringLength(300)]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }
    }
}
