using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMensajesWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensajesWhatsApp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Clave = table.Column<string>(type: "TEXT", nullable: false),
                    Texto = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesWhatsApp", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesWhatsApp_Clave",
                table: "MensajesWhatsApp",
                column: "Clave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensajesWhatsApp");
        }
    }
}
