namespace GestionCabanas.Models
{
    public class OneDriveConexion
    {
        public int Id { get; set; }

        public string? RefreshTokenCifrado { get; set; }

        public string? CuentaEmail { get; set; }

        public DateTime? FechaConexion { get; set; }

        public DateTime? UltimaSincronizacion { get; set; }
    }
}
