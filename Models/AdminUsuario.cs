namespace GestionCabanas.Models
{
    public class AdminUsuario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}
