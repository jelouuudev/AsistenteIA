using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa13_EventoProcesado_Workflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EventosProcesados_IdWorkflow",
                table: "EventosProcesados",
                column: "IdWorkflow");

            migrationBuilder.AddForeignKey(
                name: "FK_EventosProcesados_Workflows_IdWorkflow",
                table: "EventosProcesados",
                column: "IdWorkflow",
                principalTable: "Workflows",
                principalColumn: "IdWorkflow",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventosProcesados_Workflows_IdWorkflow",
                table: "EventosProcesados");

            migrationBuilder.DropIndex(
                name: "IX_EventosProcesados_IdWorkflow",
                table: "EventosProcesados");
        }
    }
}
