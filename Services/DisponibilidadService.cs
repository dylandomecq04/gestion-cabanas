using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Services
{
    public class DisponibilidadService
    {
        private readonly ApplicationDbContext _db;

        public DisponibilidadService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<bool> HaySuperposicionAsync(int cabanaId, DateTime desde, DateTime hasta, int? reservaIdExcluir = null)
        {
            var query = _db.Reservas.Where(r =>
                r.CabanaId == cabanaId &&
                r.Estado == EstadoReserva.Confirmada &&
                r.FechaDesde < hasta &&
                r.FechaHasta > desde);

            if (reservaIdExcluir.HasValue)
            {
                query = query.Where(r => r.Id != reservaIdExcluir.Value);
            }

            if (await query.AnyAsync())
            {
                return true;
            }

            return await _db.TarifasDias.AnyAsync(t =>
                t.CabanaId == cabanaId &&
                t.Bloqueada &&
                t.Fecha >= desde &&
                t.Fecha < hasta);
        }

        public async Task<List<Reserva>> ObtenerConfirmadasAsync(int cabanaId, DateTime? desde = null)
        {
            var query = _db.Reservas.Where(r => r.CabanaId == cabanaId && r.Estado == EstadoReserva.Confirmada);
            if (desde.HasValue)
            {
                query = query.Where(r => r.FechaHasta >= desde.Value);
            }
            return await query.OrderBy(r => r.FechaDesde).ToListAsync();
        }

        public async Task<List<Reserva>> ObtenerConfirmadasEnRangoAsync(DateTime desde, DateTime hasta, int? cabanaId = null)
        {
            var query = _db.Reservas.Where(r =>
                r.Estado == EstadoReserva.Confirmada &&
                r.FechaDesde <= hasta &&
                r.FechaHasta >= desde);

            if (cabanaId.HasValue)
            {
                query = query.Where(r => r.CabanaId == cabanaId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<List<TarifaDia>> ObtenerTarifasEnRangoAsync(int cabanaId, DateTime desde, DateTime hasta)
        {
            return await _db.TarifasDias
                .Where(t => t.CabanaId == cabanaId && t.Fecha >= desde && t.Fecha <= hasta)
                .ToListAsync();
        }

        public async Task<List<TarifaDia>> ObtenerTarifasEnRangoTodasCabanasAsync(DateTime desde, DateTime hasta)
        {
            return await _db.TarifasDias
                .Where(t => t.Fecha >= desde && t.Fecha <= hasta)
                .ToListAsync();
        }

        public async Task GuardarTarifasAsync(IEnumerable<TarifaDiaInput> dias)
        {
            foreach (var dia in dias)
            {
                var existente = await _db.TarifasDias.FirstOrDefaultAsync(t => t.CabanaId == dia.CabanaId && t.Fecha.Date == dia.Fecha.Date);
                var necesitaFila = dia.Bloqueada || dia.Precio.HasValue;

                if (!necesitaFila)
                {
                    if (existente is not null)
                    {
                        _db.TarifasDias.Remove(existente);
                    }
                    continue;
                }

                if (existente is null)
                {
                    _db.TarifasDias.Add(new TarifaDia
                    {
                        CabanaId = dia.CabanaId,
                        Fecha = dia.Fecha.Date,
                        Precio = dia.Precio,
                        Bloqueada = dia.Bloqueada
                    });
                }
                else
                {
                    existente.Precio = dia.Precio;
                    existente.Bloqueada = dia.Bloqueada;
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task<List<TarifaDia>> ObtenerBloqueadasEnRangoAsync(DateTime desde, DateTime hasta, int? cabanaId = null)
        {
            var query = _db.TarifasDias.Where(t => t.Bloqueada && t.Fecha >= desde && t.Fecha <= hasta);
            if (cabanaId.HasValue)
            {
                query = query.Where(t => t.CabanaId == cabanaId.Value);
            }
            return await query.ToListAsync();
        }

        public async Task<decimal?> CalcularValorTotalAsync(int cabanaId, DateTime desde, DateTime hasta, decimal? precioBase)
        {
            if (hasta <= desde)
            {
                return null;
            }

            var tarifas = await ObtenerTarifasEnRangoAsync(cabanaId, desde, hasta.AddDays(-1));
            decimal total = 0;

            for (var dia = desde; dia < hasta; dia = dia.AddDays(1))
            {
                var precioDelDia = tarifas.FirstOrDefault(t => t.Fecha.Date == dia.Date)?.Precio ?? precioBase;
                if (!precioDelDia.HasValue)
                {
                    return null;
                }
                total += precioDelDia.Value;
            }

            return total;
        }
    }
}
