using System.Text;

namespace GestionCabanas.Services
{
    public static class NombresPropios
    {
        /// <summary>
        /// Deja un nombre con la primera letra de cada palabra en mayúscula y el resto en minúscula
        /// ("juan PEREZ" -> "Juan Perez"), sin espacios de más al principio, al final ni entre palabras.
        /// </summary>
        public static string Formatear(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return string.Empty;
            }

            var resultado = new StringBuilder(nombre.Length);
            var inicioDePalabra = true;
            var espacioPendiente = false;

            foreach (var caracter in nombre.Trim())
            {
                if (char.IsWhiteSpace(caracter))
                {
                    espacioPendiente = true;
                    continue;
                }

                if (espacioPendiente)
                {
                    resultado.Append(' ');
                    espacioPendiente = false;
                    inicioDePalabra = true;
                }

                resultado.Append(inicioDePalabra ? char.ToUpperInvariant(caracter) : char.ToLowerInvariant(caracter));
                inicioDePalabra = !char.IsLetterOrDigit(caracter);
            }

            return resultado.ToString();
        }
    }
}
