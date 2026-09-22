namespace GestionCabanas.Models
{
    /// <summary>
    /// Mínimo de noches que tiene que tener una estadía que incluya la noche de tal día de la semana
    /// (ej.: reservar el sábado exige al menos 2 noches). Un registro por día; sin registro para un día
    /// no hay restricción ese día.
    /// </summary>
    public class MinimoNoches
    {
        public int Id { get; set; }

        public DayOfWeek DiaSemana { get; set; }

        public int Noches { get; set; }

        private static readonly string[] NombresDias =
            { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };

        public static string NombreDia(DayOfWeek dia) => NombresDias[(int)dia];
    }
}
