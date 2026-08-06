namespace GestionCabanas.Models
{
    public class TarifaDiaInput
    {
        public DateTime Fecha { get; set; }
        public decimal? Precio { get; set; }
        public bool Bloqueada { get; set; }
    }
}
