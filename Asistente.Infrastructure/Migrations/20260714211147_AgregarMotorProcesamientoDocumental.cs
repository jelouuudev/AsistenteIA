using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMotorProcesamientoDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentoProcesado",
                columns: table => new
                {
                    IdDocumentoProcesado = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdVersionDocumento = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalPaginas = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalCaracteres = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalChunks = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoProcesado", x => x.IdDocumentoProcesado);
                    table.ForeignKey(
                        name: "FK_DocumentoProcesado_DocumentoVersion_IdVersionDocumento",
                        column: x => x.IdVersionDocumento,
                        principalTable: "DocumentoVersion",
                        principalColumn: "IdVersion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoChunk",
                columns: table => new
                {
                    IdChunk = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDocumentoProcesado = table.Column<int>(type: "int", nullable: false),
                    NumeroChunk = table.Column<int>(type: "int", nullable: false),
                    PaginaInicial = table.Column<int>(type: "int", nullable: false),
                    PaginaFinal = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalCaracteres = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoChunk", x => x.IdChunk);
                    table.ForeignKey(
                        name: "FK_DocumentoChunk_DocumentoProcesado_IdDocumentoProcesado",
                        column: x => x.IdDocumentoProcesado,
                        principalTable: "DocumentoProcesado",
                        principalColumn: "IdDocumentoProcesado",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoChunk_IdDocumentoProcesado",
                table: "DocumentoChunk",
                column: "IdDocumentoProcesado");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoProcesado_Estado",
                table: "DocumentoProcesado",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoProcesado_IdVersionDocumento",
                table: "DocumentoProcesado",
                column: "IdVersionDocumento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentoChunk");

            migrationBuilder.DropTable(
                name: "DocumentoProcesado");
        }
    }
}
