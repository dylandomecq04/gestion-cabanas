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
        public int Omitidas { get; set; }
        public List<string> NoInterpretadas { get; } = new();
        public List<string> CabanasNoEncontradas { get; } = new();
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

        public ExcelReservasSyncService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ResultadoSincronizacion> SincronizarAsync(byte[] archivo, int anio)
        {
            var resultado = new ResultadoSincronizacion();
            var cabanas = await _db.Cabanas.ToListAsync();

            using var workbook = new XLWorkbook(new MemoryStream(archivo));

            foreach (var hoja in workbook.Worksheets)
            {
                if (!MesesPorNombre.TryGetValue(Normalizar(hoja.Name), out var mes))
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
                        if (texto == "NOMBRE") colNombre = c;
                        else if (texto == "PAGAR") colPagar = c;
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
                    for (var r = fila + 1; r <= ultimaFila; r++)
                    {
                        var textoFecha = hoja.Cell(r, colFecha).GetString().Trim();
                        var textoNombre = hoja.Cell(r, colNombre.Value).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(textoFecha) && string.IsNullOrWhiteSpace(textoNombre))
                        {
                            break;
                        }
                        if (Normalizar(textoFecha) == "TOTAL" || Normalizar(textoNombre) == "TOTAL")
                        {
                            break;
                        }
                        if (string.IsNullOrWhiteSpace(textoFecha) || string.IsNullOrWhiteSpace(textoNombre))
                        {
                            continue;
                        }

                        var match = Regex.Match(textoFecha, @"(\d{1,2})\s*a\.?\s*b?\.?\s*(\d{1,2})", RegexOptions.IgnoreCase);
                        if (!match.Success)
                        {
                            resultado.NoInterpretadas.Add($"{hoja.Name} / {nombreCabana}: \"{textoFecha}\" ({textoNombre})");
                            continue;
                        }

                        var diaDesde = int.Parse(match.Groups[1].Value);
                        var diaHasta = int.Parse(match.Groups[2].Value);

                        DateTime fechaDesde;
                        DateTime fechaHasta;
                        try
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
                            fechaHasta = new DateTime(anioHasta, mesHasta, diaHasta);
                        }
                        catch
                        {
                            resultado.NoInterpretadas.Add($"{hoja.Name} / {nombreCabana}: fecha inválida \"{textoFecha}\" ({textoNombre})");
                            continue;
                        }

                        decimal? pagar = colPagar.HasValue ? LeerDecimal(hoja.Cell(r, colPagar.Value)) : null;
                        var nombreHuespedNormalizado = textoNombre.ToLowerInvariant();

                        var existe = await _db.Reservas.AnyAsync(res =>
                            res.CabanaId == cabana.Id &&
                            res.FechaDesde == fechaDesde &&
                            res.FechaHasta == fechaHasta &&
                            res.NombreHuesped.ToLower() == nombreHuespedNormalizado);

                        if (existe)
                        {
                            resultado.Omitidas++;
                            continue;
                        }

                        _db.Reservas.Add(new Reserva
                        {
                            CabanaId = cabana.Id,
                            NombreHuesped = textoNombre,
                            FechaDesde = fechaDesde,
                            FechaHasta = fechaHasta,
                            CantidadPersonas = 1,
                            Estado = EstadoReserva.Confirmada,
                            Valor = pagar,
                        });
                        resultado.Creadas++;
                    }
                }
            }

            await _db.SaveChangesAsync();
            return resultado;
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
