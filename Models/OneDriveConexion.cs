namespace GestionCabanas.Models
{
    public class OneDriveConexion
    {
        public int Id { get; set; }

        public string? RefreshTokenCifrado { get; set; }

        public string? CuentaEmail { get; set; }

        public DateTime? FechaConexion { get; set; }

        public DateTime? UltimaSincronizacion { get; set; }

        public DateTime? UltimaModificacionExcelVista { get; set; }

        /// <summary>
        /// Último día (inclusive) para el que ya se revisó, en el calendario del Excel, si quedó
        /// sin reservar y hay que marcarlo en amarillo. Los días posteriores a este todavía no se
        /// revisaron.
        /// </summary>
        public DateTime? UltimoDiaColoreado { get; set; }
    }
}
