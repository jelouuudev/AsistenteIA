using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa18PlannerEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plan",
                columns: table => new
                {
                    IdPlan = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    Objetivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Borrador"),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TiempoTotalMs = table.Column<long>(type: "bigint", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Razonamiento = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiereAprobacion = table.Column<bool>(type: "bit", nullable: false),
                    Aprobado = table.Column<bool>(type: "bit", nullable: false),
                    IdExecution = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plan", x => x.IdPlan);
                });

            migrationBuilder.CreateTable(
                name: "PlanDependency",
                columns: table => new
                {
                    IdDependency = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPlan = table.Column<int>(type: "int", nullable: false),
                    StepOrigen = table.Column<int>(type: "int", nullable: false),
                    StepDestino = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanDependency", x => x.IdDependency);
                    table.ForeignKey(
                        name: "FK_PlanDependency_Plan_IdPlan",
                        column: x => x.IdPlan,
                        principalTable: "Plan",
                        principalColumn: "IdPlan",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanExecutionLog",
                columns: table => new
                {
                    IdLog = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPlan = table.Column<int>(type: "int", nullable: false),
                    IdStep = table.Column<int>(type: "int", nullable: true),
                    Evento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanExecutionLog", x => x.IdLog);
                    table.ForeignKey(
                        name: "FK_PlanExecutionLog_Plan_IdPlan",
                        column: x => x.IdPlan,
                        principalTable: "Plan",
                        principalColumn: "IdPlan",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanStep",
                columns: table => new
                {
                    IdStep = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPlan = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    Resultado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdAsistente = table.Column<int>(type: "int", nullable: true),
                    CodigoHerramienta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdWorkflow = table.Column<int>(type: "int", nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanStep", x => x.IdStep);
                    table.ForeignKey(
                        name: "FK_PlanStep_Plan_IdPlan",
                        column: x => x.IdPlan,
                        principalTable: "Plan",
                        principalColumn: "IdPlan",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanDependency_IdPlan",
                table: "PlanDependency",
                column: "IdPlan");

            migrationBuilder.CreateIndex(
                name: "IX_PlanExecutionLog_IdPlan",
                table: "PlanExecutionLog",
                column: "IdPlan");

            migrationBuilder.CreateIndex(
                name: "IX_PlanStep_IdPlan",
                table: "PlanStep",
                column: "IdPlan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanDependency");

            migrationBuilder.DropTable(
                name: "PlanExecutionLog");

            migrationBuilder.DropTable(
                name: "PlanStep");

            migrationBuilder.DropTable(
                name: "Plan");
        }
    }
}
