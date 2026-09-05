using GestionCabanas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class SincronizacionController : Controller
    {
        private readonly GraphOneDriveService _oneDrive;
        private readonly ExcelReservasSyncService _sync;
        private readonly IConfiguration _config;

        public SincronizacionController(GraphOneDriveService oneDrive, ExcelReservasSyncService sync, IConfiguration config)
        {
            _oneDrive = oneDrive;
            _sync = sync;
            _config = config;
        }

        private IActionResult VolverAlCalendario(int? anio, int? mes)
            => RedirectToAction("Calendario", "Reservas", new { anio, mes });

        public IActionResult Conectar(int? anio, int? mes)
        {
            if (!_oneDrive.EstaConfigurado)
            {
                TempData["Mensaje"] = "Todavía falta configurar las credenciales de Microsoft (Client ID / Client Secret).";
                return VolverAlCalendario(anio, mes);
            }

            var estado = Guid.NewGuid().ToString("N");
            TempData["OAuthState"] = estado;
            TempData["OAuthAnio"] = anio;
            TempData["OAuthMes"] = mes;

            var redirectUri = Url.Action(nameof(Callback), "Sincronizacion", null, Request.Scheme)!;
            var url = _oneDrive.ConstruirUrlAutorizacion(redirectUri, estado);
            return Redirect(url);
        }

        public async Task<IActionResult> Callback(string? code, string? state, string? error, string? error_description)
        {
            var anio = TempData["OAuthAnio"] as int?;
            var mes = TempData["OAuthMes"] as int?;

            if (!string.IsNullOrEmpty(error))
            {
                TempData["Mensaje"] = $"Microsoft devolvió un error: {error_description ?? error}";
                return VolverAlCalendario(anio, mes);
            }

            var estadoEsperado = TempData["OAuthState"] as string;
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || state != estadoEsperado)
            {
                TempData["Mensaje"] = "No se pudo validar la respuesta de Microsoft. Probá conectar de nuevo.";
                return VolverAlCalendario(anio, mes);
            }

            try
            {
                var redirectUri = Url.Action(nameof(Callback), "Sincronizacion", null, Request.Scheme)!;
                await _oneDrive.IntercambiarCodigoAsync(code, redirectUri);
                TempData["Mensaje"] = "¡Cuenta de OneDrive conectada correctamente!";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = $"No se pudo completar la conexión: {ex.Message}";
            }

            return VolverAlCalendario(anio, mes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desconectar(int? anio, int? mes)
        {
            await _oneDrive.DesconectarAsync();
            TempData["Mensaje"] = "Se desconectó la cuenta de OneDrive.";
            return VolverAlCalendario(anio, mes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sincronizar(int anio, int? mes)
        {
            var urlArchivo = _config["OneDrive:ArchivoUrl"];
            if (string.IsNullOrWhiteSpace(urlArchivo))
            {
                TempData["Mensaje"] = "Falta configurar el link del archivo de OneDrive a sincronizar.";
                return VolverAlCalendario(anio, mes);
            }

            try
            {
                var modificado = await _oneDrive.ObtenerFechaModificacionAsync(urlArchivo);
                var bytes = await _oneDrive.DescargarArchivoCompartidoAsync(urlArchivo);
                var resultado = await _sync.SincronizarAsync(bytes, anio);
                await _oneDrive.MarcarSincronizadoAsync(modificado);

                TempData["ResultadoCreadas"] = resultado.Creadas;
                TempData["ResultadoActualizadas"] = resultado.Actualizadas;
                TempData["ResultadoOmitidas"] = resultado.Omitidas;
                TempData["ResultadoDetalleCreadas"] = resultado.DetalleCreadas.Count > 0
                    ? string.Join(" | ", resultado.DetalleCreadas)
                    : null;
                TempData["ResultadoDetalleActualizadas"] = resultado.DetalleActualizadas.Count > 0
                    ? string.Join(" | ", resultado.DetalleActualizadas)
                    : null;
                TempData["ResultadoDetalleOmitidas"] = resultado.DetalleOmitidas.Count > 0
                    ? string.Join(" | ", resultado.DetalleOmitidas)
                    : null;
                TempData["ResultadoEliminadas"] = resultado.Eliminadas;
                TempData["ResultadoDetalleEliminadas"] = resultado.DetalleEliminadas.Count > 0
                    ? string.Join(" | ", resultado.DetalleEliminadas)
                    : null;
                TempData["ResultadoNoInterpretadas"] = resultado.NoInterpretadas.Count > 0
                    ? string.Join(" | ", resultado.NoInterpretadas)
                    : null;
                TempData["ResultadoCabanasNoEncontradas"] = resultado.CabanasNoEncontradas.Count > 0
                    ? string.Join(", ", resultado.CabanasNoEncontradas)
                    : null;
                TempData["ResultadoSuperposiciones"] = resultado.Superposiciones.Count > 0
                    ? string.Join(" | ", resultado.Superposiciones)
                    : null;
                TempData["Mensaje"] = $"Sincronización terminada: {Plural(resultado.Creadas, "reserva nueva", "reservas nuevas")}, " +
                    $"{Plural(resultado.Actualizadas, "actualizada", "actualizadas")}, {Plural(resultado.Omitidas, "sin cambios", "sin cambios")}, " +
                    $"{Plural(resultado.Eliminadas, "eliminada", "eliminadas")}.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = $"No se pudo sincronizar: {ex.Message}";
            }

            return VolverAlCalendario(anio, mes);
        }

        private static string Plural(int cantidad, string singular, string plural)
            => $"{cantidad} {(cantidad == 1 ? singular : plural)}";
    }
}
