namespace GestionCabanas.Models
{
    public class DiaTarifaVista
    {
        public DateTime Fecha { get; set; }
        public bool Reservada { get; set; }
        public bool Pasada { get; set; }
        public decimal? Precio { get; set; }
        public bool Bloqueada { get; set; }
    }
}
