namespace GestionCabanas.Models
{
    public enum TipoItemGaleria
    {
        Foto = 0,
        Reel = 1
    }

    // Galería del predio: fotos del lugar y reels (videos verticales). Se muestran del más
    // nuevo al más viejo, por eso no lleva orden manual.
    public class ItemGaleria
    {
        public int Id { get; set; }

        public TipoItemGaleria Tipo { get; set; }
        public string RutaArchivo { get; set; } = string.Empty;
    }
}
