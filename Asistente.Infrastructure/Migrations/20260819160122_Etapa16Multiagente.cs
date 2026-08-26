using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa16Multiagente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AsistenteIdAsistente",
                table: "UsuarioAsistentes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AsistenteIdAsistente",
                table: "AsistentesHerramientas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "Asistente",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "Asistente",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                table: "Asistente",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Objetivo",
                table: "Asistente",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptSistema",
                table: "Asistente",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Asistente",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AgentesRoles",
                columns: table => new
                {
                    IdAgenteRol = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAsistente = table.Column<int>(type: "int", nullable: false),
                    IdRol = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentesRoles", x => x.IdAgenteRol);
                    table.ForeignKey(
                        name: "FK_AgentesRoles_Asistente_IdAsistente",
                        column: x => x.IdAsistente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentesRoles_Rol_IdRol",
                        column: x => x.IdRol,
                        principalTable: "Rol",
                        principalColumn: "IdRol",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentesVersiones",
                columns: table => new
                {
                    IdAgenteVersion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAsistente = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PromptSistema = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModeloIA = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Temperatura = table.Column<double>(type: "float", nullable: true),
                    MaxTokens = table.Column<int>(type: "int", nullable: true),
                    Configuracion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentesVersiones", x => x.IdAgenteVersion);
                    table.ForeignKey(
                        name: "FK_AgentesVersiones_Asistente_IdAsistente",
                        column: x => x.IdAsistente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentesWorkflows",
                columns: table => new
                {
                    IdAgenteWorkflow = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAsistente = table.Column<int>(type: "int", nullable: false),
                    IdWorkflow = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentesWorkflows", x => x.IdAgenteWorkflow);
                    table.ForeignKey(
                        name: "FK_AgentesWorkflows_Asistente_IdAsistente",
                        column: x => x.IdAsistente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentesWorkflows_Workflows_IdWorkflow",
                        column: x => x.IdWorkflow,
                        principalTable: "Workflows",
                        principalColumn: "IdWorkflow",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioAsistentes_AsistenteIdAsistente",
                table: "UsuarioAsistentes",
                column: "AsistenteIdAsistente");

            migrationBuilder.CreateIndex(
                name: "IX_AsistentesHerramientas_AsistenteIdAsistente",
                table: "AsistentesHerramientas",
                column: "AsistenteIdAsistente");

            migrationBuilder.CreateIndex(
                name: "IX_AgentesRoles_IdAsistente",
                table: "AgentesRoles",
                column: "IdAsistente");

            migrationBuilder.CreateIndex(
                name: "IX_AgentesRoles_IdRol",
                table: "AgentesRoles",
                column: "IdRol");

            migrationBuilder.CreateIndex(
                name: "IX_AgentesVersiones_IdAsistente",
                table: "AgentesVersiones",
                column: "IdAsistente");

            migrationBuilder.CreateIndex(
                name: "IX_AgentesWorkflows_IdAsistente",
                table: "AgentesWorkflows",
                column: "IdAsistente");

            migrationBuilder.CreateIndex(
                name: "IX_AgentesWorkflows_IdWorkflow",
                table: "AgentesWorkflows",
                column: "IdWorkflow");

            migrationBuilder.AddForeignKey(
                name: "FK_AsistentesHerramientas_Asistente_AsistenteIdAsistente",
                table: "AsistentesHerramientas",
                column: "AsistenteIdAsistente",
                principalTable: "Asistente",
                principalColumn: "IdAsistente");

            migrationBuilder.AddForeignKey(
                name: "FK_UsuarioAsistentes_Asistente_AsistenteIdAsistente",
                table: "UsuarioAsistentes",
                column: "AsistenteIdAsistente",
                principalTable: "Asistente",
                principalColumn: "IdAsistente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AsistentesHerramientas_Asistente_AsistenteIdAsistente",
                table: "AsistentesHerramientas");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuarioAsistentes_Asistente_AsistenteIdAsistente",
                table: "UsuarioAsistentes");

            migrationBuilder.DropTable(
                name: "AgentesRoles");

            migrationBuilder.DropTable(
                name: "AgentesVersiones");

            migrationBuilder.DropTable(
                name: "AgentesWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_UsuarioAsistentes_AsistenteIdAsistente",
                table: "UsuarioAsistentes");

            migrationBuilder.DropIndex(
                name: "IX_AsistentesHerramientas_AsistenteIdAsistente",
                table: "AsistentesHerramientas");

            migrationBuilder.DropColumn(
                name: "AsistenteIdAsistente",
                table: "UsuarioAsistentes");

            migrationBuilder.DropColumn(
                name: "AsistenteIdAsistente",
                table: "AsistentesHerramientas");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "Objetivo",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "PromptSistema",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Asistente");
        }
    }
}
