namespace GestionCabanas.Models
{
    public class TarifaDiaInput
    {
        public int CabanaId { get; set; }
        public DateTime Fecha { get; set; }
        public decimal? Precio2 { get; set; }
        public decimal? Precio4 { get; set; }
        public decimal? Precio6 { get; set; }
    }
}
