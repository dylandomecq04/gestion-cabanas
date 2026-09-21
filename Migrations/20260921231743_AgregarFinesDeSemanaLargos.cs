using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFinesDeSemanaLargos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinesDeSemanaLargos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    FechaDesde = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaHasta = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExigeReservaCompleta = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinesDeSemanaLargos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinesDeSemanaLargos_FechaDesde_FechaHasta",
                table: "FinesDeSemanaLargos",
                columns: new[] { "FechaDesde", "FechaHasta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinesDeSemanaLargos");
        }
    }
}
