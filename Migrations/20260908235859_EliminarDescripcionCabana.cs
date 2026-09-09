using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class EliminarDescripcionCabana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "Cabanas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "Cabanas",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }
    }
}
