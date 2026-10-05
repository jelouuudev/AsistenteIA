using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorregirRelacionUsuarioAsistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsuarioAsistentes_Asistente_AsistenteIdAsistente",
                table: "UsuarioAsistentes");

            migrationBuilder.DropIndex(
                name: "IX_UsuarioAsistentes_AsistenteIdAsistente",
                table: "UsuarioAsistentes");

            migrationBuilder.DropColumn(
                name: "AsistenteIdAsistente",
                table: "UsuarioAsistentes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AsistenteIdAsistente",
                table: "UsuarioAsistentes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioAsistentes_AsistenteIdAsistente",
                table: "UsuarioAsistentes",
                column: "AsistenteIdAsistente");

            migrationBuilder.AddForeignKey(
                name: "FK_UsuarioAsistentes_Asistente_AsistenteIdAsistente",
                table: "UsuarioAsistentes",
                column: "AsistenteIdAsistente",
                principalTable: "Asistente",
                principalColumn: "IdAsistente");
        }
    }
}
