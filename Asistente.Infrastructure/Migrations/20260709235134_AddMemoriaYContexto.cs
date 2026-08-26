using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoriaYContexto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimaActividad",
                table: "Conversacion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumenContexto",
                table: "Conversacion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "Conversacion",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalMensajes",
                table: "Conversacion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioPropietario",
                table: "Conversacion",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
                oldDefaultValue: "deepseek-r1:7b");

            migrationBuilder.CreateTable(
                name: "ConfiguracionMemoria",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaximoMensajesContexto = table.Column<int>(type: "int", nullable: false, defaultValue: 20),
                    MaximoTokensContexto = table.Column<int>(type: "int", nullable: false, defaultValue: 4096),
                    LongitudResumen = table.Column<int>(type: "int", nullable: false, defaultValue: 500),
                    CantidadConversacionesVisibles = table.Column<int>(type: "int", nullable: false, defaultValue: 50),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionMemoria", x => x.IdConfiguracion);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversacion_FechaUltimaActividad",
                table: "Conversacion",
                column: "FechaUltimaActividad");

            migrationBuilder.CreateIndex(
                name: "IX_Conversacion_UsuarioPropietario",
                table: "Conversacion",
                column: "UsuarioPropietario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionMemoria");

            migrationBuilder.DropIndex(
                name: "IX_Conversacion_FechaUltimaActividad",
                table: "Conversacion");

            migrationBuilder.DropIndex(
                name: "IX_Conversacion_UsuarioPropietario",
                table: "Conversacion");

            migrationBuilder.DropColumn(
                name: "FechaUltimaActividad",
                table: "Conversacion");

            migrationBuilder.DropColumn(
                name: "ResumenContexto",
                table: "Conversacion");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "Conversacion");

            migrationBuilder.DropColumn(
                name: "TotalMensajes",
                table: "Conversacion");

            migrationBuilder.DropColumn(
                name: "UsuarioPropietario",
                table: "Conversacion");

            migrationBuilder.AlterColumn<string>(
                name: "ModeloIA",
                table: "Asistente",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "deepseek-r1:7b",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "qwen2.5:7b");
        }
    }
}
