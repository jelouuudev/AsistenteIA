using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAsistenteEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Asistente",
                columns: table => new
                {
                    IdAsistente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModeloIA = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "deepseek-r1:7b"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Idioma = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LongitudMaximaRespuesta = table.Column<int>(type: "int", nullable: true),
                    NivelFormalidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FormatoRespuesta = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Restricciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MensajeBienvenida = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Temperatura = table.Column<double>(type: "float", nullable: true),
                    MaxTokens = table.Column<int>(type: "int", nullable: true),
                    TimeoutSegundos = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistente", x => x.IdAsistente);
                });

            migrationBuilder.CreateTable(
                name: "PromptSistema",
                columns: table => new
                {
                    IdPrompt = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAsistente = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptSistema", x => x.IdPrompt);
                    table.ForeignKey(
                        name: "FK_PromptSistema_Asistente_IdAsistente",
                        column: x => x.IdAsistente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistorialPrompt",
                columns: table => new
                {
                    IdHistorial = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPrompt = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModificacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MotivoCambio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialPrompt", x => x.IdHistorial);
                    table.ForeignKey(
                        name: "FK_HistorialPrompt_PromptSistema_IdPrompt",
                        column: x => x.IdPrompt,
                        principalTable: "PromptSistema",
                        principalColumn: "IdPrompt",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPrompt_IdPrompt",
                table: "HistorialPrompt",
                column: "IdPrompt");

            migrationBuilder.CreateIndex(
                name: "IX_PromptSistema_IdAsistente",
                table: "PromptSistema",
                column: "IdAsistente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialPrompt");

            migrationBuilder.DropTable(
                name: "PromptSistema");

            migrationBuilder.DropTable(
                name: "Asistente");
        }
    }
}
