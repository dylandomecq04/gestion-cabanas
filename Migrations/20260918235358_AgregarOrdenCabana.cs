using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrdenCabana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "Cabanas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Las cabañas se cargaron en el orden en que están en el complejo (1, 2, 3, Maia, 5), así que
            // su Id sirve de posición inicial. Después se puede corregir desde el admin.
            migrationBuilder.Sql("UPDATE \"Cabanas\" SET \"Orden\" = \"Id\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Orden",
                table: "Cabanas");
        }
    }
}
