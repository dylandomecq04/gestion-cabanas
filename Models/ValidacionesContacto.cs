namespace GestionCabanas.Models
{
    /// <summary>
    /// Patrón compartido por los formularios públicos de reserva para rechazar emails que no
    /// tengan forma de serlo. La validación de teléfono vive en <see cref="TelefonoValidoAttribute"/>.
    /// </summary>
    public static class ValidacionesContacto
    {
        public const string PatronEmail = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        public const string MensajeEmail = "Ingresá un email válido (ejemplo: nombre@dominio.com)";
    }
}
