using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPreciosPorCantidadPersonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se vacían antes de tocar las columnas: las tarifas viejas no tienen equivalente por tramo.
            migrationBuilder.Sql("DELETE FROM TarifasDias;");

            migrationBuilder.DropColumn(
                name: "PrecioPorNoche",
                table: "Cabanas");

            // El precio único por día no se traduce a ningún tramo (2/4/6 personas): se descarta
            // y los precios se vuelven a cargar por tramo desde el panel.
            migrationBuilder.DropColumn(
                name: "Precio",
                table: "TarifasDias");

            migrationBuilder.AddColumn<decimal>(
                name: "Precio2",
                table: "TarifasDias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Precio4",
                table: "TarifasDias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Precio6",
                table: "TarifasDias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CantidadMenores",
                table: "Reservas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Precio2",
                table: "TarifasDias");

            migrationBuilder.DropColumn(
                name: "Precio4",
                table: "TarifasDias");

            migrationBuilder.DropColumn(
                name: "CantidadMenores",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "Precio6",
                table: "TarifasDias");

            migrationBuilder.AddColumn<decimal>(
                name: "Precio",
                table: "TarifasDias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioPorNoche",
                table: "Cabanas",
                type: "TEXT",
                nullable: true);
        }
    }
}
