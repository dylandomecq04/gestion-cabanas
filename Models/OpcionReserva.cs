namespace GestionCabanas.Models
{
    public class SegmentoOpcion
    {
        public int CabanaId { get; set; }
        public string CabanaNombre { get; set; } = string.Empty;
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public decimal? Subtotal { get; set; }
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
    }
}
