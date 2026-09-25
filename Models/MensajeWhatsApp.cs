namespace GestionCabanas.Models
{
    /// <summary>
    /// Texto editable de cada mensaje de WhatsApp (ver <c>MensajesWhatsAppService</c> para las claves
    /// y las variables que acepta cada uno). Una fila por clave; si falta, se usa el texto original.
    /// </summary>
    public class MensajeWhatsApp
    {
        public int Id { get; set; }

        public string Clave { get; set; } = string.Empty;

        public string Texto { get; set; } = string.Empty;
    }
}
