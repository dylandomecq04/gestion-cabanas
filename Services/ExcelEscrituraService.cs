using ClosedXML.Excel;
using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Services
{
    /// <summary>
    /// Refleja en el Excel los cambios que se hacen sobre una reserva desde el sitio (alta, edición
    /// o baja). Nunca inserta ni borra filas: solo escribe o limpia los valores de una fila que ya
    /// existe en el archivo, para no correr de lugar el resto de las reservas de la hoja.
    /// </summary>
    public class ExcelEscrituraService
    {
        private readonly ApplicationDbContext _db;
        private readonly GraphOneDriveService _oneDrive;
        private readonly IConfiguration _config;

        public ExcelEscrituraService(ApplicationDbContext db, GraphOneDriveService oneDrive, IConfiguration config)
        {
            _db = db;
            _oneDrive = oneDrive;
            _config = config;
        }

        /// <summary>
        /// Escribe (o actualiza) la reserva en su celda del Excel. Si la reserva todavía no tiene
        /// una ubicación asignada, o la que tenía ya no corresponde al bloque de su cabaña, busca
        /// una fila libre en el bloque de esa cabaña y ese mes. Si no hay lugar, o la conexión con
        /// OneDrive no está lista, devuelve un mensaje explicando por qué no se pudo reflejar
        /// (la reserva igual queda guardada en el sitio).
        /// </summary>
        public async Task<string?> EscribirReservaAsync(Reserva reserva)
        {
            var urlArchivo = _config["OneDrive:ArchivoUrl"];
            var conexion = await _oneDrive.ObtenerConexionAsync();
            if (string.IsNullOrWhiteSpace(urlArchivo) || conexion?.RefreshTokenCifrado is null)
            {
                return null; // Sin OneDrive conectado, no hay Excel para reflejar (no es un error).
            }

            var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == reserva.CabanaId);
            if (cabana is null)
            {
                return null;
            }

            try
            {
                var (driveId, itemId) = await _oneDrive.ObtenerDriveItemAsync(urlArchivo);
                var bytes = await _oneDrive.DescargarArchivoCompartidoAsync(urlArchivo);
                using var workbook = new XLWorkbook(new MemoryStream(bytes));

                var sobrescrituras = ExcelReservasSyncService.ObtenerSobrescrituraHojas(_config);
                var hoja = ExcelReservasSyncService.UbicarHojaDelMes(workbook, reserva.FechaDesde.Month, sobrescrituras);
                if (hoja is null)
                {
                    return $"No encontré la hoja de \"{reserva.FechaDesde:MMMM}\" en el Excel. Agregala ahí a mano.";
                }

                var fila = ResolverFilaExistente(reserva, hoja, cabana.Nombre);
                if (fila is null)
                {
                    var bloque = ExcelReservasSyncService.UbicarBloqueDeCabana(hoja, cabana.Nombre);
                    if (bloque is null)
                    {
                        return $"No encontré el bloque de \"{cabana.Nombre}\" en la hoja de \"{reserva.FechaDesde:MMMM}\". Agregala ahí a mano.";
                    }

                    var filaLibre = ExcelReservasSyncService.BuscarFilaLibreEnBloque(hoja, bloque.Value.ColFecha, bloque.Value.ColNombre, bloque.Value.FilaEncabezado);
                    if (filaLibre is null)
                    {
                        return $"No hay una fila libre para \"{cabana.Nombre}\" en la hoja de \"{reserva.FechaDesde:MMMM}\". Agregala ahí a mano.";
                    }

                    fila = (bloque.Value.ColFecha, bloque.Value.ColNombre, bloque.Value.ColPagar ?? bloque.Value.ColFecha + 3, filaLibre.Value);
                }

                var (colFecha, colNombre, colPagar, numeroFila) = fila.Value;
                var direccionFechaCelda = hoja.Cell(numeroFila, colFecha).Address.ToString();
                var direccionNombreCelda = hoja.Cell(numeroFila, colNombre).Address.ToString();
                var direccionPagarCelda = hoja.Cell(numeroFila, colPagar).Address.ToString();

                var textoFecha = $"{reserva.FechaDesde.Day} a {reserva.FechaHasta.Day}";
                var textoPagar = reserva.Valor?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionFechaCelda, textoFecha);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionNombreCelda, reserva.NombreHuesped);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionPagarCelda, textoPagar);

                reserva.ExcelUbicacion = $"{reserva.FechaDesde.Year}/{hoja.Name}!{direccionFechaCelda}";
                await _db.SaveChangesAsync();
                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo reflejar en el Excel: {ex.Message}";
            }
        }

        /// <summary>
        /// Si la reserva ya tiene una celda asignada y esa celda sigue perteneciendo al bloque
        /// actual de su cabaña, devuelve esa ubicación para reusarla. Si no, devuelve null para que
        /// el llamador busque una fila libre nueva.
        /// </summary>
        private static (int ColFecha, int ColNombre, int ColPagar, int Fila)? ResolverFilaExistente(Reserva reserva, IXLWorksheet hoja, string nombreCabana)
        {
            if (string.IsNullOrEmpty(reserva.ExcelUbicacion) || !reserva.ExcelUbicacion.Contains('!'))
            {
                return null;
            }

            var direccionFecha = reserva.ExcelUbicacion.Split('!', 2)[1];
            IXLCell celdaFecha;
            try
            {
                celdaFecha = hoja.Cell(direccionFecha);
            }
            catch
            {
                return null;
            }

            var bloque = ExcelReservasSyncService.UbicarBloqueDeCabana(hoja, nombreCabana);
            if (bloque is null || bloque.Value.ColFecha != celdaFecha.Address.ColumnNumber)
            {
                return null;
            }

            return (bloque.Value.ColFecha, bloque.Value.ColNombre, bloque.Value.ColPagar ?? bloque.Value.ColFecha + 3, celdaFecha.Address.RowNumber);
        }

        /// <summary>
        /// Limpia (sin borrar la fila) la celda de Excel de una reserva que se eliminó en el sitio.
        /// </summary>
        public async Task<string?> LimpiarReservaAsync(Reserva reserva)
        {
            var urlArchivo = _config["OneDrive:ArchivoUrl"];
            var conexion = await _oneDrive.ObtenerConexionAsync();
            if (string.IsNullOrWhiteSpace(urlArchivo) || conexion?.RefreshTokenCifrado is null)
            {
                return null;
            }
            if (string.IsNullOrEmpty(reserva.ExcelUbicacion) || !reserva.ExcelUbicacion.Contains('!'))
            {
                return null; // Esta reserva nunca estuvo en el Excel.
            }

            try
            {
                var (driveId, itemId) = await _oneDrive.ObtenerDriveItemAsync(urlArchivo);
                var bytes = await _oneDrive.DescargarArchivoCompartidoAsync(urlArchivo);
                using var workbook = new XLWorkbook(new MemoryStream(bytes));

                var partes = reserva.ExcelUbicacion.Split('!', 2);
                var nombreHoja = partes[0][(partes[0].IndexOf('/') + 1)..];
                var hoja = workbook.Worksheets.FirstOrDefault(h => h.Name == nombreHoja);
                if (hoja is null)
                {
                    return null;
                }

                var celdaFecha = hoja.Cell(partes[1]);
                var fila = celdaFecha.Address.RowNumber;
                var colFecha = celdaFecha.Address.ColumnNumber;

                var cabana = await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == reserva.CabanaId);
                var bloque = cabana is null ? null : ExcelReservasSyncService.UbicarBloqueDeCabana(hoja, cabana.Nombre);
                var colNombre = bloque?.ColNombre ?? colFecha + 1;
                var colPagar = bloque?.ColPagar ?? colFecha + 3;

                var direccionFechaCelda = hoja.Cell(fila, colFecha).Address.ToString();
                var direccionNombreCelda = hoja.Cell(fila, colNombre).Address.ToString();
                var direccionPagarCelda = hoja.Cell(fila, colPagar).Address.ToString();

                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionFechaCelda, null);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionNombreCelda, null);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionPagarCelda, null);

                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo limpiar la celda en el Excel: {ex.Message}";
            }
        }
    }
}
