namespace GestionCabanas.Models
{
    public class DiaTarifaVista
    {
        public DateTime Fecha { get; set; }
        public bool Reservada { get; set; }
        public bool Pasada { get; set; }
        public decimal? Precio2 { get; set; }
        public decimal? Precio4 { get; set; }
        public decimal? Precio6 { get; set; }
        public bool EnPromo { get; set; }
        public string? EtiquetaPromo { get; set; }
    }
}
