using GestionCabanas.Data;
using GestionCabanas.Models;
using GestionCabanas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class GaleriaController : Controller
    {
        private const long LimiteBytesReel = 100L * 1024 * 1024;
        private const long LimiteBytesRequest = 209_715_200; // el mismo tope que Kestrel/IIS

        private static readonly Dictionary<string, string> TiposFoto = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

        // Los celulares suelen mandar los videos como application/octet-stream, así que el
        // Content-Type se deduce de la extensión para que el navegador los reproduzca.
        private static readonly Dictionary<string, string> TiposReel = new(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".m4v"] = "video/mp4",
            [".mov"] = "video/quicktime",
            [".webm"] = "video/webm"
        };

        private readonly ApplicationDbContext _db;
        private readonly AlmacenamientoFotosService _almacenamiento;

        public GaleriaController(ApplicationDbContext db, AlmacenamientoFotosService almacenamiento)
        {
            _db = db;
            _almacenamiento = almacenamiento;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _db.ItemsGaleria.OrderByDescending(i => i.Id).ToListAsync();
            ViewBag.Fotos = items.Where(i => i.Tipo == TipoItemGaleria.Foto).ToList();
            ViewBag.Reels = items.Where(i => i.Tipo == TipoItemGaleria.Reel).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(LimiteBytesRequest)]
        [RequestFormLimits(MultipartBodyLengthLimit = LimiteBytesRequest)]
        public Task<IActionResult> SubirFotos(List<IFormFile> archivos) =>
            SubirAsync(archivos, TipoItemGaleria.Foto);

        // El panel sube los reels de a uno (con barra de progreso real), por eso también
        // responde en JSON cuando lo llama el XHR del navegador.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(LimiteBytesRequest)]
        [RequestFormLimits(MultipartBodyLengthLimit = LimiteBytesRequest)]
        public Task<IActionResult> SubirReels(List<IFormFile> archivos) =>
            SubirAsync(archivos, TipoItemGaleria.Reel);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(List<int> ids)
        {
            if (ids is { Count: > 0 })
            {
                var items = await _db.ItemsGaleria.Where(i => ids.Contains(i.Id)).ToListAsync();
                foreach (var item in items)
                {
                    await _almacenamiento.EliminarAsync(item.RutaArchivo);
                }
                _db.ItemsGaleria.RemoveRange(items);
                await _db.SaveChangesAsync();
                TempData["Mensaje"] = items.Count == 1 ? "Se eliminó 1 elemento." : $"Se eliminaron {items.Count} elementos.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> SubirAsync(List<IFormFile>? archivos, TipoItemGaleria tipo)
        {
            var esFoto = tipo == TipoItemGaleria.Foto;
            var permitidos = esFoto ? TiposFoto : TiposReel;
            var carpeta = esFoto ? "galeria/fotos" : "galeria/reels";
            var etiqueta = esFoto ? "foto" : "reel";
            var origenAjax = Request.Headers.XRequestedWith == "XMLHttpRequest";

            var subidos = 0;
            var rechazados = new List<string>();

            if (!_almacenamiento.Configurado)
            {
                return Responder(origenAjax, false, "El almacenamiento de archivos no está configurado en este entorno, así que no se puede subir nada.");
            }

            foreach (var archivo in archivos ?? new List<IFormFile>())
            {
                if (archivo.Length == 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(archivo.FileName);
                if (!permitidos.TryGetValue(extension, out var contentType))
                {
                    rechazados.Add($"\"{archivo.FileName}\" no es un formato válido para {(esFoto ? "fotos (JPG, PNG o WEBP)" : "reels (MP4, MOV o WEBM)")}");
                    continue;
                }

                if (!esFoto && archivo.Length > LimiteBytesReel)
                {
                    rechazados.Add($"\"{archivo.FileName}\" pesa {archivo.Length / 1024 / 1024} MB y el máximo por reel es {LimiteBytesReel / 1024 / 1024} MB");
                    continue;
                }

                using var stream = archivo.OpenReadStream();
                var url = await _almacenamiento.SubirAsync(stream, $"{carpeta}/{Guid.NewGuid()}{extension.ToLowerInvariant()}", contentType);

                _db.ItemsGaleria.Add(new ItemGaleria { Tipo = tipo, RutaArchivo = url });
                await _db.SaveChangesAsync();
                subidos++;
            }

            var mensaje = subidos switch
            {
                0 => null,
                1 => $"Se subió 1 {etiqueta}.",
                _ => esFoto ? $"Se subieron {subidos} fotos." : $"Se subieron {subidos} reels."
            };

            if (rechazados.Count > 0)
            {
                var detalle = string.Join(". ", rechazados) + ".";
                return Responder(origenAjax, false, mensaje is null ? detalle : $"{mensaje} Pero: {detalle}");
            }

            if (mensaje is null)
            {
                return Responder(origenAjax, false, $"No elegiste ningún {etiqueta} para subir.");
            }

            return Responder(origenAjax, true, mensaje);
        }

        // Con el formulario común el resultado va en TempData; el fetch del navegador recibe el
        // mensaje en JSON porque junta el resultado de varios reels antes de mostrarlo.
        private IActionResult Responder(bool origenAjax, bool ok, string mensaje)
        {
            if (origenAjax)
            {
                return Json(new { ok, mensaje });
            }

            TempData[ok ? "Mensaje" : "Alerta"] = mensaje;
            return RedirectToAction(nameof(Index));
        }
    }
}
