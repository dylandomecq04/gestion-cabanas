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

        private static MailboxAddress Remitente(string usuario) => new("Sidharta Cabañas", usuario);

        public async Task NotificarNuevaSolicitudAsync(Cabana cabana, Reserva reserva, string urlBase)
        {
            var usuario = _config["Notificaciones:Email:Usuario"];
            var destinatario = _config["Notificaciones:Email:Destinatario"];

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(destinatario))
            {
                _logger.LogWarning("Notificación de email omitida: falta completar Notificaciones:Email en appsettings.json");
                return;
            }

            var urlPanel = $"{urlBase.TrimEnd('/')}/Admin/Reservas?estado=Pendiente&anio={reserva.FechaDesde.Year}&mes={reserva.FechaDesde.Month}";

            var mensaje = new MimeMessage();
            mensaje.From.Add(Remitente(usuario));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = $"Nueva solicitud de reserva - {cabana.Nombre}";
            mensaje.Body = new TextPart("plain")
            {
                Text = $"""
                    Nueva solicitud de reserva 🏡

                    Cabaña: {cabana.Nombre}
                    Huésped: {reserva.NombreHuesped}
                    Fechas: {reserva.FechaDesde:dd/MM/yyyy} – {reserva.FechaHasta:dd/MM/yyyy}
                    Personas: {reserva.CantidadPersonas} ({reserva.CantidadAdultos} adultos, {reserva.CantidadMenores} menores)
                    Teléfono: {reserva.Telefono}

                    Confirmala o rechazala desde el panel de administración:
                    {urlPanel}
                    """
            };

            await EnviarAsync(mensaje, reserva.Id);
        }

        public async Task NotificarConfirmacionHuespedAsync(Cabana cabana, Reserva reserva)
        {
            var usuario = _config["Notificaciones:Email:Usuario"];

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(reserva.Email))
            {
                return;
            }

            var mensaje = new MimeMessage();
            mensaje.From.Add(Remitente(usuario));
            mensaje.To.Add(MailboxAddress.Parse(reserva.Email));
            mensaje.Subject = $"Recibimos tu solicitud de reserva - {cabana.Nombre}";
            mensaje.Body = new TextPart("plain")
            {
                Text = $"""
                    ¡Hola {reserva.NombreHuesped}!

                    Recibimos tu solicitud de reserva 🏡

                    Cabaña: {cabana.Nombre}
                    Fechas: {reserva.FechaDesde:dd/MM/yyyy} – {reserva.FechaHasta:dd/MM/yyyy}
                    Personas: {reserva.CantidadPersonas} ({reserva.CantidadAdultos} adultos, {reserva.CantidadMenores} menores)

                    Para coordinar el pago y confirmarla, escribinos por WhatsApp al 11 2645-2644.
                    """
            };

            await EnviarAsync(mensaje, reserva.Id);
        }

        public async Task NotificarReservaConfirmadaAsync(Cabana cabana, Reserva reserva)
        {
            var usuario = _config["Notificaciones:Email:Usuario"];

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(reserva.Email)
                || !MailboxAddress.TryParse(reserva.Email, out var destino))
            {
                return;
            }

            var mensaje = new MimeMessage();
            mensaje.From.Add(Remitente(usuario));
            mensaje.To.Add(destino);
            mensaje.Subject = $"Tu reserva está confirmada - {cabana.Nombre}";
            mensaje.Body = new TextPart("plain")
            {
                Text = $"""
                    ¡Hola {reserva.NombreHuesped}!

                    ¡Tu reserva está confirmada! 🏡

                    Cabaña: {cabana.Nombre}
                    Fechas: {reserva.FechaDesde:dd/MM/yyyy} – {reserva.FechaHasta:dd/MM/yyyy}
                    Personas: {reserva.CantidadPersonas} ({reserva.CantidadAdultos} adultos, {reserva.CantidadMenores} menores)

                    Cualquier consulta, escribinos por WhatsApp al 11 2645-2644.
                    ¡Te esperamos!
                    """
            };

            await EnviarAsync(mensaje, reserva.Id);
        }

        private async Task EnviarAsync(MimeMessage mensaje, int reservaId)
        {
            var host = _config["Notificaciones:Email:SmtpHost"];
            var portTexto = _config["Notificaciones:Email:SmtpPort"];
            var usuario = _config["Notificaciones:Email:Usuario"];
            var appPassword = _config["Notificaciones:Email:AppPassword"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(appPassword))
            {
                _logger.LogWarning("Notificación de email omitida: falta completar Notificaciones:Email en appsettings.json");
                return;
            }

            var puerto = int.TryParse(portTexto, out var p) ? p : 587;

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
                _logger.LogError(ex, "No se pudo enviar el email de notificación para la reserva {ReservaId}", reservaId);
            }
        }
    }
}
