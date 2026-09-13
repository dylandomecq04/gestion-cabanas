using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace GestionCabanas.Services
{
    public class AlmacenamientoFotosService
    {
        private readonly BlobContainerClient? _contenedor;

        public AlmacenamientoFotosService(IConfiguration configuration)
        {
            var cadenaConexion = configuration["AzureBlobStorage:ConnStr"];
            var nombreContenedor = configuration["AzureBlobStorage:ContainerName"];

            if (!string.IsNullOrWhiteSpace(cadenaConexion) && !string.IsNullOrWhiteSpace(nombreContenedor))
            {
                _contenedor = new BlobContainerClient(cadenaConexion, nombreContenedor);
                _contenedor.CreateIfNotExists(PublicAccessType.Blob);
            }
        }

        public bool Configurado => _contenedor is not null;

        public async Task<string> SubirAsync(Stream contenido, string nombreBlob, string contentType)
        {
            if (_contenedor is null)
            {
                throw new InvalidOperationException("El almacenamiento de fotos (Azure Blob Storage) no está configurado.");
            }

            var blob = _contenedor.GetBlobClient(nombreBlob);
            await blob.UploadAsync(contenido, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            });
            return blob.Uri.ToString();
        }

        public async Task EliminarAsync(string urlPublica)
        {
            if (_contenedor is null || string.IsNullOrWhiteSpace(urlPublica))
            {
                return;
            }

            var prefijo = _contenedor.Uri.ToString().TrimEnd('/') + "/";
            if (!urlPublica.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var nombreBlob = Uri.UnescapeDataString(urlPublica[prefijo.Length..]);
            await _contenedor.DeleteBlobIfExistsAsync(nombreBlob);
        }
    }
}
