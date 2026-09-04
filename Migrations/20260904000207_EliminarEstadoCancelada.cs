using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class EliminarEstadoCancelada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El estado "Cancelada" (valor 2) dejó de existir: las reservas canceladas
            // ahora se eliminan directamente en vez de guardarse con ese estado.
            migrationBuilder.Sql("DELETE FROM \"Reservas\" WHERE \"Estado\" = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reversible: las filas canceladas eliminadas por Up() no se pueden recuperar.
        }
    }
}
