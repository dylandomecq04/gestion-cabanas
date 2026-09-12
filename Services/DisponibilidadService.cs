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
            var detalle = await CalcularValorConDetalleAsync(cabanaId, desde, hasta, precioBase);
            return detalle.Total;
        }

        /// <summary>
        /// Calcula el valor total de una estadía y, si corresponde, aplica el paquete de precios
        /// de una promoción por cantidad de noches (1, 2 o 3) para ese rango. Una promo sólo se
        /// aplica si cubre TODAS las noches de la estadía. Para estadías de más de 3 noches con
        /// promo activa, las primeras 3 noches se cobran al precio del paquete de 3 noches y el
        /// resto a precio normal.
        /// </summary>
        public async Task<ResultadoPrecio> CalcularValorConDetalleAsync(int cabanaId, DateTime desde, DateTime hasta, decimal? precioBase)
        {
            var resultado = new ResultadoPrecio();
            if (hasta <= desde)
            {
                return resultado;
            }

            var noches = (hasta - desde).Days;
            var promo = await BuscarPromoCubriendoAsync(cabanaId, desde, hasta);

            if (promo is not null)
            {
                if (noches <= 3)
                {
                    var precioPaquete = ObtenerPrecioPaquete(promo, noches);
                    if (precioPaquete.HasValue)
                    {
                        resultado.Total = precioPaquete;
                        resultado.PromoAplicada = true;
                        resultado.EtiquetaPromo = EtiquetaPaquete(promo, noches);
                        return resultado;
                    }
                }
                else if (promo.Precio3Noches.HasValue)
                {
                    var totalResto = await SumaDiariaAsync(cabanaId, desde.AddDays(3), hasta, precioBase);
                    resultado.Total = totalResto.HasValue ? promo.Precio3Noches.Value + totalResto.Value : null;
                    resultado.PromoAplicada = true;
                    resultado.EtiquetaPromo = EtiquetaPaquete(promo, 3);
                    return resultado;
                }
            }

            resultado.Total = await SumaDiariaAsync(cabanaId, desde, hasta, precioBase);
            return resultado;
        }

        private async Task<PromoEstadia?> BuscarPromoCubriendoAsync(int cabanaId, DateTime desde, DateTime hasta)
        {
            var ultimaNoche = hasta.AddDays(-1).Date;
            return await _db.PromosEstadia
                .Where(p => p.CabanaId == cabanaId && p.Activa && p.FechaDesde.Date <= desde.Date && p.FechaHasta.Date >= ultimaNoche)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
        }

        private static decimal? ObtenerPrecioPaquete(PromoEstadia promo, int noches) => noches switch
        {
            1 => promo.Precio1Noche,
            2 => promo.Precio2Noches,
            3 => promo.Precio3Noches,
            _ => null
        };

        private static string EtiquetaPaquete(PromoEstadia promo, int noches)
        {
            var nochesTexto = noches == 1 ? "1 noche" : $"{noches} noches";
            return string.IsNullOrWhiteSpace(promo.Nombre) ? $"Promo {nochesTexto}" : $"{promo.Nombre} · {nochesTexto}";
        }

        private async Task<decimal?> SumaDiariaAsync(int cabanaId, DateTime desde, DateTime hasta, decimal? precioBase)
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

        public async Task<List<PromoEstadia>> ObtenerPromosEnRangoAsync(int cabanaId, DateTime desde, DateTime hasta)
        {
            return await _db.PromosEstadia
                .Where(p => p.CabanaId == cabanaId && p.Activa && p.FechaDesde <= hasta && p.FechaHasta >= desde)
                .ToListAsync();
        }

        public async Task<List<PromoEstadia>> ObtenerPromosEnRangoTodasCabanasAsync(DateTime desde, DateTime hasta)
        {
            return await _db.PromosEstadia
                .Where(p => p.Activa && p.FechaDesde <= hasta && p.FechaHasta >= desde)
                .ToListAsync();
        }

        /// <summary>
        /// Busca, para un rango de fechas y una cantidad de personas, las opciones de reserva
        /// posibles: cabañas individuales que cubran todo el rango, o -si ninguna lo cubre sola-
        /// todas las combinaciones válidas que usan la menor cantidad de cabañas posible.
        /// </summary>
        public async Task<ResultadoBusquedaDisponibilidad> BuscarOpcionesAsync(DateTime desde, DateTime hasta, int personas)
        {
            var resultado = new ResultadoBusquedaDisponibilidad();

            if (hasta <= desde)
            {
                resultado.Mensaje = "El rango de fechas no es válido.";
                return resultado;
            }

            var noches = (hasta - desde).Days;
            if (noches > 45)
            {
                resultado.Mensaje = "Elegí un rango de hasta 45 noches.";
                return resultado;
            }

            var cabanas = await _db.Cabanas
                .Where(c => c.Activa && c.Capacidad >= personas)
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            var fechas = Enumerable.Range(0, noches).Select(i => desde.AddDays(i)).ToList();

            if (cabanas.Count == 0)
            {
                resultado.CobreTotal = false;
                resultado.DiasSinCobertura = fechas;
                resultado.Mensaje = "Ninguna cabaña tiene capacidad para esa cantidad de personas.";
                return resultado;
            }

            var reservas = await ObtenerConfirmadasEnRangoAsync(desde, hasta.AddDays(-1));
            var bloqueadas = await ObtenerBloqueadasEnRangoAsync(desde, hasta.AddDays(-1));

            bool EstaOcupada(int cabanaId, DateTime dia) => reservas.Any(r => r.CabanaId == cabanaId && r.FechaDesde <= dia && dia < r.FechaHasta);
            bool EstaBloqueada(int cabanaId, DateTime dia) => bloqueadas.Any(b => b.CabanaId == cabanaId && b.Fecha.Date == dia.Date);

            var libre = new bool[cabanas.Count, noches];
            for (var c = 0; c < cabanas.Count; c++)
            {
                for (var n = 0; n < noches; n++)
                {
                    libre[c, n] = !EstaOcupada(cabanas[c].Id, fechas[n]) && !EstaBloqueada(cabanas[c].Id, fechas[n]);
                }
            }

            var sinCobertura = new List<DateTime>();
            for (var n = 0; n < noches; n++)
            {
                if (!Enumerable.Range(0, cabanas.Count).Any(c => libre[c, n]))
                {
                    sinCobertura.Add(fechas[n]);
                }
            }

            if (sinCobertura.Count > 0)
            {
                resultado.CobreTotal = false;
                resultado.DiasSinCobertura = sinCobertura;
                return resultado;
            }

            List<int> CabanasQueCubren(int inicio, int finExclusivo)
            {
                var lista = new List<int>();
                for (var c = 0; c < cabanas.Count; c++)
                {
                    var cubre = true;
                    for (var n = inicio; n < finExclusivo; n++)
                    {
                        if (!libre[c, n])
                        {
                            cubre = false;
                            break;
                        }
                    }
                    if (cubre)
                    {
                        lista.Add(c);
                    }
                }
                return lista;
            }

            var combos = new List<List<(int Ini, int Fin, int CabanaIndex)>>();

            void Generar(int inicio, int tramosRestantes, List<(int Ini, int Fin, int CabanaIndex)> actual)
            {
                if (tramosRestantes == 1)
                {
                    foreach (var c in CabanasQueCubren(inicio, noches))
                    {
                        if (actual.Count > 0 && actual[^1].CabanaIndex == c)
                        {
                            continue;
                        }
                        combos.Add(new List<(int, int, int)>(actual) { (inicio, noches, c) });
                    }
                    return;
                }

                for (var fin = inicio + 1; fin <= noches - (tramosRestantes - 1); fin++)
                {
                    foreach (var c in CabanasQueCubren(inicio, fin))
                    {
                        if (actual.Count > 0 && actual[^1].CabanaIndex == c)
                        {
                            continue;
                        }
                        actual.Add((inicio, fin, c));
                        Generar(fin, tramosRestantes - 1, actual);
                        actual.RemoveAt(actual.Count - 1);
                    }
                }
            }

            var maxK = Math.Min(cabanas.Count, noches);
            for (var k = 1; k <= maxK; k++)
            {
                combos.Clear();
                Generar(0, k, new List<(int, int, int)>());
                if (combos.Count > 0)
                {
                    break;
                }
            }

            resultado.CobreTotal = true;

            foreach (var combo in combos)
            {
                var opcion = new OpcionReserva();
                decimal? total = 0;

                foreach (var (ini, fin, cabanaIndex) in combo)
                {
                    var cabana = cabanas[cabanaIndex];
                    var segDesde = desde.AddDays(ini);
                    var segHasta = desde.AddDays(fin);
                    var subtotal = await CalcularValorTotalAsync(cabana.Id, segDesde, segHasta, cabana.PrecioPorNoche);

                    opcion.Segmentos.Add(new SegmentoOpcion
                    {
                        CabanaId = cabana.Id,
                        CabanaNombre = cabana.Nombre,
                        Desde = segDesde,
                        Hasta = segHasta,
                        Subtotal = subtotal
                    });

                    total = total.HasValue && subtotal.HasValue ? total + subtotal : null;
                }

                opcion.Total = total;
                resultado.Opciones.Add(opcion);
            }

            resultado.Opciones = resultado.Opciones
                .OrderBy(o => o.Total.HasValue ? 0 : 1)
                .ThenBy(o => o.Total)
                .ToList();

            return resultado;
        }
    }
}
