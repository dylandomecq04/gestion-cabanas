using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionCabanas.Services
{
    public class DisponibilidadService
    {
        private readonly ApplicationDbContext _db;
        private readonly PoliticaPrecios _politica;

        public DisponibilidadService(ApplicationDbContext db, IOptions<PoliticaPrecios> politica)
        {
            _db = db;
            _politica = politica.Value;
        }

        public PoliticaPrecios Politica => _politica;

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

            return await query.AnyAsync();
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
        /// Guarda los precios (para 2, 4 y 6 personas) de cada día. Un día sin ningún precio
        /// borra su tarifa. Los tramos que la cabaña no admite (ej. "para 6" en una cabaña para 4)
        /// se descartan, vengan de donde vengan.
        /// </summary>
        public async Task GuardarTarifasAsync(IEnumerable<TarifaDiaInput> dias)
        {
            var listaDias = dias.ToList();
            if (listaDias.Count == 0)
            {
                return;
            }

            var fechaMin = listaDias.Min(d => d.Fecha.Date);
            var fechaMax = listaDias.Max(d => d.Fecha.Date);

            var existentes = await _db.TarifasDias
                .Where(t => t.Fecha >= fechaMin && t.Fecha <= fechaMax)
                .ToDictionaryAsync(t => (t.CabanaId, t.Fecha.Date), t => t);

            var idsCabanas = listaDias.Select(d => d.CabanaId).Distinct().ToList();
            var cabanas = await _db.Cabanas
                .Where(c => idsCabanas.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            foreach (var dia in listaDias)
            {
                if (cabanas.TryGetValue(dia.CabanaId, out var cabana))
                {
                    if (!cabana.AdmiteTramo(4)) dia.Precio4 = null;
                    if (!cabana.AdmiteTramo(6)) dia.Precio6 = null;
                }

                var fecha = dia.Fecha.Date;
                existentes.TryGetValue((dia.CabanaId, fecha), out var existente);

                if (!dia.Precio2.HasValue && !dia.Precio4.HasValue && !dia.Precio6.HasValue)
                {
                    if (existente is not null)
                    {
                        _db.TarifasDias.Remove(existente);
                    }
                    continue;
                }

                if (existente is null)
                {
                    existente = new TarifaDia { CabanaId = dia.CabanaId, Fecha = fecha };
                    _db.TarifasDias.Add(existente);
                }

                existente.Precio2 = dia.Precio2;
                existente.Precio4 = dia.Precio4;
                existente.Precio6 = dia.Precio6;
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Precio "desde" de cada cabaña: la tarifa para 2 personas más baja entre las noches
        /// que todavía no pasaron.
        /// </summary>
        public async Task<Dictionary<int, decimal>> ObtenerPreciosDesdeAsync()
        {
            var hoy = DateTime.Today;
            var tarifas = await _db.TarifasDias
                .Where(t => t.Fecha >= hoy && t.Precio2 != null)
                .ToListAsync();

            return tarifas
                .GroupBy(t => t.CabanaId)
                .ToDictionary(g => g.Key, g => g.Min(t => t.Precio2!.Value));
        }

        public async Task<decimal?> CalcularValorTotalAsync(int cabanaId, DateTime desde, DateTime hasta, Huespedes huespedes)
        {
            var detalle = await CalcularValorConDetalleAsync(cabanaId, desde, hasta, huespedes);
            return detalle.Total;
        }

        /// <summary>
        /// Calcula el valor total de una estadía según la cantidad de adultos y menores: para cada
        /// noche se toma el precio del tramo que corresponde al grupo (para 2, 4 o 6 personas) y,
        /// si hay un adulto de más, se le suma el recargo. Si corresponde, aplica el paquete de
        /// precios de una promoción por cantidad de noches (1, 2 o 3) para ese rango. Una promo
        /// sólo se aplica si cubre TODAS las noches de la estadía. Para estadías de más de 3
        /// noches con promo activa, las primeras 3 noches se cobran al precio del paquete de 3
        /// noches y el resto a precio normal. El precio del paquete no depende de la cantidad
        /// de personas.
        /// </summary>
        public async Task<ResultadoPrecio> CalcularValorConDetalleAsync(int cabanaId, DateTime desde, DateTime hasta, Huespedes huespedes)
        {
            var resultado = new ResultadoPrecio();
            if (hasta <= desde)
            {
                return resultado;
            }

            var tarifaGrupo = _politica.Resolver(huespedes.Adultos, huespedes.Menores);
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
                    var totalResto = tarifaGrupo is null ? null : await SumaDiariaAsync(cabanaId, desde.AddDays(3), hasta, tarifaGrupo);
                    resultado.Total = totalResto.HasValue ? promo.Precio3Noches.Value + totalResto.Value : null;
                    resultado.PromoAplicada = true;
                    resultado.EtiquetaPromo = EtiquetaPaquete(promo, 3);
                    return resultado;
                }
            }

            if (tarifaGrupo is null)
            {
                return resultado;
            }

            resultado.Total = await SumaDiariaAsync(cabanaId, desde, hasta, tarifaGrupo);
            resultado.EtiquetaTarifa = tarifaGrupo.Descripcion(_politica.RecargoAdultoExtraPorcentaje);
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

        private async Task<decimal?> SumaDiariaAsync(int cabanaId, DateTime desde, DateTime hasta, TarifaGrupo tarifaGrupo)
        {
            if (hasta <= desde)
            {
                return null;
            }

            var tarifas = await ObtenerTarifasEnRangoAsync(cabanaId, desde, hasta.AddDays(-1));
            decimal total = 0;

            for (var dia = desde; dia < hasta; dia = dia.AddDays(1))
            {
                var precioDelDia = tarifas.FirstOrDefault(t => t.Fecha.Date == dia.Date)?.PrecioDelTramo(tarifaGrupo.Tramo);
                if (!precioDelDia.HasValue)
                {
                    return null;
                }
                total += tarifaGrupo.ConRecargo ? Math.Round(precioDelDia.Value * _politica.FactorRecargo, 0) : precioDelDia.Value;
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
        /// Cabañas activas (excluyendo una en particular) sin superposición con reservas
        /// confirmadas en el rango dado, con capacidad suficiente. Se usa para ofrecer
        /// alternativas cuando dos solicitudes compiten por la misma cabaña y fecha.
        /// </summary>
        public async Task<List<CabanaAlternativa>> ObtenerCabanasAlternativasAsync(DateTime desde, DateTime hasta, int cabanaIdExcluir, Huespedes huespedes)
        {
            var personas = huespedes.Total;
            var cabanas = await _db.Cabanas
                .Where(c => c.Activa && c.Id != cabanaIdExcluir && c.Capacidad >= personas)
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            var resultado = new List<CabanaAlternativa>();
            foreach (var cabana in cabanas)
            {
                if (await HaySuperposicionAsync(cabana.Id, desde, hasta))
                {
                    continue;
                }

                var precio = await CalcularValorTotalAsync(cabana.Id, desde, hasta, huespedes);
                resultado.Add(new CabanaAlternativa { CabanaId = cabana.Id, Nombre = cabana.Nombre, Precio = precio });
            }

            return resultado;
        }

        /// <summary>
        /// Reparte al grupo entre dos cabañas que se ocupan a la vez (la de mayor capacidad primero).
        /// Devuelve null si no entran o si no hay al menos un adulto en cada una.
        /// </summary>
        public static RepartoEnCabanas? RepartirEnCabanas(Cabana una, Cabana otra, Huespedes huespedes)
        {
            var (primera, segunda) = una.Capacidad >= otra.Capacidad ? (una, otra) : (otra, una);
            var reparto = huespedes.RepartirEnDos(primera.Capacidad, segunda.Capacidad);
            return reparto is null ? null : new RepartoEnCabanas(primera, segunda, reparto.Value.Primera, reparto.Value.Segunda);
        }

        /// <summary>
        /// Busca, para un rango de fechas y una cantidad de personas (hasta el máximo por reserva),
        /// las opciones de reserva posibles: cabañas individuales que cubran todo el rango, o -si
        /// ninguna lo cubre sola- todas las combinaciones válidas que usan la menor cantidad de
        /// cabañas posible. Los grupos de 5 o más personas además pueden repartirse en dos cabañas
        /// libres durante toda la estadía; si son más de lo que entra en una cabaña, es la única forma.
        /// </summary>
        public async Task<ResultadoBusquedaDisponibilidad> BuscarOpcionesAsync(DateTime desde, DateTime hasta, Huespedes huespedes)
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

            if (huespedes.Adultos < 1)
            {
                resultado.Mensaje = "Tiene que haber al menos un adulto.";
                return resultado;
            }

            var personas = huespedes.Total;
            if (personas > PoliticaPrecios.MaxPersonasPorReserva)
            {
                resultado.Mensaje = $"Podemos recibir hasta {PoliticaPrecios.MaxPersonasPorReserva} personas por reserva (contando a los menores).";
                return resultado;
            }

            var activas = await _db.Cabanas
                .Where(c => c.Activa)
                .OrderBy(c => c.Nombre)
                .ToListAsync();
            var cabanas = activas.Where(c => c.Capacidad >= personas).ToList();

            var repartos = new List<RepartoEnCabanas>();
            if (personas >= PoliticaPrecios.DosCabanasDesdePersonas)
            {
                for (var i = 0; i < activas.Count; i++)
                {
                    for (var j = i + 1; j < activas.Count; j++)
                    {
                        var reparto = RepartirEnCabanas(activas[i], activas[j], huespedes);
                        if (reparto is not null)
                        {
                            repartos.Add(reparto);
                        }
                    }
                }
            }

            var fechas = Enumerable.Range(0, noches).Select(i => desde.AddDays(i)).ToList();

            if (cabanas.Count == 0 && repartos.Count == 0)
            {
                resultado.CobreTotal = false;
                resultado.Mensaje = "No podemos armar una reserva para ese grupo (en cada cabaña tiene que haber al menos un adulto). Escribinos y lo coordinamos.";
                return resultado;
            }

            var reservas = await ObtenerConfirmadasEnRangoAsync(desde, hasta.AddDays(-1));

            bool EstaOcupada(int cabanaId, DateTime dia) => reservas.Any(r => r.CabanaId == cabanaId && r.FechaDesde <= dia && dia < r.FechaHasta);

            var libre = new bool[cabanas.Count, noches];
            for (var c = 0; c < cabanas.Count; c++)
            {
                for (var n = 0; n < noches; n++)
                {
                    libre[c, n] = !EstaOcupada(cabanas[c].Id, fechas[n]);
                }
            }

            var sinCobertura = new List<DateTime>();
            for (var n = 0; n < noches; n++)
            {
                var hayCabanaLibre = Enumerable.Range(0, cabanas.Count).Any(c => libre[c, n]);
                var hayRepartoLibre = repartos.Any(r => !EstaOcupada(r.Primera.Id, fechas[n]) && !EstaOcupada(r.Segunda.Id, fechas[n]));
                if (!hayCabanaLibre && !hayRepartoLibre)
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
                    var detalleSegmento = await CalcularValorConDetalleAsync(cabana.Id, segDesde, segHasta, huespedes);

                    opcion.Segmentos.Add(new SegmentoOpcion
                    {
                        CabanaId = cabana.Id,
                        CabanaNombre = cabana.Nombre,
                        Desde = segDesde,
                        Hasta = segHasta,
                        Subtotal = detalleSegmento.Total,
                        PromoAplicada = detalleSegmento.PromoAplicada,
                        EtiquetaPromo = detalleSegmento.EtiquetaPromo,
                        EtiquetaTarifa = detalleSegmento.EtiquetaTarifa,
                        Adultos = huespedes.Adultos,
                        Menores = huespedes.Menores
                    });

                    total = total.HasValue && detalleSegmento.Total.HasValue ? total + detalleSegmento.Total : null;
                }

                opcion.Total = total;
                resultado.Opciones.Add(opcion);
            }

            foreach (var reparto in repartos)
            {
                var libreTodaLaEstadia = fechas.All(f => !EstaOcupada(reparto.Primera.Id, f) && !EstaOcupada(reparto.Segunda.Id, f));
                if (!libreTodaLaEstadia)
                {
                    continue;
                }

                var opcion = new OpcionReserva { Repartida = true };
                decimal? total = 0;

                foreach (var (cabana, grupo) in new[] { (reparto.Primera, reparto.HuespedesPrimera), (reparto.Segunda, reparto.HuespedesSegunda) })
                {
                    var detalle = await CalcularValorConDetalleAsync(cabana.Id, desde, hasta, grupo);

                    opcion.Segmentos.Add(new SegmentoOpcion
                    {
                        CabanaId = cabana.Id,
                        CabanaNombre = cabana.Nombre,
                        Desde = desde,
                        Hasta = hasta,
                        Subtotal = detalle.Total,
                        PromoAplicada = detalle.PromoAplicada,
                        EtiquetaPromo = detalle.EtiquetaPromo,
                        EtiquetaTarifa = detalle.EtiquetaTarifa,
                        Adultos = grupo.Adultos,
                        Menores = grupo.Menores
                    });

                    total = total.HasValue && detalle.Total.HasValue ? total + detalle.Total : null;
                }

                opcion.Total = total;
                resultado.Opciones.Add(opcion);
            }

            if (resultado.Opciones.Count == 0 && personas >= PoliticaPrecios.DosCabanasDesdePersonas)
            {
                resultado.CobreTotal = false;
                resultado.Mensaje = "No encontramos una cabaña, ni dos cabañas a la vez, libres durante todas esas fechas para el grupo. Probá con otras fechas o escribinos y lo coordinamos.";
                return resultado;
            }

            resultado.Opciones = resultado.Opciones
                .OrderBy(o => o.Total.HasValue ? 0 : 1)
                .ThenBy(o => o.Total)
                .ToList();

            return resultado;
        }
    }
}
