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

            db.SaveChanges();
        }
    }
}
