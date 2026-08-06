using System.Text;
using System.Text.RegularExpressions;
using GestionCabanas.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestionCabanas.Services
{
    public class CallMeBotWhatsAppService : INotificacionWhatsAppService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<CallMeBotWhatsAppService> _logger;

        public CallMeBotWhatsAppService(HttpClient http, IConfiguration config, ILogger<CallMeBotWhatsAppService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public async Task NotificarNuevaSolicitudAsync(Cabana cabana, Reserva reserva)
        {
            var telefono = _config["Notificaciones:WhatsApp:Telefono"];
            var apiKey = _config["Notificaciones:WhatsApp:ApiKey"];

            if (string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Notificación de WhatsApp omitida: falta configurar Notificaciones:WhatsApp:Telefono o ApiKey en appsettings.json");
                return;
            }

            var telefonoLimpio = Regex.Replace(telefono, "[^0-9]", "");

            var mensaje = new StringBuilder()
                .AppendLine("🏡 Nueva solicitud de reserva")
                .AppendLine()
                .AppendLine($"Cabaña: {cabana.Nombre}")
                .AppendLine($"Huésped: {reserva.NombreHuesped}")
                .AppendLine($"Fechas: {reserva.FechaDesde:dd/MM/yyyy} – {reserva.FechaHasta:dd/MM/yyyy}")
                .AppendLine($"Personas: {reserva.CantidadPersonas}")
                .AppendLine($"Teléfono: {reserva.Telefono}")
                .ToString();

            var url = $"https://api.callmebot.com/whatsapp.php?phone={telefonoLimpio}&text={Uri.EscapeDataString(mensaje)}&apikey={Uri.EscapeDataString(apiKey)}";

            try
            {
                var respuesta = await _http.GetAsync(url);
                if (!respuesta.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CallMeBot respondió {StatusCode} al notificar la reserva {ReservaId}", respuesta.StatusCode, reserva.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar la notificación de WhatsApp para la reserva {ReservaId}", reserva.Id);
            }
        }
    }
}
