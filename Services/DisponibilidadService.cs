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
        /// Guarda los cambios de precio/bloqueo del calendario. Cuando el conjunto de días
        /// bloqueados de una cabaña cambia, reconcilia los RANGOS contiguos de bloqueo (no día por
        /// día): un tramo de varios días bloqueados seguidos ocupa una sola fila en el Excel, como
        /// si fuera una reserva a nombre de "Bloqueada" con PAGÓ y PAGAR en 0. Devuelve los avisos
        /// de Excel que no se pudieron aplicar.
        /// </summary>
        public async Task<List<string>> GuardarTarifasAsync(IEnumerable<TarifaDiaInput> dias)
        {
            var avisos = new List<string>();
            var listaDias = dias.ToList();
            if (listaDias.Count == 0)
            {
                return avisos;
            }

            var fechaMin = listaDias.Min(d => d.Fecha.Date);
            var fechaMax = listaDias.Max(d => d.Fecha.Date);

            var existentes = await _db.TarifasDias
                .Where(t => t.Fecha >= fechaMin && t.Fecha <= fechaMax)
                .ToListAsync();
            var nombresCabanas = await _db.Cabanas.ToDictionaryAsync(c => c.Id, c => c.Nombre);

            foreach (var grupoCabana in listaDias.GroupBy(d => d.CabanaId))
            {
                var cabanaId = grupoCabana.Key;
                var existentesDelMes = existentes.Where(t => t.CabanaId == cabanaId).ToDictionary(t => t.Fecha.Date, t => t);
                var bloqueadosAntes = existentesDelMes.Where(kv => kv.Value.Bloqueada).Select(kv => kv.Key).ToHashSet();
                var bloqueadosDespues = new HashSet<DateTime>(bloqueadosAntes);
                var registrosPorFecha = new Dictionary<DateTime, TarifaDia>(existentesDelMes);

                foreach (var dia in grupoCabana)
                {
                    var fecha = dia.Fecha.Date;
                    existentesDelMes.TryGetValue(fecha, out var existente);
                    var necesitaFila = dia.Bloqueada || dia.Precio.HasValue;

                    if (!necesitaFila)
                    {
                        if (existente is not null)
                        {
                            _db.TarifasDias.Remove(existente);
                            registrosPorFecha.Remove(fecha);
                        }
                        bloqueadosDespues.Remove(fecha);
                        continue;
                    }

                    TarifaDia registro;
                    if (existente is null)
                    {
                        registro = new TarifaDia { CabanaId = cabanaId, Fecha = fecha, Precio = dia.Precio, Bloqueada = dia.Bloqueada };
                        _db.TarifasDias.Add(registro);
                    }
                    else
                    {
                        registro = existente;
                        registro.Precio = dia.Precio;
                        registro.Bloqueada = dia.Bloqueada;
                    }
                    registrosPorFecha[fecha] = registro;

                    if (dia.Bloqueada)
                    {
                        bloqueadosDespues.Add(fecha);
                    }
                    else
                    {
                        bloqueadosDespues.Remove(fecha);
                    }
                }

                if (bloqueadosAntes.SetEquals(bloqueadosDespues) || !nombresCabanas.TryGetValue(cabanaId, out var nombreCabana))
                {
                    continue;
                }

                var rangosAntes = CalcularRangosBloqueados(bloqueadosAntes);
                var rangosDespues = CalcularRangosBloqueados(bloqueadosDespues);
                var rangosAntesSet = rangosAntes.ToHashSet();
                var rangosDespuesSet = rangosDespues.ToHashSet();

                // Limpiar los rangos viejos que ya no existen tal cual (se acortaron, se estiraron,
                // se dividieron o se desbloquearon del todo).
                foreach (var rango in rangosAntes)
                {
                    if (rangosDespuesSet.Contains(rango))
                    {
                        continue;
                    }

                    var conUbicacion = EnumerarFechas(rango.Inicio, rango.FinExclusivo)
                        .Select(f => existentesDelMes.TryGetValue(f, out var t) ? t : null)
                        .FirstOrDefault(t => !string.IsNullOrEmpty(t?.ExcelUbicacion));

                    if (conUbicacion is not null)
                    {
                        var aviso = await _excelEscritura.LimpiarBloqueoAsync(conUbicacion, nombreCabana);
                        if (aviso is not null) avisos.Add(aviso);
                    }

                    foreach (var f in EnumerarFechas(rango.Inicio, rango.FinExclusivo))
                    {
                        if (registrosPorFecha.TryGetValue(f, out var reg))
                        {
                            reg.ExcelUbicacion = null;
                        }
                    }
                }

                // Escribir los rangos nuevos que no existían tal cual antes.
                foreach (var rango in rangosDespues)
                {
                    if (rangosAntesSet.Contains(rango) || !registrosPorFecha.TryGetValue(rango.Inicio, out var representante))
                    {
                        continue;
                    }

                    var aviso = await _excelEscritura.EscribirBloqueoAsync(representante, rango.FinExclusivo, nombreCabana);
                    if (aviso is not null)
                    {
                        avisos.Add(aviso);
                        continue;
                    }

                    foreach (var f in EnumerarFechas(rango.Inicio, rango.FinExclusivo))
                    {
                        if (registrosPorFecha.TryGetValue(f, out var reg))
                        {
                            reg.ExcelUbicacion = representante.ExcelUbicacion;
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();
            return avisos;
        }

        /// <summary>
        /// Agrupa un conjunto de días bloqueados en tramos contiguos (ej. 5,6,7 -> un solo tramo
        /// del 5 al 8 exclusivo, como el FechaHasta de una reserva).
        /// </summary>
        private static List<(DateTime Inicio, DateTime FinExclusivo)> CalcularRangosBloqueados(IEnumerable<DateTime> diasBloqueados)
        {
            var rangos = new List<(DateTime, DateTime)>();
            DateTime? inicio = null;
            DateTime? anterior = null;

            foreach (var fecha in diasBloqueados.OrderBy(f => f))
            {
                if (inicio is null)
                {
                    inicio = fecha;
                }
                else if (fecha != anterior!.Value.AddDays(1))
                {
                    rangos.Add((inicio.Value, anterior.Value.AddDays(1)));
                    inicio = fecha;
                }
                anterior = fecha;
            }

            if (inicio is not null)
            {
                rangos.Add((inicio.Value, anterior!.Value.AddDays(1)));
            }

            return rangos;
        }

        private static IEnumerable<DateTime> EnumerarFechas(DateTime inicio, DateTime finExclusivo)
        {
            for (var f = inicio; f < finExclusivo; f = f.AddDays(1))
            {
                yield return f;
            }
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
