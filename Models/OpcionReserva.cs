namespace GestionCabanas.Models
{
    public class SegmentoOpcion
    {
        public int CabanaId { get; set; }
        public string CabanaNombre { get; set; } = string.Empty;
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public decimal? Subtotal { get; set; }
        public bool PromoAplicada { get; set; }
        public string? EtiquetaPromo { get; set; }
        public string? EtiquetaTarifa { get; set; }
    }

    public class OpcionReserva
    {
        public List<SegmentoOpcion> Segmentos { get; set; } = new();
        public decimal? Total { get; set; }
    }

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
