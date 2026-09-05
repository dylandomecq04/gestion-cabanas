using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Services
{
    public class GraphOneDriveService
    {
        private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        private const string AuthorizeEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        private const string Scopes = "Files.ReadWrite offline_access";

        private readonly ApplicationDbContext _db;
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly IDataProtector _protector;

        public GraphOneDriveService(ApplicationDbContext db, HttpClient http, IConfiguration config, IDataProtectionProvider dataProtectionProvider)
        {
            _db = db;
            _http = http;
            _config = config;
            _protector = dataProtectionProvider.CreateProtector("GraphOneDriveService.RefreshToken");
        }

        private string ClientId => _config["MicrosoftGraph:ClientId"] ?? string.Empty;
        private string ClientSecret => _config["MicrosoftGraph:ClientSecret"] ?? string.Empty;

        public bool EstaConfigurado => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

        public string ConstruirUrlAutorizacion(string redirectUri, string state)
        {
            var query = new Dictionary<string, string?>
            {
                ["client_id"] = ClientId,
                ["response_type"] = "code",
                ["redirect_uri"] = redirectUri,
                ["response_mode"] = "query",
                ["scope"] = Scopes,
                ["state"] = state,
            };
            return AuthorizeEndpoint + "?" + string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? "")}"));
        }

        public async Task<OneDriveConexion> IntercambiarCodigoAsync(string code, string redirectUri)
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["scope"] = Scopes,
            };

            var respuesta = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"No se pudo obtener el token de Microsoft: {cuerpo}");
            }

            using var json = JsonDocument.Parse(cuerpo);
            var refreshToken = json.RootElement.GetProperty("refresh_token").GetString();

            var conexion = await _db.OneDriveConexiones.FirstOrDefaultAsync();
            if (conexion is null)
            {
                conexion = new OneDriveConexion();
                _db.OneDriveConexiones.Add(conexion);
            }

            conexion.RefreshTokenCifrado = _protector.Protect(refreshToken ?? string.Empty);
            conexion.FechaConexion = DateTime.Now;

            await _db.SaveChangesAsync();
            return conexion;
        }

        public async Task<OneDriveConexion?> ObtenerConexionAsync()
        {
            return await _db.OneDriveConexiones.FirstOrDefaultAsync();
        }

        public async Task DesconectarAsync()
        {
            var conexion = await _db.OneDriveConexiones.FirstOrDefaultAsync();
            if (conexion is not null)
            {
                _db.OneDriveConexiones.Remove(conexion);
                await _db.SaveChangesAsync();
            }
        }

        public async Task MarcarSincronizadoAsync(DateTime? modificacionExcelVista = null)
        {
            var conexion = await _db.OneDriveConexiones.FirstOrDefaultAsync();
            if (conexion is not null)
            {
                conexion.UltimaSincronizacion = DateTime.Now;
                if (modificacionExcelVista.HasValue)
                {
                    conexion.UltimaModificacionExcelVista = modificacionExcelVista;
                }
                await _db.SaveChangesAsync();
            }
        }

        private async Task<string> ObtenerAccessTokenAsync()
        {
            var conexion = await _db.OneDriveConexiones.FirstOrDefaultAsync();
            if (conexion?.RefreshTokenCifrado is null)
            {
                throw new InvalidOperationException("Todavía no conectaste tu cuenta de OneDrive.");
            }

            var refreshToken = _protector.Unprotect(conexion.RefreshTokenCifrado);

            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = Scopes,
            };

            var respuesta = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"La conexión con OneDrive dejó de ser válida. Volvé a conectar tu cuenta. Detalle: {cuerpo}");
            }

            using var json = JsonDocument.Parse(cuerpo);
            var accessToken = json.RootElement.GetProperty("access_token").GetString()!;

            if (json.RootElement.TryGetProperty("refresh_token", out var nuevoRefresh))
            {
                conexion.RefreshTokenCifrado = _protector.Protect(nuevoRefresh.GetString() ?? refreshToken);
                await _db.SaveChangesAsync();
            }

            return accessToken;
        }

        public async Task<DateTime?> ObtenerFechaModificacionAsync(string urlCompartida)
        {
            var accessToken = await ObtenerAccessTokenAsync();
            var shareId = CodificarUrlCompartida(urlCompartida);

            using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/shares/{shareId}/driveItem?$select=lastModifiedDateTime");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var respuesta = await _http.SendAsync(solicitud);
            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await respuesta.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"No se pudo consultar el archivo de OneDrive: {detalle}");
            }

            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("lastModifiedDateTime", out var valor)
                ? valor.GetDateTime()
                : null;
        }

        public async Task<(string DriveId, string ItemId)> ObtenerDriveItemAsync(string urlCompartida)
        {
            var accessToken = await ObtenerAccessTokenAsync();
            var shareId = CodificarUrlCompartida(urlCompartida);

            using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/shares/{shareId}/driveItem?$select=id,parentReference");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var respuesta = await _http.SendAsync(solicitud);
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"No se pudo resolver el archivo de OneDrive: {cuerpo}");
            }

            using var json = JsonDocument.Parse(cuerpo);
            var itemId = json.RootElement.GetProperty("id").GetString()!;
            var driveId = json.RootElement.GetProperty("parentReference").GetProperty("driveId").GetString()!;
            return (driveId, itemId);
        }

        public async Task EscribirCeldaAsync(string driveId, string itemId, string hoja, string direccion, string? valor)
        {
            var accessToken = await ObtenerAccessTokenAsync();
            var hojaCodificada = Uri.EscapeDataString(hoja);

            // Graph no vacía la celda si se manda "null" (lo interpreta como "no tocar"): para
            // limpiarla hay que mandar un string vacío explícito.
            var valorAEnviar = valor ?? string.Empty;

            using var solicitud = new HttpRequestMessage(
                HttpMethod.Patch,
                $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{itemId}/workbook/worksheets('{hojaCodificada}')/range(address='{direccion}')");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            solicitud.Content = JsonContent.Create(new { values = new[] { new[] { valorAEnviar } } });

            var respuesta = await _http.SendAsync(solicitud);
            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await respuesta.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"No se pudo escribir en el Excel (hoja \"{hoja}\", celda {direccion}): {detalle}");
            }
        }

        public async Task<byte[]> DescargarArchivoCompartidoAsync(string urlCompartida)
        {
            var accessToken = await ObtenerAccessTokenAsync();
            var shareId = CodificarUrlCompartida(urlCompartida);

            using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/shares/{shareId}/driveItem/content");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var respuesta = await _http.SendAsync(solicitud);
            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await respuesta.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"No se pudo descargar el archivo de OneDrive: {detalle}");
            }

            return await respuesta.Content.ReadAsByteArrayAsync();
        }

        private static string CodificarUrlCompartida(string url)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(url);
            var base64 = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('/', '_')
                .Replace('+', '-');
            return "u!" + base64;
        }
    }
}
