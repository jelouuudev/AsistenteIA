using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarGestorDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriaDocumento",
                columns: table => new
                {
                    IdCategoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaDocumento", x => x.IdCategoria);
                });

            migrationBuilder.CreateTable(
                name: "Documento",
                columns: table => new
                {
                    IdDocumento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    VersionActual = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PendienteProcesamiento = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioRegistro = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documento", x => x.IdDocumento);
                    table.ForeignKey(
                        name: "FK_Documento_CategoriaDocumento_IdCategoria",
                        column: x => x.IdCategoria,
                        principalTable: "CategoriaDocumento",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaDocumental",
                columns: table => new
                {
                    IdAuditoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDocumento = table.Column<int>(type: "int", nullable: false),
                    IdVersion = table.Column<int>(type: "int", nullable: true),
                    Accion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    FechaAccion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DireccionIP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaDocumental", x => x.IdAuditoria);
                    table.ForeignKey(
                        name: "FK_AuditoriaDocumental_Documento_IdDocumento",
                        column: x => x.IdDocumento,
                        principalTable: "Documento",
                        principalColumn: "IdDocumento",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoVersion",
                columns: table => new
                {
                    IdVersion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDocumento = table.Column<int>(type: "int", nullable: false),
                    NumeroVersion = table.Column<int>(type: "int", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RutaArchivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TamanoArchivo = table.Column<long>(type: "bigint", nullable: false),
                    HashArchivo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCarga = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoVersion", x => x.IdVersion);
                    table.ForeignKey(
                        name: "FK_DocumentoVersion_Documento_IdDocumento",
                        column: x => x.IdDocumento,
                        principalTable: "Documento",
                        principalColumn: "IdDocumento",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaDocumental_IdDocumento",
                table: "AuditoriaDocumental",
                column: "IdDocumento");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaDocumento_Nombre",
                table: "CategoriaDocumento",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documento_Codigo",
                table: "Documento",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documento_IdCategoria",
                table: "Documento",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoVersion_IdDocumento",
                table: "DocumentoVersion",
                column: "IdDocumento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaDocumental");

            migrationBuilder.DropTable(
                name: "DocumentoVersion");

            migrationBuilder.DropTable(
                name: "Documento");

            migrationBuilder.DropTable(
                name: "CategoriaDocumento");
        }
    }
}
