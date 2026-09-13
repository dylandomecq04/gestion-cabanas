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

        ViewBag.Inicio = await _db.InicioSitio.FirstOrDefaultAsync() ?? new InicioSitio();
        ViewBag.FotosHero = await _db.FotosHero.OrderBy(f => f.Orden).ToListAsync();

        return View();
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
