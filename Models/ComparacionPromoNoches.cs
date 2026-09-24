namespace GestionCabanas.Models
{
    /// <summary>Para un paquete de la promo (1, 2 o 3 noches), el precio regular de cada tramo
    /// admitido por la cabaña (para tacharlo) junto al precio de la promo para esa cantidad de noches.</summary>
    public class ComparacionPromoNoches
    {
        public int Noches { get; set; }
        public decimal PrecioPromo { get; set; }
        public List<(int Tramo, decimal? PrecioRegular)> PreciosRegulares { get; set; } = new();
    }
}
