using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdAsistenteToConversacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdAsistente",
                table: "Conversacion",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdAsistente",
                table: "Conversacion");
        }
    }
}
