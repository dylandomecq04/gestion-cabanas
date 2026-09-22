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

        /// <summary>Si ese día es una de las noches del fin de semana largo (la de la salida ya no cuenta).</summary>
        public bool IncluyeDia(DateTime dia) => dia.Date >= FechaDesde.Date && dia.Date < FechaHasta.Date;

        /// <summary>
        /// Clases del marco que se dibuja sobre cada celda de un calendario semanal (lunes a domingo) para
        /// enmarcar el fin de semana largo: borde arriba y abajo en todos los días, y cerrado a los costados
        /// en el primer día, en el último y donde la semana se corta. Null si el día no pertenece a ninguno.
        /// Se usa dentro de la celda (que tiene que ser <c>relative</c>): el marco sobresale medio hueco
        /// para que se una con el de la celda de al lado.
        /// </summary>
        public static string? ClasesMarco(IEnumerable<FinDeSemanaLargo> fines, DateTime dia)
        {
            var fin = fines.FirstOrDefault(f => f.IncluyeDia(dia));
            if (fin is null)
            {
                return null;
            }

            var clases = "pointer-events-none absolute -inset-0.5 z-10 border-y-2 border-amber-500";
            if (dia.Date == fin.FechaDesde.Date || dia.DayOfWeek == DayOfWeek.Monday)
            {
                clases += " rounded-l-xl border-l-2";
            }
            if (dia.Date == fin.FechaHasta.Date.AddDays(-1) || dia.DayOfWeek == DayOfWeek.Sunday)
            {
                clases += " rounded-r-xl border-r-2";
            }
            return clases;
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
