using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa11ToolOrchestrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionOrchestrator",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Habilitado = table.Column<bool>(type: "bit", nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    TiempoMaximoEjecucionMs = table.Column<int>(type: "int", nullable: false),
                    MaxEjecucionesSimultaneas = table.Column<int>(type: "int", nullable: false),
                    RequiereAutorizacion = table.Column<bool>(type: "bit", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionOrchestrator", x => x.IdConfiguracion);
                });

            migrationBuilder.CreateTable(
                name: "Herramientas",
                columns: table => new
                {
                    IdHerramienta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Utilidad"),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    RequierePermiso = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Herramientas", x => x.IdHerramienta);
                });

            migrationBuilder.CreateTable(
                name: "AsistentesHerramientas",
                columns: table => new
                {
                    IdAsistente = table.Column<int>(type: "int", nullable: false),
                    IdHerramienta = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsistentesHerramientas", x => new { x.IdAsistente, x.IdHerramienta });
                    table.ForeignKey(
                        name: "FK_AsistentesHerramientas_Asistente_IdAsistente",
                        column: x => x.IdAsistente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsistentesHerramientas_Herramientas_IdHerramienta",
                        column: x => x.IdHerramienta,
                        principalTable: "Herramientas",
                        principalColumn: "IdHerramienta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EjecucionesHerramientas",
                columns: table => new
                {
                    IdEjecucion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdHerramienta = table.Column<int>(type: "int", nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Parametros = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TiempoEjecucion = table.Column<long>(type: "bigint", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pendiente")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionesHerramientas", x => x.IdEjecucion);
                    table.ForeignKey(
                        name: "FK_EjecucionesHerramientas_Herramientas_IdHerramienta",
                        column: x => x.IdHerramienta,
                        principalTable: "Herramientas",
                        principalColumn: "IdHerramienta",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EjecucionesHerramientas_Usuario_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsistentesHerramientas_IdHerramienta",
                table: "AsistentesHerramientas",
                column: "IdHerramienta");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesHerramientas_FechaHora",
                table: "EjecucionesHerramientas",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesHerramientas_IdHerramienta",
                table: "EjecucionesHerramientas",
                column: "IdHerramienta");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesHerramientas_IdUsuario",
                table: "EjecucionesHerramientas",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_Herramientas_Codigo",
                table: "Herramientas",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsistentesHerramientas");

            migrationBuilder.DropTable(
                name: "ConfiguracionOrchestrator");

            migrationBuilder.DropTable(
                name: "EjecucionesHerramientas");

            migrationBuilder.DropTable(
                name: "Herramientas");
        }
    }
}
