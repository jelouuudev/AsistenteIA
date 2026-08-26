using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUltimoDocumentoPreferido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UltimoDocumentoPreferido",
                table: "Conversacion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModeloIA",
                table: "Asistente",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "qwen2.5:14b",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "qwen2.5:7b");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimoDocumentoPreferido",
                table: "Conversacion");

            migrationBuilder.AlterColumn<string>(
                name: "ModeloIA",
                table: "Asistente",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "qwen2.5:7b",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "qwen2.5:14b");
        }
    }
}
