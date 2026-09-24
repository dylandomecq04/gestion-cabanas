using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class CambiarMinimoNochesAFecha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MinimosNoches_DiaSemana",
                table: "MinimosNoches");

            migrationBuilder.DropColumn(
                name: "DiaSemana",
                table: "MinimosNoches");

            migrationBuilder.AddColumn<DateTime>(
                name: "Fecha",
                table: "MinimosNoches",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_MinimosNoches_Fecha",
                table: "MinimosNoches",
                column: "Fecha",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MinimosNoches_Fecha",
                table: "MinimosNoches");

            migrationBuilder.DropColumn(
                name: "Fecha",
                table: "MinimosNoches");

            migrationBuilder.AddColumn<int>(
                name: "DiaSemana",
                table: "MinimosNoches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MinimosNoches_DiaSemana",
                table: "MinimosNoches",
                column: "DiaSemana",
                unique: true);
        }
    }
}
