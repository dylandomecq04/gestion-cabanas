using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AjustarCapacidadMaia : Migration
    {
        // Maia es para hasta 6 personas (las demás, hasta 4), pero se había cargado con la capacidad
        // por defecto del seed. De la capacidad dependen los tramos de precio que admite cada cabaña.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Cabanas\" SET \"Capacidad\" = 6 WHERE \"Nombre\" = 'Maia' AND \"Capacidad\" < 6;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Cabanas\" SET \"Capacidad\" = 4 WHERE \"Nombre\" = 'Maia' AND \"Capacidad\" = 6;");
        }
    }
}
