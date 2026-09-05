using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Services
{
    public class ResultadoSincronizacion
    {
        public int Creadas { get; set; }
        public int Actualizadas { get; set; }
        public int Omitidas { get; set; }
        public int Eliminadas { get; set; }
        public List<string> DetalleCreadas { get; } = new();
        public List<string> DetalleActualizadas { get; } = new();
        public List<string> DetalleOmitidas { get; } = new();
        public List<string> DetalleEliminadas { get; } = new();
        public List<string> NoInterpretadas { get; } = new();
        public List<string> CabanasNoEncontradas { get; } = new();
        public List<string> Superposiciones { get; } = new();
    }

    public class ExcelReservasSyncService
    {
        private readonly ApplicationDbContext _db;

        private static readonly Dictionary<string, int> MesesPorNombre = new()
        {
            ["ENERO"] = 1,
            ["FEBRERO"] = 2,
            ["MARZO"] = 3,
            ["ABRIL"] = 4,
            ["MAYO"] = 5,
            ["JUNIO"] = 6,
            ["JULIO"] = 7,
            ["AGOSTO"] = 8,
            ["SEPTIEMBRE"] = 9,
            ["SETIEMBRE"] = 9,
            ["OCTUBRE"] = 10,
            ["NOVIEMBRE"] = 11,
            ["DICIEMBRE"] = 12,
        };

        private readonly IConfiguration _config;

        public ExcelReservasSyncService(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        /// <summary>
        /// Lee, si existe, una hoja de reemplazo por mes (ej. para probar con una hoja de prueba
        /// en vez de la real) desde la configuración "OneDrive:SobrescrituraHojas:{mes}".
        /// </summary>
        public static Dictionary<int, string> ObtenerSobrescrituraHojas(IConfiguration config)
        {
            var resultado = new Dictionary<int, string>();
            foreach (var hijo in config.GetSection("OneDrive:SobrescrituraHojas").GetChildren())
            {
                if (int.TryParse(hijo.Key, out var mes) && !string.IsNullOrWhiteSpace(hijo.Value))
                {
                    resultado[mes] = hijo.Value;
                }
            }
            return resultado;
        }

        public async Task<ResultadoSincronizacion> SincronizarAsync(byte[] archivo, int anio)
        {
            var resultado = new ResultadoSincronizacion();
            var cabanas = await _db.Cabanas.ToListAsync();
            var sobrescrituras = ObtenerSobrescrituraHojas(_config);

            var reservasPorUbicacion = await _db.Reservas
                .Where(r => r.ExcelUbicacion != null)
                .ToDictionaryAsync(r => r.ExcelUbicacion!);

            var reservasSinTag = await _db.Reservas
                .Where(r => r.ExcelUbicacion == null)
                .ToListAsync();

            var ubicacionesVistas = new HashSet<string>();

            using var workbook = new XLWorkbook(new MemoryStream(archivo));

            foreach (var hoja in workbook.Worksheets)
            {
                int mes;
                var entradaSobrescritura = sobrescrituras.FirstOrDefault(kv => Normalizar(kv.Value) == Normalizar(hoja.Name));
                if (!string.IsNullOrEmpty(entradaSobrescritura.Value))
                {
                    // Esta hoja es la de prueba configurada para ese mes: usarla en vez de la real.
                    mes = entradaSobrescritura.Key;
                }
                else if (MesesPorNombre.TryGetValue(Normalizar(hoja.Name), out var mesPorNombre))
                {
                    if (sobrescrituras.ContainsKey(mesPorNombre))
                    {
                        // Hay una hoja de prueba activa para este mes: no tocar la hoja real.
                        continue;
                    }
                    mes = mesPorNombre;
                }
                else
                {
                    continue;
                }

                var usado = hoja.RangeUsed();
                if (usado is null)
                {
                    continue;
                }

                var celdasFecha = usado.Cells().Where(c => Normalizar(c.GetString()) == "FECHA").ToList();

                foreach (var celdaFecha in celdasFecha)
                {
                    var fila = celdaFecha.Address.RowNumber;
                    var colFecha = celdaFecha.Address.ColumnNumber;

                    int? colNombre = null, colPagar = null;
                    for (var c = colFecha + 1; c <= colFecha + 6; c++)
                    {
                        var texto = Normalizar(hoja.Cell(fila, c).GetString());
                        if (texto == "FECHA")
                        {
                            break;
                        }
                        if (texto == "NOMBRE") colNombre ??= c;
                        else if (texto == "PAGAR") colPagar ??= c;
                    }

                    if (colNombre is null)
                    {
                        continue;
                    }

                    var colFinBloque = colPagar ?? colFecha + 3;
                    string? nombreCabana = null;
                    for (var r = fila - 1; r >= Math.Max(1, fila - 3) && nombreCabana is null; r--)
                    {
                        for (var c = colFecha; c <= colFinBloque; c++)
                        {
                            var texto = hoja.Cell(r, c).GetString().Trim();
                            if (!string.IsNullOrWhiteSpace(texto))
                            {
                                nombreCabana = texto;
                                break;
                            }
                        }
                    }

                    if (nombreCabana is null)
                    {
                        continue;
                    }

                    var cabana = cabanas.FirstOrDefault(c => Normalizar(c.Nombre) == Normalizar(nombreCabana));
                    if (cabana is null)
                    {
                        if (!resultado.CabanasNoEncontradas.Contains(nombreCabana))
                        {
                            resultado.CabanasNoEncontradas.Add(nombreCabana);
                        }
                        continue;
                    }

                    var ultimaFila = usado.LastRow().RowNumber();
                    var primeraFilaDelBloque = true;
                    for (var r = fila + 1; r <= ultimaFila; r++)
                    {
                        var textoFecha = hoja.Cell(r, colFecha).GetString().Trim();
                        var textoNombre = hoja.Cell(r, colNombre.Value).GetString().Trim();

                        if (Normalizar(textoFecha) == "TOTAL" || Normalizar(textoNombre) == "TOTAL")
                        {
                            break;
                        }
                        if (string.IsNullOrWhiteSpace(textoFecha) || string.IsNullOrWhiteSpace(textoNombre))
                        {
                            continue;
                        }

                        // La posición de la fila dentro del bloque importa: las filas están en orden
                        // cronológico. Si la primera reserva del bloque tiene el día de "hasta" menor
                        // que el de "desde" (ej. "27 al 2"), es porque arrancó el mes anterior y terminó
                        // en el mes de la hoja. Si esa misma ambigüedad aparece más abajo, es al revés:
                        // arrancó en el mes de la hoja y terminó en el siguiente.
                        var esPrimeraFila = primeraFilaDelBloque;
                        primeraFilaDelBloque = false;

                        var match = Regex.Match(textoFecha, @"(\d{1,2})\s*al?b?\.?\s*(\d{1,2})", RegexOptions.IgnoreCase);
                        if (!match.Success)
                        {
                            resultado.NoInterpretadas.Add($"{hoja.Name} / {nombreCabana}: \"{textoFecha}\" ({textoNombre})");
                            continue;
                        }

                        var diaDesde = int.Parse(match.Groups[1].Value);
                        var diaHasta = int.Parse(match.Groups[2].Value);

                        DateTime fechaDesde;
                        DateTime fechaHasta;

                        var retrocedeMes = diaDesde > DateTime.DaysInMonth(anio, mes) ||
                            (esPrimeraFila && diaHasta < diaDesde);

                        if (retrocedeMes)
                        {
                            // El día de inicio pertenece al mes anterior al de la hoja
                            // (ej. hoja "Septiembre" con "27 al 2" = 27 de agosto a 2 de septiembre).
                            var mesDesde = mes - 1;
                            var anioDesde = anio;
                            if (mesDesde < 1)
                            {
                                mesDesde = 12;
                                anioDesde--;
                            }

                            if (diaDesde > DateTime.DaysInMonth(anioDesde, mesDesde) || diaHasta > DateTime.DaysInMonth(anio, mes))
                            {
                                resultado.NoInterpretadas.Add($"{hoja.Name} / {nombreCabana}: fecha inválida \"{textoFecha}\" ({textoNombre})");
                                continue;
                            }

                            fechaDesde = new DateTime(anioDesde, mesDesde, diaDesde);
                            fechaHasta = new DateTime(anio, mes, diaHasta);
                        }
                        else
                        {
                            fechaDesde = new DateTime(anio, mes, diaDesde);

                            var mesHasta = mes;
                            var anioHasta = anio;
                            if (diaHasta < diaDesde)
                            {
                                mesHasta++;
                                if (mesHasta > 12)
                                {
                                    mesHasta = 1;
                                    anioHasta++;
                                }
                            }

                            if (diaHasta > DateTime.DaysInMonth(anioHasta, mesHasta))
                            {
                                resultado.NoInterpretadas.Add($"{hoja.Name} / {nombreCabana}: fecha inválida \"{textoFecha}\" ({textoNombre})");
                                continue;
                            }

                            fechaHasta = new DateTime(anioHasta, mesHasta, diaHasta);
                        }

                        decimal? pagar = colPagar.HasValue ? LeerDecimal(hoja.Cell(r, colPagar.Value)) : null;
                        var ubicacion = $"{anio}/{hoja.Name}!{hoja.Cell(r, colFecha).Address}";
                        ubicacionesVistas.Add(ubicacion);
                        var descripcion = $"{cabana.Nombre}: {textoNombre} ({fechaDesde:dd/MM} - {fechaHasta:dd/MM})";

                        if (reservasPorUbicacion.TryGetValue(ubicacion, out var reservaExistente))
                        {
                            if (reservaExistente.CabanaId != cabana.Id ||
                                reservaExistente.NombreHuesped != textoNombre ||
                                reservaExistente.FechaDesde != fechaDesde ||
                                reservaExistente.FechaHasta != fechaHasta ||
                                reservaExistente.Valor != pagar)
                            {
                                reservaExistente.CabanaId = cabana.Id;
                                reservaExistente.NombreHuesped = textoNombre;
                                reservaExistente.FechaDesde = fechaDesde;
                                reservaExistente.FechaHasta = fechaHasta;
                                reservaExistente.Valor = pagar;
                                resultado.Actualizadas++;
                                resultado.DetalleActualizadas.Add(descripcion);
                            }
                            else
                            {
                                resultado.Omitidas++;
                                resultado.DetalleOmitidas.Add(descripcion);
                            }
                            continue;
                        }

                        var nombreHuespedNormalizado = textoNombre.ToLowerInvariant();
                        var adoptada = reservasSinTag.FirstOrDefault(res =>
                            res.ExcelUbicacion is null &&
                            res.CabanaId == cabana.Id &&
                            res.FechaDesde == fechaDesde &&
                            res.FechaHasta == fechaHasta &&
                            res.NombreHuesped.ToLowerInvariant() == nombreHuespedNormalizado);

                        if (adoptada is not null)
                        {
                            adoptada.ExcelUbicacion = ubicacion;
                            reservasPorUbicacion[ubicacion] = adoptada;
                            resultado.Omitidas++;
                            resultado.DetalleOmitidas.Add(descripcion);
                            continue;
                        }

                        var nueva = new Reserva
                        {
                            CabanaId = cabana.Id,
                            NombreHuesped = textoNombre,
                            FechaDesde = fechaDesde,
                            FechaHasta = fechaHasta,
                            CantidadPersonas = 1,
                            Estado = EstadoReserva.Confirmada,
                            Valor = pagar,
                            ExcelUbicacion = ubicacion,
                        };
                        _db.Reservas.Add(nueva);
                        reservasPorUbicacion[ubicacion] = nueva;
                        resultado.Creadas++;
                        resultado.DetalleCreadas.Add(descripcion);
                    }
                }
            }

            EliminarFaltantes(resultado, cabanas, reservasPorUbicacion, ubicacionesVistas, anio);

            await _db.SaveChangesAsync();

            await DetectarSuperposicionesAsync(resultado, cabanas);

            return resultado;
        }

        private void EliminarFaltantes(
            ResultadoSincronizacion resultado,
            List<Cabana> cabanas,
            Dictionary<string, Reserva> reservasPorUbicacion,
            HashSet<string> ubicacionesVistas,
            int anio)
        {
            var prefijoAnio = $"{anio}/";

            foreach (var (ubicacion, reserva) in reservasPorUbicacion)
            {
                if (!ubicacion.StartsWith(prefijoAnio, StringComparison.Ordinal))
                {
                    continue;
                }
                if (ubicacionesVistas.Contains(ubicacion))
                {
                    continue;
                }

                var nombreCabana = cabanas.FirstOrDefault(c => c.Id == reserva.CabanaId)?.Nombre ?? "Cabaña";
                resultado.DetalleEliminadas.Add(
                    $"{nombreCabana}: {reserva.NombreHuesped} ({reserva.FechaDesde:dd/MM} - {reserva.FechaHasta:dd/MM})");
                resultado.Eliminadas++;
                _db.Reservas.Remove(reserva);
            }
        }

        private async Task DetectarSuperposicionesAsync(ResultadoSincronizacion resultado, List<Cabana> cabanas)
        {
            var activas = await _db.Reservas.ToListAsync();

            foreach (var grupo in activas.GroupBy(r => r.CabanaId))
            {
                var nombreCabana = cabanas.FirstOrDefault(c => c.Id == grupo.Key)?.Nombre ?? "Cabaña";
                var lista = grupo.OrderBy(r => r.FechaDesde).ToList();

                for (var i = 0; i < lista.Count; i++)
                {
                    for (var j = i + 1; j < lista.Count; j++)
                    {
                        if (lista[i].FechaDesde < lista[j].FechaHasta && lista[j].FechaDesde < lista[i].FechaHasta)
                        {
                            resultado.Superposiciones.Add(
                                $"{nombreCabana}: {lista[i].NombreHuesped} ({lista[i].FechaDesde:dd/MM} - {lista[i].FechaHasta:dd/MM}) se superpone con {lista[j].NombreHuesped} ({lista[j].FechaDesde:dd/MM} - {lista[j].FechaHasta:dd/MM})");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Busca, dentro de un libro ya abierto, la hoja cuyo nombre corresponde al mes indicado
        /// (ej. mes=9 -> hoja "Septiembre"). Se usa tanto para sincronizar como para escribir. Si
        /// hay una hoja de prueba configurada para ese mes (ver <see cref="ObtenerSobrescrituraHojas"/>),
        /// usa esa en vez de la real.
        /// </summary>
        public static IXLWorksheet? UbicarHojaDelMes(XLWorkbook workbook, int mes, IReadOnlyDictionary<int, string>? sobrescrituras = null)
        {
            if (sobrescrituras is not null && sobrescrituras.TryGetValue(mes, out var nombreHojaPrueba))
            {
                var hojaPrueba = workbook.Worksheets.FirstOrDefault(h => Normalizar(h.Name) == Normalizar(nombreHojaPrueba));
                if (hojaPrueba is not null)
                {
                    return hojaPrueba;
                }
            }

            return workbook.Worksheets.FirstOrDefault(h => MesesPorNombre.TryGetValue(Normalizar(h.Name), out var m) && m == mes);
        }

        /// <summary>
        /// Busca el bloque (columnas FECHA/NOMBRE/PAGAR y fila de encabezado) de una cabaña dentro
        /// de una hoja. Devuelve null si no encuentra un bloque con ese nombre de cabaña.
        /// </summary>
        public static (int ColFecha, int ColNombre, int? ColPagar, int FilaEncabezado)? UbicarBloqueDeCabana(IXLWorksheet hoja, string nombreCabana)
        {
            var usado = hoja.RangeUsed();
            if (usado is null)
            {
                return null;
            }

            var objetivo = Normalizar(nombreCabana);

            foreach (var celdaFecha in usado.Cells().Where(c => Normalizar(c.GetString()) == "FECHA"))
            {
                var fila = celdaFecha.Address.RowNumber;
                var colFecha = celdaFecha.Address.ColumnNumber;

                int? colNombre = null, colPagar = null;
                for (var c = colFecha + 1; c <= colFecha + 6; c++)
                {
                    var texto = Normalizar(hoja.Cell(fila, c).GetString());
                    if (texto == "FECHA")
                    {
                        break;
                    }
                    if (texto == "NOMBRE") colNombre ??= c;
                    else if (texto == "PAGAR") colPagar ??= c;
                }

                if (colNombre is null)
                {
                    continue;
                }

                var colFinBloque = colPagar ?? colFecha + 3;
                string? nombreCabanaBloque = null;
                for (var r = fila - 1; r >= Math.Max(1, fila - 3) && nombreCabanaBloque is null; r--)
                {
                    for (var c = colFecha; c <= colFinBloque; c++)
                    {
                        var texto = hoja.Cell(r, c).GetString().Trim();
                        if (!string.IsNullOrWhiteSpace(texto))
                        {
                            nombreCabanaBloque = texto;
                            break;
                        }
                    }
                }

                if (nombreCabanaBloque is not null && Normalizar(nombreCabanaBloque) == objetivo)
                {
                    return (colFecha, colNombre.Value, colPagar, fila);
                }
            }

            return null;
        }

        /// <summary>
        /// Busca, dentro de un bloque ya ubicado, la primera fila completamente vacía antes de
        /// llegar a un "TOTAL" o al final de lo usado en la hoja. Devuelve null si el bloque está
        /// lleno (no hay fila libre para agregar una reserva nueva sin insertar filas).
        /// </summary>
        public static int? BuscarFilaLibreEnBloque(IXLWorksheet hoja, int colFecha, int colNombre, int filaEncabezado)
        {
            var usado = hoja.RangeUsed();
            var ultimaFila = usado?.LastRow().RowNumber() ?? filaEncabezado;

            for (var r = filaEncabezado + 1; r <= ultimaFila; r++)
            {
                var textoFecha = hoja.Cell(r, colFecha).GetString().Trim();
                var textoNombre = hoja.Cell(r, colNombre).GetString().Trim();

                if (Normalizar(textoFecha) == "TOTAL" || Normalizar(textoNombre) == "TOTAL")
                {
                    return null;
                }
                if (string.IsNullOrWhiteSpace(textoFecha) && string.IsNullOrWhiteSpace(textoNombre))
                {
                    return r;
                }
            }

            return null;
        }

        private static decimal? LeerDecimal(IXLCell celda)
        {
            if (celda.IsEmpty())
            {
                return null;
            }
            if (celda.TryGetValue(out decimal valor))
            {
                return valor;
            }
            var texto = celda.GetString().Trim();
            if (string.IsNullOrEmpty(texto))
            {
                return null;
            }
            texto = texto.Replace(".", "").Replace(",", ".");
            return decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }
            var formaD = texto.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in formaD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
