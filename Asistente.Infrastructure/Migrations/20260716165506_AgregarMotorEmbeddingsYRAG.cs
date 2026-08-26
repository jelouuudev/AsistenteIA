using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMotorEmbeddingsYRAG : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentoIndexado",
                columns: table => new
                {
                    IdDocumentoIndexado = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDocumentoProcesado = table.Column<int>(type: "int", nullable: false),
                    FechaIndexacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalChunks = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalEmbeddings = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoIndexado", x => x.IdDocumentoIndexado);
                    table.ForeignKey(
                        name: "FK_DocumentoIndexado_DocumentoProcesado_IdDocumentoProcesado",
                        column: x => x.IdDocumentoProcesado,
                        principalTable: "DocumentoProcesado",
                        principalColumn: "IdDocumentoProcesado",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingConfiguracion",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Proveedor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModeloEmbeddings = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseVectorial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CantidadResultados = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    PuntajeMinimo = table.Column<double>(type: "float", nullable: false, defaultValue: 0.5),
                    LongitudMaximaContexto = table.Column<int>(type: "int", nullable: false, defaultValue: 4000),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingConfiguracion", x => x.IdConfiguracion);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoIndexado_Estado",
                table: "DocumentoIndexado",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoIndexado_IdDocumentoProcesado",
                table: "DocumentoIndexado",
                column: "IdDocumentoProcesado");

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingConfiguracion_Activo",
                table: "EmbeddingConfiguracion",
                column: "Activo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentoIndexado");

            migrationBuilder.DropTable(
                name: "EmbeddingConfiguracion");
        }
    }
}
