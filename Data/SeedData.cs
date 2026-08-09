using GestionCabanas.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestionCabanas.Data
{
    public static class SeedData
    {
        public static void Inicializar(ApplicationDbContext db, IPasswordHasher<AdminUsuario> hasher, IConfiguration config, ILogger logger)
        {
            if (!db.AdminUsuarios.Any())
            {
                var usuario = config["AdminSeed:Usuario"];
                var password = config["AdminSeed:Password"];

                if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
                {
                    logger.LogWarning("No se creó el usuario admin: falta configurar AdminSeed:Usuario y AdminSeed:Password (ver dotnet user-secrets).");
                }
                else
                {
                    var admin = new AdminUsuario { NombreUsuario = usuario };
                    admin.PasswordHash = hasher.HashPassword(admin, password);
                    db.AdminUsuarios.Add(admin);
                }
            }

            if (!db.Cabanas.Any())
            {
                var nombres = new[] { "Sidharta 1", "Sidharta 2", "Sidharta 3", "Maia", "Sidharta 5" };
                foreach (var nombre in nombres)
                {
                    db.Cabanas.Add(new Cabana
                    {
                        Nombre = nombre,
                        Descripcion = "Cabaña del complejo Cabañas Sidharta, en El Tigre, a metros del río. Completá la descripción desde el panel de administración.",
                        Capacidad = 4,
                        Activa = true
                    });
                }
            }

            if (!db.InformacionSitio.Any())
            {
                db.InformacionSitio.Add(new InformacionSitio
                {
                    Direccion = "Completá acá la dirección real del complejo (ej: Ruta Provincial 25, Km 3, El Tigre, Buenos Aires).",
                    ComoLlegar = "Contanos acá cómo llegar en auto, lancha o colectivo. Completá esta información desde el panel de administración.",
                    Comodidades = "Pileta compartida\nParrilla individual\nWiFi\nEstacionamiento\nMuelle propio\nRío a metros",
                    InformacionAdicional = "Agregá acá cualquier información adicional que quieras que vean tus huéspedes."
                });
            }

            if (!db.PreguntasFrecuentes.Any())
            {
                db.PreguntasFrecuentes.AddRange(
                    new PreguntaFrecuente { Pregunta = "¿A qué hora es el check-in y el check-out?", Respuesta = "Completá esta respuesta desde el panel de administración.", Orden = 1 },
                    new PreguntaFrecuente { Pregunta = "¿Aceptan mascotas?", Respuesta = "Completá esta respuesta desde el panel de administración.", Orden = 2 },
                    new PreguntaFrecuente { Pregunta = "¿Cómo se paga la reserva?", Respuesta = "Completá esta respuesta desde el panel de administración.", Orden = 3 }
                );
            }

            db.SaveChanges();
        }
    }
}
