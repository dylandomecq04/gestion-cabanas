using ClosedXML.Excel;
using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Services
{
    /// <summary>
    /// Estado de una reserva antes de aplicarle cambios, para que <see cref="ExcelEscrituraService"/>
    /// sepa qué días liberar en el calendario del Excel si la fecha, la cabaña o el estado cambiaron.
    /// </summary>
    public record EstadoAnteriorReserva(int CabanaId, DateTime FechaDesde, DateTime FechaHasta, EstadoReserva Estado);

    /// <summary>
    /// Refleja en el Excel los cambios que se hacen sobre una reserva desde el sitio (alta, edición
    /// o baja). Nunca inserta ni borra filas: solo escribe o limpia los valores de una fila que ya
    /// existe en el archivo, para no correr de lugar el resto de las reservas de la hoja.
    /// </summary>
    public class ExcelEscrituraService
    {
        // Colores de relleno (mismo criterio que el calendario del sitio): verde = reservada,
        // rojo = se liberó (se eliminó o dejó de estar confirmada), amarillo = pasó el día y
        // quedó sin reservar.
        public const string ColorReservado = "#C6EFCE";
        public const string ColorLiberado = "#FFC7CE";
        public const string ColorPasadoSinReservar = "#FFF2CC";

        // Columna del calendario de disponibilidad (A a E) de cada cabaña, en el orden fijo del Excel.
        private static readonly Dictionary<string, int> ColumnaCalendarioPorCabana = new()
        {
            [ExcelReservasSyncService.Normalizar("Sidharta 1")] = 1,
            [ExcelReservasSyncService.Normalizar("Sidharta 2")] = 2,
            [ExcelReservasSyncService.Normalizar("Sidharta 3")] = 3,
            [ExcelReservasSyncService.Normalizar("Maia")] = 4,
            [ExcelReservasSyncService.Normalizar("Sidharta 5")] = 5,
        };

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
        public async Task<string?> EscribirReservaAsync(Reserva reserva, EstadoAnteriorReserva? anterior = null)
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

                    var filaLibre = ExcelReservasSyncService.BuscarFilaLibreEnBloque(hoja, bloque.Value.ColFecha, bloque.Value.ColNombre, bloque.Value.FilaEncabezado, reserva.FechaDesde.Day);
                    if (filaLibre is null)
                    {
                        return $"No hay una fila libre para \"{cabana.Nombre}\" en la hoja de \"{reserva.FechaDesde:MMMM}\". Agregala ahí a mano.";
                    }

                    fila = (bloque.Value.ColFecha, bloque.Value.ColNombre, bloque.Value.ColPago ?? bloque.Value.ColFecha + 2, bloque.Value.ColPagar ?? bloque.Value.ColFecha + 3, filaLibre.Value);
                }

                var (colFecha, colNombre, colPago, colPagar, numeroFila) = fila.Value;
                var direccionFechaCelda = hoja.Cell(numeroFila, colFecha).Address.ToString();
                var direccionNombreCelda = hoja.Cell(numeroFila, colNombre).Address.ToString();
                var direccionPagoCelda = hoja.Cell(numeroFila, colPago).Address.ToString();
                var direccionPagarCelda = hoja.Cell(numeroFila, colPagar).Address.ToString();

                var textoFecha = $"{reserva.FechaDesde.Day} a {reserva.FechaHasta.Day}";
                var textoPago = reserva.Pago?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var textoPagar = reserva.Valor?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionFechaCelda, textoFecha);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionNombreCelda, reserva.NombreHuesped);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionPagoCelda, textoPago);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionPagarCelda, textoPagar);

                // Si antes estaba confirmada con otra cabaña o fechas, esos días quedaron libres.
                if (anterior is not null && anterior.Estado == EstadoReserva.Confirmada &&
                    (anterior.CabanaId != reserva.CabanaId || anterior.FechaDesde != reserva.FechaDesde || anterior.FechaHasta != reserva.FechaHasta))
                {
                    var cabanaAnterior = anterior.CabanaId == cabana.Id
                        ? cabana
                        : await _db.Cabanas.FirstOrDefaultAsync(c => c.Id == anterior.CabanaId);
                    if (cabanaAnterior is not null)
                    {
                        await ColorearDiasAsync(driveId, itemId, workbook, sobrescrituras, cabanaAnterior.Nombre, anterior.FechaDesde, anterior.FechaHasta, ColorLiberado);
                    }
                }

                if (reserva.Estado == EstadoReserva.Confirmada)
                {
                    await ColorearDiasAsync(driveId, itemId, workbook, sobrescrituras, cabana.Nombre, reserva.FechaDesde, reserva.FechaHasta, ColorReservado);
                }
                else if (anterior?.Estado == EstadoReserva.Confirmada &&
                    anterior.CabanaId == reserva.CabanaId && anterior.FechaDesde == reserva.FechaDesde && anterior.FechaHasta == reserva.FechaHasta)
                {
                    // Dejó de estar confirmada sin cambiar de cabaña ni fechas: liberar esos días.
                    await ColorearDiasAsync(driveId, itemId, workbook, sobrescrituras, cabana.Nombre, reserva.FechaDesde, reserva.FechaHasta, ColorLiberado);
                }

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
        private static (int ColFecha, int ColNombre, int ColPago, int ColPagar, int Fila)? ResolverFilaExistente(Reserva reserva, IXLWorksheet hoja, string nombreCabana)
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

            return (bloque.Value.ColFecha, bloque.Value.ColNombre, bloque.Value.ColPago ?? bloque.Value.ColFecha + 2, bloque.Value.ColPagar ?? bloque.Value.ColFecha + 3, celdaFecha.Address.RowNumber);
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
                var colPago = bloque?.ColPago ?? colFecha + 2;
                var colPagar = bloque?.ColPagar ?? colFecha + 3;

                var direccionFechaCelda = hoja.Cell(fila, colFecha).Address.ToString();
                var direccionNombreCelda = hoja.Cell(fila, colNombre).Address.ToString();
                var direccionPagoCelda = hoja.Cell(fila, colPago).Address.ToString();
                var direccionPagarCelda = hoja.Cell(fila, colPagar).Address.ToString();

                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionFechaCelda, null);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionNombreCelda, null);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionPagoCelda, null);
                await _oneDrive.EscribirCeldaAsync(driveId, itemId, hoja.Name, direccionPagarCelda, null);

                if (reserva.Estado == EstadoReserva.Confirmada && cabana is not null)
                {
                    var sobrescrituras = ExcelReservasSyncService.ObtenerSobrescrituraHojas(_config);
                    await ColorearDiasAsync(driveId, itemId, workbook, sobrescrituras, cabana.Nombre, reserva.FechaDesde, reserva.FechaHasta, ColorLiberado);
                }

                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo limpiar la celda en el Excel: {ex.Message}";
            }
        }

        /// <summary>
        /// Pinta, en el calendario de disponibilidad (columnas A a E), los días de una cabaña entre
        /// <paramref name="desde"/> (incluido) y <paramref name="hastaExclusiva"/> (excluido, o sea
        /// sin contar el día de salida) del color indicado. Si la cabaña no tiene columna conocida en
        /// el calendario, o algún mes de la estadía no tiene hoja en el libro, esos días se saltean.
        /// </summary>
        private async Task ColorearDiasAsync(
            string driveId,
            string itemId,
            XLWorkbook workbook,
            IReadOnlyDictionary<int, string> sobrescrituras,
            string nombreCabana,
            DateTime desde,
            DateTime hastaExclusiva,
            string colorHex)
        {
            if (!ColumnaCalendarioPorCabana.TryGetValue(ExcelReservasSyncService.Normalizar(nombreCabana), out var columna))
            {
                return;
            }

            for (var fecha = desde.Date; fecha < hastaExclusiva.Date; fecha = fecha.AddDays(1))
            {
                var hoja = ExcelReservasSyncService.UbicarHojaDelMes(workbook, fecha.Month, sobrescrituras);
                if (hoja is null)
                {
                    continue;
                }

                var direccion = hoja.Cell(fecha.Day, columna).Address.ToString();
                await _oneDrive.EscribirColorCeldaAsync(driveId, itemId, hoja.Name, direccion, colorHex);
            }
        }

        /// <summary>
        /// Revisa, día por día desde el último que se procesó hasta ayer, si quedó sin ninguna
        /// reserva confirmada y lo marca en amarillo en el calendario de disponibilidad (columnas A
        /// a E). Los días que sí tienen una reserva confirmada se dejan (o se vuelven a dejar) en
        /// verde, para que el Excel quede consistente aunque algún color anterior no se haya podido
        /// escribir. No hace nada si no hay OneDrive conectado o si ya está al día.
        /// </summary>
        public async Task<string?> MarcarDiasPasadosAsync()
        {
            var urlArchivo = _config["OneDrive:ArchivoUrl"];
            var conexion = await _oneDrive.ObtenerConexionAsync();
            if (string.IsNullOrWhiteSpace(urlArchivo) || conexion?.RefreshTokenCifrado is null)
            {
                return null;
            }

            var ayer = DateTime.Today.AddDays(-1);
            var desde = conexion.UltimoDiaColoreado?.AddDays(1) ?? ayer;
            if (desde > ayer)
            {
                return null; // Ya está al día.
            }

            try
            {
                var cabanas = await _db.Cabanas.ToListAsync();
                var reservasConfirmadas = await _db.Reservas
                    .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaHasta > desde && r.FechaDesde <= ayer)
                    .ToListAsync();

                var (driveId, itemId) = await _oneDrive.ObtenerDriveItemAsync(urlArchivo);
                var bytes = await _oneDrive.DescargarArchivoCompartidoAsync(urlArchivo);
                using var workbook = new XLWorkbook(new MemoryStream(bytes));
                var sobrescrituras = ExcelReservasSyncService.ObtenerSobrescrituraHojas(_config);

                for (var fecha = desde; fecha <= ayer; fecha = fecha.AddDays(1))
                {
                    foreach (var cabana in cabanas)
                    {
                        var ocupada = reservasConfirmadas.Any(r => r.CabanaId == cabana.Id && r.FechaDesde <= fecha && fecha < r.FechaHasta);
                        await ColorearDiasAsync(driveId, itemId, workbook, sobrescrituras, cabana.Nombre, fecha, fecha.AddDays(1), ocupada ? ColorReservado : ColorPasadoSinReservar);
                    }
                }

                conexion.UltimoDiaColoreado = ayer;
                await _db.SaveChangesAsync();
                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo marcar los días pasados en el Excel: {ex.Message}";
            }
        }

        /// <summary>
        /// Repinta todo el calendario de disponibilidad (columnas A a E) de un año completo, en
        /// todas las hojas de mes que encuentre en el libro: verde en los días con una reserva
        /// confirmada, amarillo en los días ya pasados sin ninguna, y sin tocar los días futuros sin
        /// reservar. Pensado para dejar sincronizado de una vez lo que ya estaba cargado antes de
        /// tener este coloreado automático. Agrupa los días consecutivos del mismo color de cada
        /// cabaña en una sola llamada a Graph para no hacer una por día.
        /// </summary>
        public async Task<string?> RepintarCalendarioAsync(int anio)
        {
            var urlArchivo = _config["OneDrive:ArchivoUrl"];
            var conexion = await _oneDrive.ObtenerConexionAsync();
            if (string.IsNullOrWhiteSpace(urlArchivo) || conexion?.RefreshTokenCifrado is null)
            {
                return "Todavía no conectaste tu cuenta de OneDrive.";
            }

            try
            {
                var inicioAnio = new DateTime(anio, 1, 1);
                var finAnioExclusivo = new DateTime(anio + 1, 1, 1);
                var hoy = DateTime.Today;

                var cabanas = await _db.Cabanas.ToListAsync();
                var reservasConfirmadas = await _db.Reservas
                    .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaDesde < finAnioExclusivo && r.FechaHasta > inicioAnio)
                    .ToListAsync();

                var (driveId, itemId) = await _oneDrive.ObtenerDriveItemAsync(urlArchivo);
                var bytes = await _oneDrive.DescargarArchivoCompartidoAsync(urlArchivo);
                using var workbook = new XLWorkbook(new MemoryStream(bytes));
                var sobrescrituras = ExcelReservasSyncService.ObtenerSobrescrituraHojas(_config);

                for (var mes = 1; mes <= 12; mes++)
                {
                    var hoja = ExcelReservasSyncService.UbicarHojaDelMes(workbook, mes, sobrescrituras);
                    if (hoja is null)
                    {
                        continue;
                    }

                    foreach (var cabana in cabanas)
                    {
                        if (!ColumnaCalendarioPorCabana.TryGetValue(ExcelReservasSyncService.Normalizar(cabana.Nombre), out var columna))
                        {
                            continue;
                        }

                        await ColorearMesCabanaAsync(driveId, itemId, hoja, columna, anio, mes, cabana.Id, reservasConfirmadas, hoy);
                    }
                }

                if (anio == hoy.Year && (conexion.UltimoDiaColoreado is null || conexion.UltimoDiaColoreado < hoy.AddDays(-1)))
                {
                    conexion.UltimoDiaColoreado = hoy.AddDays(-1);
                }

                await _db.SaveChangesAsync();
                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo repintar el calendario del Excel: {ex.Message}";
            }
        }

        /// <summary>
        /// Recorre los días de un mes para una cabaña y pinta cada tramo de días consecutivos que
        /// comparten color en una sola llamada (en vez de una por día), para que repintar el año
        /// entero no implique miles de llamadas a Graph.
        /// </summary>
        private async Task ColorearMesCabanaAsync(
            string driveId,
            string itemId,
            IXLWorksheet hoja,
            int columna,
            int anio,
            int mes,
            int cabanaId,
            List<Reserva> reservasConfirmadas,
            DateTime hoy)
        {
            var diasEnMes = DateTime.DaysInMonth(anio, mes);
            string? colorTramo = null;
            var inicioTramo = 1;

            async Task CerrarTramoAsync(int filaHasta)
            {
                if (colorTramo is null)
                {
                    return;
                }
                var direccion = inicioTramo == filaHasta
                    ? hoja.Cell(inicioTramo, columna).Address.ToString()
                    : $"{hoja.Cell(inicioTramo, columna).Address}:{hoja.Cell(filaHasta, columna).Address}";
                await _oneDrive.EscribirColorCeldaAsync(driveId, itemId, hoja.Name, direccion, colorTramo);
            }

            for (var dia = 1; dia <= diasEnMes; dia++)
            {
                var fecha = new DateTime(anio, mes, dia);
                var ocupada = reservasConfirmadas.Any(r => r.CabanaId == cabanaId && r.FechaDesde <= fecha && fecha < r.FechaHasta);
                var colorDia = ocupada ? ColorReservado : (fecha < hoy ? ColorPasadoSinReservar : null);

                if (colorDia != colorTramo)
                {
                    await CerrarTramoAsync(dia - 1);
                    colorTramo = colorDia;
                    inicioTramo = dia;
                }
            }

            await CerrarTramoAsync(diasEnMes);
        }
    }
}
