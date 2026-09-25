using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContextoDisparoEventoProcesado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextoDisparo",
                table: "EventosProcesados",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            // Rescata el contexto de disparo en filas aún no procesadas: en ellas
            // Resultado todavía contiene el JSON original (aún no sobrescrito).
            migrationBuilder.Sql(
                "UPDATE EventosProcesados SET ContextoDisparo = Resultado " +
                "WHERE ContextoDisparo IS NULL AND Resultado IS NOT NULL " +
                "AND Estado IN ('Pendiente', 'EnProceso', 'Reintentando')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextoDisparo",
                table: "EventosProcesados");
        }
    }
}
