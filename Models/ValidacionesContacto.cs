namespace GestionCabanas.Models
{
    /// <summary>
    /// Patrones compartidos por los formularios públicos de reserva para rechazar teléfonos y
    /// emails que no tengan forma de serlo (ej: "123" o "asd"), sin atarse al formato de un solo país:
    /// Argentina, Uruguay y los países limítrofes usan largos de número distintos.
    /// </summary>
    public static class ValidacionesContacto
    {
        public const string PatronTelefono = @"^(?=(?:\D*\d){8,15}\D*$)\+?[\d\s\-.()]+$";
        public const string MensajeTelefono = "Ingresá un teléfono válido, con al menos 8 números";

        public const string PatronEmail = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        public const string MensajeEmail = "Ingresá un email válido (ejemplo: nombre@dominio.com)";
    }
}
