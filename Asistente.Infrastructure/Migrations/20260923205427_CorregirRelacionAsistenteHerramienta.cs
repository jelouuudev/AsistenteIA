using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorregirRelacionAsistenteHerramienta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AsistentesHerramientas_Asistente_AsistenteIdAsistente",
                table: "AsistentesHerramientas");

            migrationBuilder.DropIndex(
                name: "IX_AsistentesHerramientas_AsistenteIdAsistente",
                table: "AsistentesHerramientas");

            migrationBuilder.DropColumn(
                name: "AsistenteIdAsistente",
                table: "AsistentesHerramientas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AsistenteIdAsistente",
                table: "AsistentesHerramientas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsistentesHerramientas_AsistenteIdAsistente",
                table: "AsistentesHerramientas",
                column: "AsistenteIdAsistente");

            migrationBuilder.AddForeignKey(
                name: "FK_AsistentesHerramientas_Asistente_AsistenteIdAsistente",
                table: "AsistentesHerramientas",
                column: "AsistenteIdAsistente",
                principalTable: "Asistente",
                principalColumn: "IdAsistente");
        }
    }
}
