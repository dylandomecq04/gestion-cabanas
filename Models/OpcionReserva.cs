namespace GestionCabanas.Models
{
    public class SegmentoOpcion
    {
        public int CabanaId { get; set; }
        public string CabanaNombre { get; set; } = string.Empty;
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public decimal? Subtotal { get; set; }

        /// <summary>Lo que costaría el segmento sin la promo (tarifa normal); sólo cuando la promo aplicada ahorra algo.</summary>
        public decimal? SubtotalSinPromo { get; set; }
        public bool PromoAplicada { get; set; }
        public string? EtiquetaPromo { get; set; }
        public string? EtiquetaTarifa { get; set; }

        /// <summary>Personas que se alojan en esta cabaña (cuando el grupo se reparte en dos, sólo una parte).</summary>
        public int Adultos { get; set; }
        public int Menores { get; set; }
        public int Personas => Adultos + Menores;
    }

    public class OpcionReserva
    {
        public List<SegmentoOpcion> Segmentos { get; set; } = new();
        public decimal? Total { get; set; }

        /// <summary>Lo que costaría la opción sin las promos; sólo cuando alguna promo ahorra algo.</summary>
        public decimal? TotalSinPromo { get; set; }

        /// <summary>
        /// El grupo se reparte en dos cabañas que se ocupan a la vez, en las mismas fechas. Si es false,
        /// los segmentos son una sola cabaña o cabañas que se van sucediendo durante la estadía.
        /// </summary>
        public bool Repartida { get; set; }

        /// <summary>Sólo para las repartidas: las dos cabañas quedan una al lado de la otra.</summary>
        public bool Contiguas { get; set; }
    }

    /// <summary>Cómo se reparte un grupo entre dos cabañas que se ocupan a la vez.</summary>
    public record RepartoEnCabanas(Cabana Primera, Cabana Segunda, Huespedes HuespedesPrimera, Huespedes HuespedesSegunda);

    public class ResultadoBusquedaDisponibilidad
    {
        public bool CobreTotal { get; set; }
        public List<DateTime> DiasSinCobertura { get; set; } = new();
        public List<OpcionReserva> Opciones { get; set; } = new();
        public string? Mensaje { get; set; }
    }

    public class ResultadoPrecio
    {
        public decimal? Total { get; set; }

        /// <summary>Lo que costaría la estadía a tarifa normal; sólo cuando se aplicó una promo que ahorra algo.</summary>
        public decimal? TotalSinPromo { get; set; }
        public bool PromoAplicada { get; set; }
        public string? EtiquetaPromo { get; set; }

        /// <summary>Qué tarifa se usó (p. ej. "Tarifa para 4 personas"); null si aplicó una promo.</summary>
        public string? EtiquetaTarifa { get; set; }
    }

    public class CabanaAlternativa
    {
        public int CabanaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal? Precio { get; set; }
    }

    /// <summary>
    /// Solicitud pendiente que quedó en conflicto de fechas/cabaña tras confirmar otra reserva.
    /// Se muestra en un modal para que el admin decida si mover la solicitud a otra cabaña.
    /// </summary>
    public class ConflictoSolicitud
    {
        public int ReservaId { get; set; }
        public string NombreHuesped { get; set; } = string.Empty;
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
        public decimal? Valor { get; set; }
        public List<CabanaAlternativa> Alternativas { get; set; } = new();
    }
}
