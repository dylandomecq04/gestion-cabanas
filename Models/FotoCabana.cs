namespace GestionCabanas.Models
{
    public class FotoCabana
    {
        public int Id { get; set; }

        public int CabanaId { get; set; }
        public Cabana? Cabana { get; set; }

        public string RutaArchivo { get; set; } = string.Empty;
        public int Orden { get; set; }
    }
}
