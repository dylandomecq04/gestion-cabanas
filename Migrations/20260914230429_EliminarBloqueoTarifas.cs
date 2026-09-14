using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class EliminarBloqueoTarifas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes de sacar la columna, convertimos cada tramo contiguo de días bloqueados en una
            // reserva confirmada a nombre de "Bloqueada" con pago y valor en 0 (el mismo criterio
            // que ya se usaba para reflejar un bloqueo en el Excel), para no perder ese bloqueo.
            // Se conserva la ubicación de Excel del primer día del tramo para no duplicar la fila
            // que ya estaba escrita ahí.
            migrationBuilder.Sql(@"
                INSERT INTO Reservas (CabanaId, NombreHuesped, Telefono, FechaDesde, FechaHasta, CantidadPersonas, Estado, Pago, Valor, FechaCreacion, ExcelUbicacion)
                SELECT g.CabanaId,
                       'Bloqueada',
                       NULL,
                       datetime(g.Inicio, 'start of day'),
                       datetime(g.Fin, '+1 day', 'start of day'),
                       1,
                       1,
                       0,
                       0,
                       datetime('now'),
                       (SELECT t2.ExcelUbicacion FROM TarifasDias t2 WHERE t2.CabanaId = g.CabanaId AND t2.Fecha = g.Inicio)
                FROM (
                    SELECT CabanaId, MIN(Fecha) AS Inicio, MAX(Fecha) AS Fin
                    FROM (
                        SELECT CabanaId, Fecha,
                               julianday(Fecha) - ROW_NUMBER() OVER (PARTITION BY CabanaId ORDER BY Fecha) AS Grupo
                        FROM TarifasDias
                        WHERE Bloqueada = 1
                    )
                    GROUP BY CabanaId, Grupo
                ) g;
            ");

            // Las filas que sólo existían para marcar un bloqueo (sin precio) ya no tienen sentido;
            // las que además tenían un precio cargado se conservan.
            migrationBuilder.Sql(@"DELETE FROM TarifasDias WHERE Bloqueada = 1 AND Precio IS NULL;");

            migrationBuilder.DropColumn(
                name: "Bloqueada",
                table: "TarifasDias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Bloqueada",
                table: "TarifasDias",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
