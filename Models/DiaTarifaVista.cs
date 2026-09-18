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
        public decimal? PrecioDelTramo(int tramo) => tramo switch
        {
            2 => Precio2,
            4 => Precio4,
            6 => Precio6,
            _ => null
        };
        public bool EnPromo { get; set; }
        public string? EtiquetaPromo { get; set; }
    }
}
