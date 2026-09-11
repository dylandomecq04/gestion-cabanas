using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPromoEstadia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromosEstadia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CabanaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    FechaDesde = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaHasta = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Precio1Noche = table.Column<decimal>(type: "TEXT", nullable: true),
                    Precio2Noches = table.Column<decimal>(type: "TEXT", nullable: true),
                    Precio3Noches = table.Column<decimal>(type: "TEXT", nullable: true),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromosEstadia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromosEstadia_Cabanas_CabanaId",
                        column: x => x.CabanaId,
                        principalTable: "Cabanas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromosEstadia_CabanaId_FechaDesde_FechaHasta",
                table: "PromosEstadia",
                columns: new[] { "CabanaId", "FechaDesde", "FechaHasta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromosEstadia");
        }
    }
}
