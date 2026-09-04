using Microsoft.EntityFrameworkCore;
using GestionCabanas.Data;

namespace GestionCabanas.Services
{
    public class SincronizacionAutomaticaService : BackgroundService
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SincronizacionAutomaticaService> _logger;

        public SincronizacionAutomaticaService(IServiceScopeFactory scopeFactory, ILogger<SincronizacionAutomaticaService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Intervalo);

            do
            {
                await RevisarYSincronizarAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RevisarYSincronizarAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var oneDrive = scope.ServiceProvider.GetRequiredService<GraphOneDriveService>();
            var sync = scope.ServiceProvider.GetRequiredService<ExcelReservasSyncService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            if (!oneDrive.EstaConfigurado)
            {
                return;
            }

            var urlArchivo = config["OneDrive:ArchivoUrl"];
            if (string.IsNullOrWhiteSpace(urlArchivo))
            {
                return;
            }

            var conexion = await db.OneDriveConexiones.FirstOrDefaultAsync(stoppingToken);
            if (conexion?.RefreshTokenCifrado is null)
            {
                return;
            }

            try
            {
                var modificado = await oneDrive.ObtenerFechaModificacionAsync(urlArchivo);
                if (modificado is null)
                {
                    return;
                }

                if (conexion.UltimaModificacionExcelVista.HasValue &&
                    modificado.Value <= conexion.UltimaModificacionExcelVista.Value)
                {
                    return;
                }

                var bytes = await oneDrive.DescargarArchivoCompartidoAsync(urlArchivo);
                var resultado = await sync.SincronizarAsync(bytes, DateTime.Today.Year);
                await oneDrive.MarcarSincronizadoAsync(modificado);

                _logger.LogInformation(
                    "Sincronización automática: {Creadas} nuevas, {Actualizadas} actualizadas, {Omitidas} sin cambios.",
                    resultado.Creadas, resultado.Actualizadas, resultado.Omitidas);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo completar la sincronización automática con OneDrive.");
            }
        }
    }
}
