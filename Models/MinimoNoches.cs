namespace GestionCabanas.Models
{
    /// <summary>
    /// Mínimo de noches que tiene que tener una estadía que incluya la noche de esta fecha puntual
    /// (ej.: sábado 4/10 con mínimo 2 noches). Un registro por fecha; sin registro para un día no hay restricción.
    /// </summary>
    public class MinimoNoches
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; }

        public int Noches { get; set; }
    }
}
