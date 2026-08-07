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
                const string descripcionSidharta =
                    "Cabaña de 1 dormitorio para hasta 4 personas, a metros del río, con parrilla privada en área iluminada.\n" +
                    "Equipada con TV Smart, aire frío/calor, agua caliente, vajilla, pava eléctrica, horno a gas, heladera con freezer y luz de emergencia. Wifi con internet satelital Starlink.\n\n" +
                    "Precios por noche (temporada agosto):\n" +
                    "• 2 personas: $45.000 lunes a viernes · promo 3 noches $110.000 · $140.000 sábado y domingo\n" +
                    "• 4 personas: $70.000 lunes a viernes · $180.000 sábado y domingo\n\n" +
                    "Hasta 2 menores de 12 años no abonan. Ingreso desde las 12hs, egreso hasta las 10hs (fin de semana, egreso domingo 20hs).";

                const string descripcionMaia =
                    "Cabaña de 1 dormitorio para hasta 6 personas, a metros del río, con parrilla privada en área iluminada.\n" +
                    "Equipada con TV Smart, aire frío/calor, agua caliente, vajilla, pava eléctrica, horno a gas, heladera con freezer y luz de emergencia. Wifi con internet satelital Starlink.\n\n" +
                    "Precios por noche (temporada agosto):\n" +
                    "• 2 personas: $55.000 lunes a viernes · promo 3 noches $130.000 · $160.000 sábado y domingo\n" +
                    "• 4 personas: $85.000 lunes a viernes · $210.000 sábado y domingo\n" +
                    "• 6 personas: $120.000 lunes a viernes · $250.000 sábado y domingo\n\n" +
                    "Hasta 2 menores de 12 años no abonan. Ingreso desde las 12hs, egreso hasta las 10hs (fin de semana, egreso domingo 20hs).";

                var cabanas = new[]
                {
                    new Cabana { Nombre = "Sidharta 1", Descripcion = descripcionSidharta, Capacidad = 4, PrecioPorNoche = 45000, Activa = true },
                    new Cabana { Nombre = "Sidharta 2", Descripcion = descripcionSidharta, Capacidad = 4, PrecioPorNoche = 45000, Activa = true },
                    new Cabana { Nombre = "Sidharta 3", Descripcion = descripcionSidharta, Capacidad = 4, PrecioPorNoche = 45000, Activa = true },
                    new Cabana { Nombre = "Sidharta 4", Descripcion = descripcionSidharta, Capacidad = 4, PrecioPorNoche = 45000, Activa = true },
                    new Cabana { Nombre = "Sidharta 5", Descripcion = descripcionSidharta, Capacidad = 4, PrecioPorNoche = 45000, Activa = true },
                    new Cabana { Nombre = "Maia", Descripcion = descripcionMaia, Capacidad = 6, PrecioPorNoche = 55000, Activa = true },
                };

                db.Cabanas.AddRange(cabanas);
            }

            db.SaveChanges();
        }
    }
}
