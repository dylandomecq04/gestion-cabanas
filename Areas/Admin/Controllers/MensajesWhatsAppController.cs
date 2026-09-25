using GestionCabanas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class MensajesWhatsAppController : Controller
    {
        private readonly MensajesWhatsAppService _mensajes;

        public MensajesWhatsAppController(MensajesWhatsAppService mensajes)
        {
            _mensajes = mensajes;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Textos = await _mensajes.ObtenerTextosAsync();
            return View(MensajesWhatsAppService.Definiciones);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(string clave, string? texto)
        {
            var definicion = MensajesWhatsAppService.Buscar(clave);
            if (definicion is null) return NotFound();

            if (string.IsNullOrWhiteSpace(texto))
            {
                TempData["Alerta"] = "El mensaje no puede quedar vacío. Si querés volver al texto original, usá «Restaurar original».";
                return RedirectToAction(nameof(Index));
            }

            await _mensajes.GuardarAsync(clave, texto.Replace("\r\n", "\n").Trim());
            TempData["Mensaje"] = $"Mensaje «{definicion.Titulo}» guardado.";
            return RedirectToAction(nameof(Index), null, definicion.Clave);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restaurar(string clave)
        {
            var definicion = MensajesWhatsAppService.Buscar(clave);
            if (definicion is null) return NotFound();

            await _mensajes.RestaurarAsync(clave);
            TempData["Mensaje"] = $"Mensaje «{definicion.Titulo}» restaurado al texto original.";
            return RedirectToAction(nameof(Index));
        }
    }
}
