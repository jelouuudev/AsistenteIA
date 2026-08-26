using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMotorConsultasEmpresariales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConexionesBaseDatos",
                columns: table => new
                {
                    IdConexion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Servidor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BaseDatos = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UsuarioConexion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CadenaConexionCifrada = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConexionesBaseDatos", x => x.IdConexion);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesMotorConsultas",
                columns: table => new
                {
                    IdConfiguracion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TiempoMaximoEjecucionSegundos = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    MaximoRegistros = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    MaxConsultasSimultaneas = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    IdConexionPredeterminada = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesMotorConsultas", x => x.IdConfiguracion);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesMotorConsultas_ConexionesBaseDatos_IdConexionPredeterminada",
                        column: x => x.IdConexionPredeterminada,
                        principalTable: "ConexionesBaseDatos",
                        principalColumn: "IdConexion",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConsultasEjecutadas",
                columns: table => new
                {
                    IdConsulta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdConexion = table.Column<int>(type: "int", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    PreguntaUsuario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OperacionEjecutada = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConsultaGenerada = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TiempoEjecucion = table.Column<long>(type: "bigint", nullable: false),
                    CantidadRegistros = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultasEjecutadas", x => x.IdConsulta);
                    table.ForeignKey(
                        name: "FK_ConsultasEjecutadas_ConexionesBaseDatos_IdConexion",
                        column: x => x.IdConexion,
                        principalTable: "ConexionesBaseDatos",
                        principalColumn: "IdConexion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultasEjecutadas_Usuario_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuario",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultasPlantillas",
                columns: table => new
                {
                    IdPlantilla = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdConexion = table.Column<int>(type: "int", nullable: true),
                    ConsultaSql = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Parametros = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultasPlantillas", x => x.IdPlantilla);
                    table.ForeignKey(
                        name: "FK_ConsultasPlantillas_ConexionesBaseDatos_IdConexion",
                        column: x => x.IdConexion,
                        principalTable: "ConexionesBaseDatos",
                        principalColumn: "IdConexion",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TablasAutorizadas",
                columns: table => new
                {
                    IdTabla = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConexion = table.Column<int>(type: "int", nullable: false),
                    NombreTabla = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Esquema = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TablasAutorizadas", x => x.IdTabla);
                    table.ForeignKey(
                        name: "FK_TablasAutorizadas_ConexionesBaseDatos_IdConexion",
                        column: x => x.IdConexion,
                        principalTable: "ConexionesBaseDatos",
                        principalColumn: "IdConexion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VistasAutorizadas",
                columns: table => new
                {
                    IdVista = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConexion = table.Column<int>(type: "int", nullable: false),
                    NombreVista = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VistasAutorizadas", x => x.IdVista);
                    table.ForeignKey(
                        name: "FK_VistasAutorizadas_ConexionesBaseDatos_IdConexion",
                        column: x => x.IdConexion,
                        principalTable: "ConexionesBaseDatos",
                        principalColumn: "IdConexion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConexionesBaseDatos_Nombre",
                table: "ConexionesBaseDatos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesMotorConsultas_Activo",
                table: "ConfiguracionesMotorConsultas",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesMotorConsultas_IdConexionPredeterminada",
                table: "ConfiguracionesMotorConsultas",
                column: "IdConexionPredeterminada");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasEjecutadas_FechaHora",
                table: "ConsultasEjecutadas",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasEjecutadas_IdConexion",
                table: "ConsultasEjecutadas",
                column: "IdConexion");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasEjecutadas_IdUsuario",
                table: "ConsultasEjecutadas",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasPlantillas_IdConexion",
                table: "ConsultasPlantillas",
                column: "IdConexion");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasPlantillas_Nombre",
                table: "ConsultasPlantillas",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TablasAutorizadas_IdConexion_Esquema_NombreTabla",
                table: "TablasAutorizadas",
                columns: new[] { "IdConexion", "Esquema", "NombreTabla" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VistasAutorizadas_IdConexion_NombreVista",
                table: "VistasAutorizadas",
                columns: new[] { "IdConexion", "NombreVista" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesMotorConsultas");

            migrationBuilder.DropTable(
                name: "ConsultasEjecutadas");

            migrationBuilder.DropTable(
                name: "ConsultasPlantillas");

            migrationBuilder.DropTable(
                name: "TablasAutorizadas");

            migrationBuilder.DropTable(
                name: "VistasAutorizadas");

            migrationBuilder.DropTable(
                name: "ConexionesBaseDatos");
        }
    }
}
