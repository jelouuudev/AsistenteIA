using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa12WorkflowEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionWorkflow",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false),
                    ReintentosMaximos = table.Column<int>(type: "int", nullable: false),
                    TiempoMaximoPasoMs = table.Column<int>(type: "int", nullable: false),
                    TiempoMaximoFlujoMs = table.Column<int>(type: "int", nullable: false),
                    ConfirmacionesObligatorias = table.Column<bool>(type: "bit", nullable: false),
                    LimitePasosPorWorkflow = table.Column<int>(type: "int", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionWorkflow", x => x.IdConfiguracion);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    IdWorkflow = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Disparadores = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.IdWorkflow);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowEjecuciones",
                columns: table => new
                {
                    IdEjecucion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdWorkflow = table.Column<int>(type: "int", nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdAsistente = table.Column<int>(type: "int", nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TiempoTotalMs = table.Column<long>(type: "bigint", nullable: true),
                    ResultadoFinal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confirmado = table.Column<bool>(type: "bit", nullable: false),
                    WorkflowIdWorkflow = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowEjecuciones", x => x.IdEjecucion);
                    table.ForeignKey(
                        name: "FK_WorkflowEjecuciones_Workflows_WorkflowIdWorkflow",
                        column: x => x.WorkflowIdWorkflow,
                        principalTable: "Workflows",
                        principalColumn: "IdWorkflow");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowPasos",
                columns: table => new
                {
                    IdPaso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdWorkflow = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Herramienta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Parametros = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiereConfirmacion = table.Column<bool>(type: "bit", nullable: false),
                    ReintentosMaximos = table.Column<int>(type: "int", nullable: false),
                    TiempoMaximoMs = table.Column<int>(type: "int", nullable: false),
                    EstrategiaError = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowPasos", x => x.IdPaso);
                    table.ForeignKey(
                        name: "FK_WorkflowPasos_Workflows_IdWorkflow",
                        column: x => x.IdWorkflow,
                        principalTable: "Workflows",
                        principalColumn: "IdWorkflow",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowPasosEjecucion",
                columns: table => new
                {
                    IdPasoEjecucion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEjecucion = table.Column<int>(type: "int", nullable: false),
                    IdPaso = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowPasosEjecucion", x => x.IdPasoEjecucion);
                    table.ForeignKey(
                        name: "FK_WorkflowPasosEjecucion_WorkflowEjecuciones_IdEjecucion",
                        column: x => x.IdEjecucion,
                        principalTable: "WorkflowEjecuciones",
                        principalColumn: "IdEjecucion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEjecuciones_WorkflowIdWorkflow",
                table: "WorkflowEjecuciones",
                column: "WorkflowIdWorkflow");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPasos_IdWorkflow_Orden",
                table: "WorkflowPasos",
                columns: new[] { "IdWorkflow", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPasosEjecucion_IdEjecucion",
                table: "WorkflowPasosEjecucion",
                column: "IdEjecucion");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Codigo",
                table: "Workflows",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionWorkflow");

            migrationBuilder.DropTable(
                name: "WorkflowPasos");

            migrationBuilder.DropTable(
                name: "WorkflowPasosEjecucion");

            migrationBuilder.DropTable(
                name: "WorkflowEjecuciones");

            migrationBuilder.DropTable(
                name: "Workflows");
        }
    }
}
