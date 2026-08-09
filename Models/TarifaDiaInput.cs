namespace GestionCabanas.Models
{
    public class TarifaDiaInput
    {
        public int CabanaId { get; set; }
        public DateTime Fecha { get; set; }
        public decimal? Precio { get; set; }
        public bool Bloqueada { get; set; }
    }
}
