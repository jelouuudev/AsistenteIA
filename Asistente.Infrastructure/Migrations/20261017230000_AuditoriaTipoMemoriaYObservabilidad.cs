using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <summary>
    /// Añade el tipo de memoria (ámbito) y la duración a la auditoría, e indexa por
    /// agente, usuario y operación.
    ///
    /// El ámbito se copia a MemoryAccessLogs porque al purgar una memoria su
    /// IdMemory queda en NULL y solo sobrevive el código: sin esta columna el tipo
    /// de memoria se perdería justo en el caso donde la trazabilidad importa.
    ///
    /// DuracionMs alimenta la métrica de tiempo de recuperación del panel, y los
    /// índices evitan un escaneo completo en cada consulta de observabilidad
    /// (uso por agente, uso por usuario, accesos por tipo).
    /// </summary>
    public partial class AuditoriaTipoMemoriaYObservabilidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ambito",
                table: "MemoryAccessLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionMs",
                table: "MemoryAccessLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryAccessLogs_IdAsistente",
                table: "MemoryAccessLogs",
                column: "IdAsistente");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryAccessLogs_IdUsuario",
                table: "MemoryAccessLogs",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryAccessLogs_Operacion",
                table: "MemoryAccessLogs",
                column: "Operacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemoryAccessLogs_IdAsistente",
                table: "MemoryAccessLogs");

            migrationBuilder.DropIndex(
                name: "IX_MemoryAccessLogs_IdUsuario",
                table: "MemoryAccessLogs");

            migrationBuilder.DropIndex(
                name: "IX_MemoryAccessLogs_Operacion",
                table: "MemoryAccessLogs");

            migrationBuilder.DropColumn(
                name: "Ambito",
                table: "MemoryAccessLogs");

            migrationBuilder.DropColumn(
                name: "DuracionMs",
                table: "MemoryAccessLogs");
        }
    }
}

