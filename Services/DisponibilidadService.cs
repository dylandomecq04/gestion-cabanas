using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Services
{
    public class DisponibilidadService
    {
        private readonly ApplicationDbContext _db;
        private readonly ExcelEscrituraService _excelEscritura;

        public DisponibilidadService(ApplicationDbContext db, ExcelEscrituraService excelEscritura)
        {
            _db = db;
            _excelEscritura = excelEscritura;
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

        /// <summary>
        /// Guarda los cambios de precio/bloqueo del calendario. Cuando un día pasa a estar
        /// bloqueado (o deja de estarlo) refleja ese cambio en el Excel como si fuera una reserva
        /// a nombre de "Bloqueada" con PAGÓ y PAGAR en 0, reusando el mismo mecanismo de las
        /// reservas reales. Devuelve los avisos de Excel que no se pudieron aplicar.
        /// </summary>
        public async Task<List<string>> GuardarTarifasAsync(IEnumerable<TarifaDiaInput> dias)
        {
            var avisos = new List<string>();
            Dictionary<int, string>? nombresCabanas = null;

            foreach (var dia in dias)
            {
                var existente = await _db.TarifasDias.FirstOrDefaultAsync(t => t.CabanaId == dia.CabanaId && t.Fecha.Date == dia.Fecha.Date);
                var necesitaFila = dia.Bloqueada || dia.Precio.HasValue;
                var estabaBloqueada = existente?.Bloqueada ?? false;

                if (!necesitaFila)
                {
                    if (existente is not null)
                    {
                        if (estabaBloqueada)
                        {
                            nombresCabanas ??= await _db.Cabanas.ToDictionaryAsync(c => c.Id, c => c.Nombre);
                            if (nombresCabanas.TryGetValue(dia.CabanaId, out var nombreCabana))
                            {
                                var aviso = await _excelEscritura.LimpiarBloqueoAsync(existente, nombreCabana);
                                if (aviso is not null) avisos.Add(aviso);
                            }
                        }
                        _db.TarifasDias.Remove(existente);
                    }
                    continue;
                }

                TarifaDia registro;
                if (existente is null)
                {
                    registro = new TarifaDia
                    {
                        CabanaId = dia.CabanaId,
                        Fecha = dia.Fecha.Date,
                        Precio = dia.Precio,
                        Bloqueada = dia.Bloqueada
                    };
                    _db.TarifasDias.Add(registro);
                }
                else
                {
                    registro = existente;
                    registro.Precio = dia.Precio;
                    registro.Bloqueada = dia.Bloqueada;
                }

                if (dia.Bloqueada != estabaBloqueada)
                {
                    nombresCabanas ??= await _db.Cabanas.ToDictionaryAsync(c => c.Id, c => c.Nombre);
                    if (nombresCabanas.TryGetValue(dia.CabanaId, out var nombreCabana))
                    {
                        if (dia.Bloqueada)
                        {
                            var aviso = await _excelEscritura.EscribirBloqueoAsync(registro, nombreCabana);
                            if (aviso is not null) avisos.Add(aviso);
                        }
                        else
                        {
                            var aviso = await _excelEscritura.LimpiarBloqueoAsync(registro, nombreCabana);
                            if (aviso is not null) avisos.Add(aviso);
                            registro.ExcelUbicacion = null;
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();
            return avisos;
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
