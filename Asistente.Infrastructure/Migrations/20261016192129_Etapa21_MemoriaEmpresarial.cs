using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa21_MemoriaEmpresarial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemoryAccessLogs",
                columns: table => new
                {
                    IdLog = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMemory = table.Column<int>(type: "int", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: true),
                    IdAsistente = table.Column<int>(type: "int", nullable: true),
                    Operacion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryAccessLogs", x => x.IdLog);
                });

            migrationBuilder.CreateTable(
                name: "MemoryCategories",
                columns: table => new
                {
                    IdCategory = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryCategories", x => x.IdCategory);
                });

            migrationBuilder.CreateTable(
                name: "MemoryScopes",
                columns: table => new
                {
                    IdScope = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryScopes", x => x.IdScope);
                });

            migrationBuilder.CreateTable(
                name: "MemoryRecords",
                columns: table => new
                {
                    IdMemory = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdScope = table.Column<int>(type: "int", nullable: false),
                    IdPropietario = table.Column<int>(type: "int", nullable: true),
                    IdCategory = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    VersionActual = table.Column<int>(type: "int", nullable: false),
                    RequierePermiso = table.Column<bool>(type: "bit", nullable: false),
                    VectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Indexada = table.Column<bool>(type: "bit", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryRecords", x => x.IdMemory);
                    table.ForeignKey(
                        name: "FK_MemoryRecords_MemoryCategories_IdCategory",
                        column: x => x.IdCategory,
                        principalTable: "MemoryCategories",
                        principalColumn: "IdCategory",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MemoryRecords_MemoryScopes_IdScope",
                        column: x => x.IdScope,
                        principalTable: "MemoryScopes",
                        principalColumn: "IdScope",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemoryRetentionPolicies",
                columns: table => new
                {
                    IdPolicy = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdScope = table.Column<int>(type: "int", nullable: true),
                    DiasRetencion = table.Column<int>(type: "int", nullable: false),
                    EliminarAlExpirar = table.Column<bool>(type: "bit", nullable: false),
                    ConservarTrazabilidad = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryRetentionPolicies", x => x.IdPolicy);
                    table.ForeignKey(
                        name: "FK_MemoryRetentionPolicies_MemoryScopes_IdScope",
                        column: x => x.IdScope,
                        principalTable: "MemoryScopes",
                        principalColumn: "IdScope",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MemoryVersions",
                columns: table => new
                {
                    IdVersion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMemory = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdUsuario = table.Column<int>(type: "int", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryVersions", x => x.IdVersion);
                    table.ForeignKey(
                        name: "FK_MemoryVersions_MemoryRecords_IdMemory",
                        column: x => x.IdMemory,
                        principalTable: "MemoryRecords",
                        principalColumn: "IdMemory",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryAccessLogs_Codigo_Fecha",
                table: "MemoryAccessLogs",
                columns: new[] { "Codigo", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryAccessLogs_IdMemory_Fecha",
                table: "MemoryAccessLogs",
                columns: new[] { "IdMemory", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryCategories_Codigo",
                table: "MemoryCategories",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryRecords_Codigo",
                table: "MemoryRecords",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryRecords_IdCategory",
                table: "MemoryRecords",
                column: "IdCategory");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryRecords_IdScope_Activo",
                table: "MemoryRecords",
                columns: new[] { "IdScope", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryRetentionPolicies_IdScope",
                table: "MemoryRetentionPolicies",
                column: "IdScope");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryScopes_Codigo",
                table: "MemoryScopes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryVersions_IdMemory_Numero",
                table: "MemoryVersions",
                columns: new[] { "IdMemory", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryAccessLogs");

            migrationBuilder.DropTable(
                name: "MemoryRetentionPolicies");

            migrationBuilder.DropTable(
                name: "MemoryVersions");

            migrationBuilder.DropTable(
                name: "MemoryRecords");

            migrationBuilder.DropTable(
                name: "MemoryCategories");

            migrationBuilder.DropTable(
                name: "MemoryScopes");
        }
    }
}
