using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class QuitarDescuentoFinDeSemana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DescuentosFinDeSemana");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DescuentosFinDeSemana",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Anio = table.Column<int>(type: "INTEGER", nullable: false),
                    Mes = table.Column<int>(type: "INTEGER", nullable: false),
                    Monto = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescuentosFinDeSemana", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DescuentosFinDeSemana_Anio_Mes",
                table: "DescuentosFinDeSemana",
                columns: new[] { "Anio", "Mes" },
                unique: true);
        }
    }
}
