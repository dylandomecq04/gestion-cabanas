namespace GestionCabanas.Models
{
    /// <summary>
    /// Reglas para decidir qué tarifa (para 2, 4 o 6 personas) corresponde a un grupo.
    /// Se configura en la sección "Precios" de appsettings.
    /// </summary>
    public class PoliticaPrecios
    {
        public static readonly int[] Tramos = { 2, 4, 6 };

        /// <summary>Máximo de personas (contando a los menores) en una misma reserva, sea en una o en dos cabañas.</summary>
        public const int MaxPersonasPorReserva = 8;

        /// <summary>
        /// Desde esta cantidad de personas, además de una cabaña sola (si alguna tiene lugar), se ofrece
        /// repartir al grupo en dos cabañas a la vez. Las cabañas comunes son para 4 y Maia para 6.
        /// </summary>
        public const int DosCabanasDesdePersonas = 5;

        /// <summary>Menores que no suman para el precio. Del tercero en adelante cuentan como adultos.</summary>
        public int MenoresSinCargo { get; set; } = 2;

        /// <summary>Recargo (en %) sobre el tramo inferior cuando hay un adulto de más (3 o 5 pagantes).</summary>
        public decimal RecargoAdultoExtraPorcentaje { get; set; } = 10;

        public decimal FactorRecargo => 1 + RecargoAdultoExtraPorcentaje / 100m;

        public int Pagantes(int adultos, int menores) => adultos + Math.Max(0, menores - MenoresSinCargo);

        /// <summary>
        /// Devuelve la tarifa que se cobra al grupo, o null si no hay ninguna que lo cubra
        /// (sin adultos, o más de 6 pagantes).
        /// </summary>
        public TarifaGrupo? Resolver(int adultos, int menores)
        {
            if (adultos < 1 || menores < 0)
            {
                return null;
            }

            var pagantes = Pagantes(adultos, menores);
            return pagantes switch
            {
                <= 2 => new TarifaGrupo(2, false, pagantes),
                3 => new TarifaGrupo(2, true, pagantes),
                4 => new TarifaGrupo(4, false, pagantes),
                5 => new TarifaGrupo(4, true, pagantes),
                6 => new TarifaGrupo(6, false, pagantes),
                _ => null
            };
        }
    }

    /// <summary>Tramo de precio (2, 4 o 6) y si lleva el recargo por adulto extra.</summary>
    public record TarifaGrupo(int Tramo, bool ConRecargo, int Pagantes)
    {
        public string Descripcion(decimal recargoPorcentaje) => ConRecargo
            ? $"Tarifa para {Tramo} personas + {recargoPorcentaje:0.##}% por adulto extra"
            : $"Tarifa para {Tramo} personas";
    }

    public record Huespedes(int Adultos, int Menores)
    {
        public int Total => Adultos + Menores;

        /// <summary>
        /// Reparte al grupo entre dos cabañas que se ocupan a la vez: lo más parejo posible, con al menos
        /// un adulto en cada una y sin pasar la capacidad de ninguna. El grupo más grande queda en la
        /// primera cabaña (que tiene que ser la de mayor capacidad). Devuelve null si no se puede.
        /// </summary>
        public (Huespedes Primera, Huespedes Segunda)? RepartirEnDos(int capacidadPrimera, int capacidadSegunda)
        {
            if (Adultos < 2 || Menores < 0 || Total > capacidadPrimera + capacidadSegunda)
            {
                return null;
            }

            var minPrimera = Math.Max(1, Total - capacidadSegunda);
            var maxPrimera = Math.Min(capacidadPrimera, Total - 1);

            (Huespedes, Huespedes)? mejor = null;
            var mejorDiferencia = int.MaxValue;

            for (var primera = maxPrimera; primera >= minPrimera; primera--)
            {
                var diferencia = Math.Abs(2 * primera - Total);
                if (diferencia >= mejorDiferencia)
                {
                    continue;
                }

                var reparto = Dividir(primera);
                if (reparto is null)
                {
                    continue;
                }

                mejor = reparto;
                mejorDiferencia = diferencia;
            }

            return mejor;
        }

        /// <summary>
        /// Todos los repartos que se ofrecen entre dos cabañas: primero el más parejo y, para un grupo de 6,
        /// también el de 4 en una cabaña y 2 en la otra (cambia el precio, porque cada una usa su tarifa).
        /// El grupo más grande queda en la primera cabaña. Vacío si no se puede repartir.
        /// </summary>
        public List<(Huespedes Primera, Huespedes Segunda)> RepartosPosibles(int capacidadPrimera, int capacidadSegunda)
        {
            var repartos = new List<(Huespedes, Huespedes)>();
            var parejo = RepartirEnDos(capacidadPrimera, capacidadSegunda);
            if (parejo is null)
            {
                return repartos;
            }

            repartos.Add(parejo.Value);

            // Una cabaña con el tramo de 4 personas y el resto en la otra. Si el parejo ya es así, no se repite.
            const int tramoDeCuatro = 4;
            var resto = Total - tramoDeCuatro;
            if (resto >= 2 && tramoDeCuatro <= capacidadPrimera && resto <= capacidadSegunda
                && parejo.Value.Primera.Total != tramoDeCuatro)
            {
                var conCuatro = Dividir(tramoDeCuatro);
                if (conCuatro is not null)
                {
                    repartos.Add(conCuatro.Value);
                }
            }

            return repartos;
        }

        /// <summary>
        /// Deja <paramref name="primera"/> personas en la primera cabaña y el resto en la segunda. Los adultos
        /// de la primera son los que le corresponden por proporción, con al menos uno en cada cabaña y sin que
        /// sobren menores de un lado. Null si no se puede.
        /// </summary>
        private (Huespedes Primera, Huespedes Segunda)? Dividir(int primera)
        {
            var adultosMin = Math.Max(1, primera - Menores);
            var adultosMax = Math.Min(Adultos - 1, primera);
            if (adultosMin > adultosMax)
            {
                return null;
            }

            var adultosPrimera = Math.Clamp((int)Math.Round((double)Adultos * primera / Total, MidpointRounding.AwayFromZero), adultosMin, adultosMax);
            var menoresPrimera = primera - adultosPrimera;

            return (new Huespedes(adultosPrimera, menoresPrimera),
                    new Huespedes(Adultos - adultosPrimera, Menores - menoresPrimera));
        }
    }
}
