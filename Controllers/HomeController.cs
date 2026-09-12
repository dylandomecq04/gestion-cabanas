using System.Diagnostics;
using GestionCabanas.Data;
using Microsoft.AspNetCore.Mvc;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var cabanas = await _db.Cabanas
            .Where(c => c.Activa)
            .Include(c => c.Fotos)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        var hoy = DateTime.Today;
        var promosVigentes = await _db.PromosEstadia
            .Where(p => p.Activa && p.FechaDesde <= hoy && p.FechaHasta >= hoy)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        var promosParaHome = promosVigentes
            .GroupBy(p => new { p.Nombre, p.Descripcion, p.FechaDesde, p.FechaHasta })
            .Select(g => g.First())
            .Take(2)
            .ToList();

        ViewBag.PromoIzquierda = promosParaHome.ElementAtOrDefault(0);
        ViewBag.PromoDerecha = promosParaHome.ElementAtOrDefault(1);

        return View(cabanas);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public async Task<IActionResult> Informacion()
    {
        var info = await _db.InformacionSitio.FirstOrDefaultAsync() ?? new InformacionSitio();
        var preguntas = await _db.PreguntasFrecuentes.OrderBy(p => p.Orden).ToListAsync();

        ViewBag.Preguntas = preguntas;
        return View(info);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
