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
        return View(cabanas);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terminos()
    {
        return View();
    }

    public IActionResult ComoLlegar()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
