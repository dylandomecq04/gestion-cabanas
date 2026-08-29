using GestionCabanas.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace GestionCabanas.Services
{
    public class EmailNotificacionService : INotificacionEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailNotificacionService> _logger;

        public EmailNotificacionService(IConfiguration config, ILogger<EmailNotificacionService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task NotificarNuevaSolicitudAsync(Cabana cabana, Reserva reserva)
        {
            var host = _config["Notificaciones:Email:SmtpHost"];
            var portTexto = _config["Notificaciones:Email:SmtpPort"];
            var usuario = _config["Notificaciones:Email:Usuario"];
            var appPassword = _config["Notificaciones:Email:AppPassword"];
            var destinatario = _config["Notificaciones:Email:Destinatario"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(usuario) ||
                string.IsNullOrWhiteSpace(appPassword) || string.IsNullOrWhiteSpace(destinatario))
            {
                _logger.LogWarning("Notificación de email omitida: falta completar Notificaciones:Email en appsettings.json");
                return;
            }

            var puerto = int.TryParse(portTexto, out var p) ? p : 587;

            var mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(usuario));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = $"Nueva solicitud de reserva - {cabana.Nombre}";
            mensaje.Body = new TextPart("plain")
            {
                Text = $"""
                    Nueva solicitud de reserva 🏡

                    Cabaña: {cabana.Nombre}
                    Huésped: {reserva.NombreHuesped}
                    Fechas: {reserva.FechaDesde:dd/MM/yyyy} – {reserva.FechaHasta:dd/MM/yyyy}
                    Personas: {reserva.CantidadPersonas}
                    Teléfono: {reserva.Telefono}

                    Entrá al panel de administración para confirmarla o rechazarla.
                    """
            };

            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(host, puerto, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(usuario, appPassword);
                await client.SendAsync(mensaje);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el email de notificación para la reserva {ReservaId}", reserva.Id);
            }
        }
    }
}
