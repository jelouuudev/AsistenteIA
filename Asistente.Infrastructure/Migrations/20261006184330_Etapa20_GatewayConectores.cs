using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa20_GatewayConectores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Connectors",
                columns: table => new
                {
                    IdConnector = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "REST"),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    RequierePermiso = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connectors", x => x.IdConnector);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorConfiguraciones",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConnector = table.Column<int>(type: "int", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorConfiguraciones", x => x.IdConfiguracion);
                    table.ForeignKey(
                        name: "FK_ConnectorConfiguraciones_Connectors_IdConnector",
                        column: x => x.IdConnector,
                        principalTable: "Connectors",
                        principalColumn: "IdConnector",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorCredenciales",
                columns: table => new
                {
                    IdCredencial = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConnector = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "None"),
                    NombreUsuario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValorCifrado = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ParametrosCifrados = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorCredenciales", x => x.IdCredencial);
                    table.ForeignKey(
                        name: "FK_ConnectorCredenciales_Connectors_IdConnector",
                        column: x => x.IdConnector,
                        principalTable: "Connectors",
                        principalColumn: "IdConnector",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorEjecuciones",
                columns: table => new
                {
                    IdEjecucion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConnector = table.Column<int>(type: "int", nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: true),
                    IdAsistente = table.Column<int>(type: "int", nullable: true),
                    Operacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodigoRespuesta = table.Column<int>(type: "int", nullable: true),
                    LatenciaMs = table.Column<long>(type: "bigint", nullable: false),
                    Reintentos = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RespuestaResumen = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorEjecuciones", x => x.IdEjecucion);
                    table.ForeignKey(
                        name: "FK_ConnectorEjecuciones_Connectors_IdConnector",
                        column: x => x.IdConnector,
                        principalTable: "Connectors",
                        principalColumn: "IdConnector",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorPoliticas",
                columns: table => new
                {
                    IdPolitica = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConnector = table.Column<int>(type: "int", nullable: false),
                    TimeoutSegundos = table.Column<int>(type: "int", nullable: false),
                    MaxReintentos = table.Column<int>(type: "int", nullable: false),
                    IntervaloReintentoMs = table.Column<int>(type: "int", nullable: false),
                    RateLimitPorMinuto = table.Column<int>(type: "int", nullable: false),
                    CircuitBreakerUmbralFallos = table.Column<int>(type: "int", nullable: false),
                    CircuitBreakerSegundosAbierto = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorPoliticas", x => x.IdPolitica);
                    table.ForeignKey(
                        name: "FK_ConnectorPoliticas_Connectors_IdConnector",
                        column: x => x.IdConnector,
                        principalTable: "Connectors",
                        principalColumn: "IdConnector",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorConfiguraciones_IdConnector_Clave",
                table: "ConnectorConfiguraciones",
                columns: new[] { "IdConnector", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorCredenciales_IdConnector",
                table: "ConnectorCredenciales",
                column: "IdConnector");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorEjecuciones_IdConnector_Fecha",
                table: "ConnectorEjecuciones",
                columns: new[] { "IdConnector", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorPoliticas_IdConnector",
                table: "ConnectorPoliticas",
                column: "IdConnector");

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_Codigo",
                table: "Connectors",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectorConfiguraciones");

            migrationBuilder.DropTable(
                name: "ConnectorCredenciales");

            migrationBuilder.DropTable(
                name: "ConnectorEjecuciones");

            migrationBuilder.DropTable(
                name: "ConnectorPoliticas");

            migrationBuilder.DropTable(
                name: "Connectors");
        }
    }
}
