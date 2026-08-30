using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPromociones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Promociones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Lado = table.Column<int>(type: "INTEGER", nullable: false),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    Etiqueta = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promociones", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Promociones");
        }
    }
}
