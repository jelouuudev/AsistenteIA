using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa17AgentOrchestrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstrategiaError",
                table: "ConfiguracionOrchestrator",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxAgentesPorSolicitud",
                table: "ConfiguracionOrchestrator",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxHerramientasPorAgente",
                table: "ConfiguracionOrchestrator",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxProfundidad",
                table: "ConfiguracionOrchestrator",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxTiempoTotalMs",
                table: "ConfiguracionOrchestrator",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReintentosNodo",
                table: "ConfiguracionOrchestrator",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AgentCollaborationRule",
                columns: table => new
                {
                    IdRule = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgenteOrigen = table.Column<int>(type: "int", nullable: false),
                    AgenteDestino = table.Column<int>(type: "int", nullable: false),
                    Permitido = table.Column<bool>(type: "bit", nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentCollaborationRule", x => x.IdRule);
                    table.ForeignKey(
                        name: "FK_AgentCollaborationRule_Asistente_AgenteDestino",
                        column: x => x.AgenteDestino,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentCollaborationRule_Asistente_AgenteOrigen",
                        column: x => x.AgenteOrigen,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentExecution",
                columns: table => new
                {
                    IdExecution = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdAgentePrincipal = table.Column<int>(type: "int", nullable: false),
                    Pregunta = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TiempoTotalMs = table.Column<long>(type: "bigint", nullable: true),
                    CantidadAgentes = table.Column<int>(type: "int", nullable: false),
                    ProfundidadAlcanzada = table.Column<int>(type: "int", nullable: false),
                    HerramientasUtilizadas = table.Column<int>(type: "int", nullable: false),
                    RespuestaFinal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecution", x => x.IdExecution);
                    table.ForeignKey(
                        name: "FK_AgentExecution_Asistente_IdAgentePrincipal",
                        column: x => x.IdAgentePrincipal,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentExecutionStep",
                columns: table => new
                {
                    IdStep = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdExecution = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    IdAgente = table.Column<int>(type: "int", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TiempoMs = table.Column<long>(type: "bigint", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dependencias = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecutionStep", x => x.IdStep);
                    table.ForeignKey(
                        name: "FK_AgentExecutionStep_AgentExecution_IdExecution",
                        column: x => x.IdExecution,
                        principalTable: "AgentExecution",
                        principalColumn: "IdExecution",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentExecutionStep_Asistente_IdAgente",
                        column: x => x.IdAgente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentExecutionTrace",
                columns: table => new
                {
                    IdTrace = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdExecution = table.Column<int>(type: "int", nullable: false),
                    Evento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecutionTrace", x => x.IdTrace);
                    table.ForeignKey(
                        name: "FK_AgentExecutionTrace_AgentExecution_IdExecution",
                        column: x => x.IdExecution,
                        principalTable: "AgentExecution",
                        principalColumn: "IdExecution",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCollaborationRule_AgenteDestino",
                table: "AgentCollaborationRule",
                column: "AgenteDestino");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCollaborationRule_AgenteOrigen_AgenteDestino",
                table: "AgentCollaborationRule",
                columns: new[] { "AgenteOrigen", "AgenteDestino" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecution_IdAgentePrincipal",
                table: "AgentExecution",
                column: "IdAgentePrincipal");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionStep_IdAgente",
                table: "AgentExecutionStep",
                column: "IdAgente");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionStep_IdExecution",
                table: "AgentExecutionStep",
                column: "IdExecution");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionTrace_IdExecution",
                table: "AgentExecutionTrace",
                column: "IdExecution");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentCollaborationRule");

            migrationBuilder.DropTable(
                name: "AgentExecutionStep");

            migrationBuilder.DropTable(
                name: "AgentExecutionTrace");

            migrationBuilder.DropTable(
                name: "AgentExecution");

            migrationBuilder.DropColumn(
                name: "EstrategiaError",
                table: "ConfiguracionOrchestrator");

            migrationBuilder.DropColumn(
                name: "MaxAgentesPorSolicitud",
                table: "ConfiguracionOrchestrator");

            migrationBuilder.DropColumn(
                name: "MaxHerramientasPorAgente",
                table: "ConfiguracionOrchestrator");

            migrationBuilder.DropColumn(
                name: "MaxProfundidad",
                table: "ConfiguracionOrchestrator");

            migrationBuilder.DropColumn(
                name: "MaxTiempoTotalMs",
                table: "ConfiguracionOrchestrator");

            migrationBuilder.DropColumn(
                name: "ReintentosNodo",
                table: "ConfiguracionOrchestrator");
        }
    }
}
