namespace GestionCabanas.Models
{
    /// <summary>
    /// Reglas para decidir qué tarifa (para 2, 4 o 6 personas) corresponde a un grupo.
    /// Se configura en la sección "Precios" de appsettings.
    /// </summary>
    public class PoliticaPrecios
    {
        public static readonly int[] Tramos = { 2, 4, 6 };

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
    }
}
