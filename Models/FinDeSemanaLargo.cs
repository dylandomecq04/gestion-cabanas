using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    /// <summary>
    /// Un fin de semana largo cargado por el admin. Si <see cref="ExigeReservaCompleta"/> está activo, desde el
    /// sitio público sólo se puede reservar la estadía entera (de la entrada a la salida, o una que la incluya):
    /// no se puede reservar sólo una parte. Las fechas se cargan como las de una reserva: el día de entrada y el
    /// día de salida (jueves a lunes = 4 noches).
    /// </summary>
    public class FinDeSemanaLargo
    {
        public int Id { get; set; }

        [StringLength(80)]
        [Display(Name = "Nombre (opcional)")]
        public string? Nombre { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Entrada")]
        public DateTime FechaDesde { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Salida")]
        public DateTime FechaHasta { get; set; }

        [Display(Name = "Solo se reserva completo")]
        public bool ExigeReservaCompleta { get; set; } = true;

        public int Noches => (FechaHasta.Date - FechaDesde.Date).Days;

        /// <summary>
        /// True si la estadía pasa alguna noche de este fin de semana largo pero no las pasa todas: justo
        /// lo que no se permite cuando se exige la reserva completa.
        /// </summary>
        public bool EstadiaIncompleta(DateTime desde, DateTime hasta)
        {
            var toca = desde.Date < FechaHasta.Date && hasta.Date > FechaDesde.Date;
            var cubreTodo = desde.Date <= FechaDesde.Date && hasta.Date >= FechaHasta.Date;
            return toca && !cubreTodo;
        }

        /// <summary>El aviso para el huésped cuando pide sólo una parte.</summary>
        public string MensajeReservaCompleta()
        {
            var prefijo = string.IsNullOrWhiteSpace(Nombre) ? "Fin de semana largo" : Nombre.Trim();
            return $"{prefijo}: se reserva completo, del {FechaDesde:dddd d/M} al {FechaHasta:dddd d/M} ({Noches} noches). " +
                   "Elegí esas fechas o una estadía que las incluya.";
        }
    }
}
