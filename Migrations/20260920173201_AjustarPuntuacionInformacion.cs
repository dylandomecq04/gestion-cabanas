using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AjustarPuntuacionInformacion : Migration
    {
        // Reescribe con puntos algunas frases de los textos de Información que se cargaron con punto y coma.
        // Es un reemplazo de frases exactas: si alguien ya editó ese texto desde el panel, no lo toca.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Reemplazar(migrationBuilder, "ComoLlegar",
                "en San Fernando; más abajo están los pasos",
                "en San Fernando. Más abajo están los pasos");

            Reemplazar(migrationBuilder, "ComoLlegarAuto",
                "Continuá por la avenida Colón. Vas a cruzar otra vía (la del Tren de la Costa); seguí derecho.",
                "Continuá por la avenida Colón, cruzá otra vía (la del Tren de la Costa) y seguí derecho.");
            Reemplazar(migrationBuilder, "ComoLlegarAuto",
                "la Guardería Poseidón; después de la guardería",
                "la Guardería Poseidón. Después de la guardería");

            Reemplazar(migrationBuilder, "ComoLlegarTren",
                "la Guardería Poseidón; después de la guardería",
                "la Guardería Poseidón. Después de la guardería");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }

        private static void Reemplazar(MigrationBuilder migrationBuilder, string columna, string viejo, string nuevo)
        {
            migrationBuilder.Sql(
                $"UPDATE \"InformacionSitio\" SET \"{columna}\" = REPLACE(\"{columna}\", '{viejo.Replace("'", "''")}', '{nuevo.Replace("'", "''")}') " +
                $"WHERE \"{columna}\" LIKE '%{viejo.Replace("'", "''")}%';");
        }
    }
}
