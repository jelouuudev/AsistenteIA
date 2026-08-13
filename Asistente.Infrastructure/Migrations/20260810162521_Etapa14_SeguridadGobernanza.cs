using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa14_SeguridadGobernanza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Resultado",
                table: "AuditoriaActividad",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoOperacion",
                table: "AuditoriaActividad",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AuditoriaIA",
                columns: table => new
                {
                    IdAuditoriaIA = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdConversacion = table.Column<int>(type: "int", nullable: true),
                    IdAsistente = table.Column<int>(type: "int", nullable: true),
                    AsistenteNombre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Pregunta = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromptUtilizado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HerramientasUtilizadas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FuentesConsultadas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Respuesta = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TiempoRespuestaMs = table.Column<long>(type: "bigint", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaIA", x => x.IdAuditoriaIA);
                });

            migrationBuilder.CreateTable(
                name: "MetricasIA",
                columns: table => new
                {
                    IdMetrica = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdAsistente = table.Column<int>(type: "int", nullable: true),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TiempoGeneracionMs = table.Column<long>(type: "bigint", nullable: false),
                    TiempoRecuperacionRagMs = table.Column<long>(type: "bigint", nullable: false),
                    TiempoConsultaSqlMs = table.Column<long>(type: "bigint", nullable: false),
                    TiempoHerramientasMs = table.Column<long>(type: "bigint", nullable: false),
                    DocumentosRecuperados = table.Column<int>(type: "int", nullable: false),
                    HerramientasEjecutadas = table.Column<int>(type: "int", nullable: false),
                    TokensEntrada = table.Column<int>(type: "int", nullable: true),
                    TokensSalida = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricasIA", x => x.IdMetrica);
                });

            migrationBuilder.CreateTable(
                name: "Permisos",
                columns: table => new
                {
                    IdPermiso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Modulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permisos", x => x.IdPermiso);
                });

            migrationBuilder.CreateTable(
                name: "PoliticasIA",
                columns: table => new
                {
                    IdPolitica = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticasIA", x => x.IdPolitica);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioAsistentes",
                columns: table => new
                {
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdAsistente = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioAsistentes", x => new { x.IdUsuario, x.IdAsistente });
                    table.ForeignKey(
                        name: "FK_UsuarioAsistentes_Asistente_IdAsistente",
                        column: x => x.IdAsistente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioAsistentes_Usuario_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FuenteConocimiento",
                columns: table => new
                {
                    IdFuente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Manual"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Prioridad = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()"),
                    UsuarioCreacion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuenteConocimiento", x => x.IdFuente);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionRAG",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MaxCaracteresContexto = table.Column<int>(type: "int", nullable: false, defaultValue: 8000),
                    MaxChunks = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    MaxChunksAlModelo = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    MaxFuentesConsultadas = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    MaxReferencias = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    MinScore = table.Column<double>(type: "float", nullable: false, defaultValue: 0.45),
                    TopKPorFuente = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    UsarDocumentosHistoricos = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionRAG", x => x.IdConfiguracion);
                });

            migrationBuilder.CreateTable(
                name: "AsistenteFuente",
                columns: table => new
                {
                    IdAsistente = table.Column<int>(type: "int", nullable: false),
                    IdFuente = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Prioridad = table.Column<int>(type: "int", nullable: false, defaultValue: 5)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsistenteFuente", x => new { x.IdAsistente, x.IdFuente });
                    table.ForeignKey(
                        name: "FK_AsistenteFuente_Asistente_IdAsistente",
                        column: x => x.IdAsistente,
                        principalTable: "Asistente",
                        principalColumn: "IdAsistente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsistenteFuente_FuenteConocimiento_IdFuente",
                        column: x => x.IdFuente,
                        principalTable: "FuenteConocimiento",
                        principalColumn: "IdFuente",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoFuente",
                columns: table => new
                {
                    IdDocumento = table.Column<int>(type: "int", nullable: false),
                    IdFuente = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoFuente", x => new { x.IdDocumento, x.IdFuente });
                    table.ForeignKey(
                        name: "FK_DocumentoFuente_Documento_IdDocumento",
                        column: x => x.IdDocumento,
                        principalTable: "Documento",
                        principalColumn: "IdDocumento",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentoFuente_FuenteConocimiento_IdFuente",
                        column: x => x.IdFuente,
                        principalTable: "FuenteConocimiento",
                        principalColumn: "IdFuente",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsistenteFuente_IdFuente",
                table: "AsistenteFuente",
                column: "IdFuente");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoFuente_IdFuente",
                table: "DocumentoFuente",
                column: "IdFuente");

            migrationBuilder.CreateTable(
                name: "UsuarioFuentes",
                columns: table => new
                {
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdFuente = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioFuentes", x => new { x.IdUsuario, x.IdFuente });
                    table.ForeignKey(
                        name: "FK_UsuarioFuentes_FuenteConocimiento_IdFuente",
                        column: x => x.IdFuente,
                        principalTable: "FuenteConocimiento",
                        principalColumn: "IdFuente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioFuentes_Usuario_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolPermisos",
                columns: table => new
                {
                    IdRol = table.Column<int>(type: "int", nullable: false),
                    IdPermiso = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolPermisos", x => new { x.IdRol, x.IdPermiso });
                    table.ForeignKey(
                        name: "FK_RolPermisos_Permisos_IdPermiso",
                        column: x => x.IdPermiso,
                        principalTable: "Permisos",
                        principalColumn: "IdPermiso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolPermisos_Rol_IdRol",
                        column: x => x.IdRol,
                        principalTable: "Rol",
                        principalColumn: "IdRol",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaIA_FechaHora",
                table: "AuditoriaIA",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaIA_IdUsuario",
                table: "AuditoriaIA",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_MetricasIA_FechaHora",
                table: "MetricasIA",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_Permisos_Codigo",
                table: "Permisos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolPermisos_IdPermiso",
                table: "RolPermisos",
                column: "IdPermiso");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioAsistentes_IdAsistente",
                table: "UsuarioAsistentes",
                column: "IdAsistente");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioFuentes_IdFuente",
                table: "UsuarioFuentes",
                column: "IdFuente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaIA");

            migrationBuilder.DropTable(
                name: "MetricasIA");

            migrationBuilder.DropTable(
                name: "PoliticasIA");

            migrationBuilder.DropTable(
                name: "RolPermisos");

            migrationBuilder.DropTable(
                name: "UsuarioAsistentes");

            migrationBuilder.DropTable(
                name: "UsuarioFuentes");

            migrationBuilder.DropTable(
                name: "Permisos");

            migrationBuilder.DropColumn(
                name: "Resultado",
                table: "AuditoriaActividad");

            migrationBuilder.DropColumn(
                name: "TipoOperacion",
                table: "AuditoriaActividad");
        }
    }
}
