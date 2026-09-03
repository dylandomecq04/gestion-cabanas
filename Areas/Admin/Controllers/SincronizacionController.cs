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

        public async Task<IActionResult> Index()
        {
            ViewBag.Configurado = _oneDrive.EstaConfigurado;
            ViewBag.Conexion = await _oneDrive.ObtenerConexionAsync();
            ViewBag.AnioSugerido = DateTime.Today.Year;
            return View();
        }

        public IActionResult Conectar()
        {
            if (!_oneDrive.EstaConfigurado)
            {
                TempData["Mensaje"] = "Todavía falta configurar las credenciales de Microsoft (Client ID / Client Secret).";
                return RedirectToAction(nameof(Index));
            }

            var estado = Guid.NewGuid().ToString("N");
            TempData["OAuthState"] = estado;

            var redirectUri = Url.Action(nameof(Callback), "Sincronizacion", null, Request.Scheme)!;
            var url = _oneDrive.ConstruirUrlAutorizacion(redirectUri, estado);
            return Redirect(url);
        }

        public async Task<IActionResult> Callback(string? code, string? state, string? error, string? error_description)
        {
            if (!string.IsNullOrEmpty(error))
            {
                TempData["Mensaje"] = $"Microsoft devolvió un error: {error_description ?? error}";
                return RedirectToAction(nameof(Index));
            }

            var estadoEsperado = TempData["OAuthState"] as string;
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || state != estadoEsperado)
            {
                TempData["Mensaje"] = "No se pudo validar la respuesta de Microsoft. Probá conectar de nuevo.";
                return RedirectToAction(nameof(Index));
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

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desconectar()
        {
            await _oneDrive.DesconectarAsync();
            TempData["Mensaje"] = "Se desconectó la cuenta de OneDrive.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sincronizar(int anio)
        {
            var urlArchivo = _config["OneDrive:ArchivoUrl"];
            if (string.IsNullOrWhiteSpace(urlArchivo))
            {
                TempData["Mensaje"] = "Falta configurar el link del archivo de OneDrive a sincronizar.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var bytes = await _oneDrive.DescargarArchivoCompartidoAsync(urlArchivo);
                var resultado = await _sync.SincronizarAsync(bytes, anio);

                TempData["ResultadoCreadas"] = resultado.Creadas;
                TempData["ResultadoOmitidas"] = resultado.Omitidas;
                TempData["ResultadoNoInterpretadas"] = resultado.NoInterpretadas.Count > 0
                    ? string.Join(" | ", resultado.NoInterpretadas)
                    : null;
                TempData["ResultadoCabanasNoEncontradas"] = resultado.CabanasNoEncontradas.Count > 0
                    ? string.Join(", ", resultado.CabanasNoEncontradas)
                    : null;
                TempData["Mensaje"] = $"Sincronización terminada: {resultado.Creadas} reserva(s) nueva(s), {resultado.Omitidas} ya existían.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = $"No se pudo sincronizar: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
