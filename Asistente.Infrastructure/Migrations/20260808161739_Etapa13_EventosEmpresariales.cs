using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa13_EventosEmpresariales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionEventoMotor",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false),
                    ReintentosMaximos = table.Column<int>(type: "int", nullable: false),
                    IntervaloReintentoMs = table.Column<int>(type: "int", nullable: false),
                    TiempoMaximoEventoMs = table.Column<int>(type: "int", nullable: false),
                    EventosSimultaneosMax = table.Column<int>(type: "int", nullable: false),
                    FrecuenciaProcesadorMs = table.Column<int>(type: "int", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionEventoMotor", x => x.IdConfiguracion);
                });

            migrationBuilder.CreateTable(
                name: "EventosEmpresariales",
                columns: table => new
                {
                    IdEvento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosEmpresariales", x => x.IdEvento);
                });

            migrationBuilder.CreateTable(
                name: "TareasProgramadas",
                columns: table => new
                {
                    IdTarea = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpresionCron = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdWorkflow = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    UltimaEjecucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProximaEjecucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasProgramadas", x => x.IdTarea);
                    table.ForeignKey(
                        name: "FK_TareasProgramadas_Workflows_IdWorkflow",
                        column: x => x.IdWorkflow,
                        principalTable: "Workflows",
                        principalColumn: "IdWorkflow",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosProcesados",
                columns: table => new
                {
                    IdEventoProcesado = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEvento = table.Column<int>(type: "int", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TiempoProcesamiento = table.Column<long>(type: "bigint", nullable: false),
                    IdRegla = table.Column<int>(type: "int", nullable: true),
                    IdWorkflow = table.Column<int>(type: "int", nullable: true),
                    IdUsuario = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosProcesados", x => x.IdEventoProcesado);
                    table.ForeignKey(
                        name: "FK_EventosProcesados_EventosEmpresariales_IdEvento",
                        column: x => x.IdEvento,
                        principalTable: "EventosEmpresariales",
                        principalColumn: "IdEvento",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReglasEvento",
                columns: table => new
                {
                    IdRegla = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEvento = table.Column<int>(type: "int", nullable: false),
                    IdWorkflow = table.Column<int>(type: "int", nullable: false),
                    Condicion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasEvento", x => x.IdRegla);
                    table.ForeignKey(
                        name: "FK_ReglasEvento_EventosEmpresariales_IdEvento",
                        column: x => x.IdEvento,
                        principalTable: "EventosEmpresariales",
                        principalColumn: "IdEvento",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReglasEvento_Workflows_IdWorkflow",
                        column: x => x.IdWorkflow,
                        principalTable: "Workflows",
                        principalColumn: "IdWorkflow",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosEmpresariales_Codigo",
                table: "EventosEmpresariales",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventosProcesados_Estado",
                table: "EventosProcesados",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_EventosProcesados_FechaHora",
                table: "EventosProcesados",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_EventosProcesados_IdEvento",
                table: "EventosProcesados",
                column: "IdEvento");

            migrationBuilder.CreateIndex(
                name: "IX_ReglasEvento_IdEvento",
                table: "ReglasEvento",
                column: "IdEvento");

            migrationBuilder.CreateIndex(
                name: "IX_ReglasEvento_IdWorkflow",
                table: "ReglasEvento",
                column: "IdWorkflow");

            migrationBuilder.CreateIndex(
                name: "IX_TareasProgramadas_IdWorkflow",
                table: "TareasProgramadas",
                column: "IdWorkflow");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionEventoMotor");

            migrationBuilder.DropTable(
                name: "EventosProcesados");

            migrationBuilder.DropTable(
                name: "ReglasEvento");

            migrationBuilder.DropTable(
                name: "TareasProgramadas");

            migrationBuilder.DropTable(
                name: "EventosEmpresariales");
        }
    }
}
