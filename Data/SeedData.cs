using GestionCabanas.Models;
using GestionCabanas.Services;
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
                // Están en el orden en que se ubican en el complejo, una al lado de la otra.
                var nombres = new[] { "Sidharta 1", "Sidharta 2", "Sidharta 3", "Maia", "Sidharta 5" };
                for (var i = 0; i < nombres.Length; i++)
                {
                    db.Cabanas.Add(new Cabana
                    {
                        Nombre = nombres[i],
                        Capacidad = nombres[i] == "Maia" ? 6 : 4,
                        Activa = true,
                        Orden = i + 1
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

            if (!db.InicioSitio.Any())
            {
                db.InicioSitio.Add(new InicioSitio
                {
                    TextoEyebrow = "Cabañas Sidharta · Delta Tigre",
                    Titulo = "Tu escapada a la naturaleza"
                });
            }

            if (!db.FotosHero.Any())
            {
                var archivosHero = new[]
                {
                    "hero-atardecer.jpg", "hero-sendero.webp", "hero-otono.jpg", "hero-arco.webp",
                    "hero-picnic.jpg", "hero-refugio.webp", "hero-naranjo.webp"
                };
                for (var i = 0; i < archivosHero.Length; i++)
                {
                    db.FotosHero.Add(new FotoHero { RutaArchivo = $"/images/hero/{archivosHero[i]}", Orden = i });
                }
            }

            NormalizarNombresExistentes(db, logger);

            db.SaveChanges();
        }

        // Las reservas cargadas antes de que los nombres se normalizaran al guardar quedan con el
        // formato que tenían (todo en minúscula, todo en mayúscula, etc.). Idempotente: una vez
        // corregidas no toca nada.
        private static void NormalizarNombresExistentes(ApplicationDbContext db, ILogger logger)
        {
            var corregidas = 0;
            foreach (var reserva in db.Reservas.ToList())
            {
                var nombre = NombresPropios.Formatear(reserva.NombreHuesped);
                if (reserva.NombreHuesped != nombre)
                {
                    reserva.NombreHuesped = nombre;
                    corregidas++;
                }
            }

            if (corregidas == 0)
            {
                return;
            }

            logger.LogInformation("Se normalizaron los nombres de {Cantidad} reservas.", corregidas);

            // Sin esto la sincronización automática no vuelve a leer el Excel hasta que alguien lo
            // modifique, y los nombres de sus celdas quedarían como estaban.
            var conexion = db.OneDriveConexiones.FirstOrDefault();
            if (conexion is not null)
            {
                conexion.UltimaModificacionExcelVista = null;
            }
        }
    }
}
