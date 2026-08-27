using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa19CentroAprobaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalPolicy",
                columns: table => new
                {
                    IdPolicy = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CantidadMinimaAprobaciones = table.Column<int>(type: "int", nullable: false),
                    RequiereUnanimidad = table.Column<bool>(type: "bit", nullable: false),
                    PermiteDelegacion = table.Column<bool>(type: "bit", nullable: false),
                    TiempoMaximoHoras = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalPolicy", x => x.IdPolicy);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequest",
                columns: table => new
                {
                    IdApproval = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdPlan = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Solicitante = table.Column<int>(type: "int", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdPolicy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequest", x => x.IdApproval);
                    table.ForeignKey(
                        name: "FK_ApprovalRequest_ApprovalPolicy_IdPolicy",
                        column: x => x.IdPolicy,
                        principalTable: "ApprovalPolicy",
                        principalColumn: "IdPolicy",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalAssignee",
                columns: table => new
                {
                    IdAssignee = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdApproval = table.Column<int>(type: "int", nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    EsPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalAssignee", x => x.IdAssignee);
                    table.ForeignKey(
                        name: "FK_ApprovalAssignee_ApprovalRequest_IdApproval",
                        column: x => x.IdApproval,
                        principalTable: "ApprovalRequest",
                        principalColumn: "IdApproval",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalDecision",
                columns: table => new
                {
                    IdDecision = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdApproval = table.Column<int>(type: "int", nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaDecision = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDecision", x => x.IdDecision);
                    table.ForeignKey(
                        name: "FK_ApprovalDecision_ApprovalRequest_IdApproval",
                        column: x => x.IdApproval,
                        principalTable: "ApprovalRequest",
                        principalColumn: "IdApproval",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalAssignee_IdApproval",
                table: "ApprovalAssignee",
                column: "IdApproval");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecision_IdApproval",
                table: "ApprovalDecision",
                column: "IdApproval");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequest_IdPolicy",
                table: "ApprovalRequest",
                column: "IdPolicy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalAssignee");

            migrationBuilder.DropTable(
                name: "ApprovalDecision");

            migrationBuilder.DropTable(
                name: "ApprovalRequest");

            migrationBuilder.DropTable(
                name: "ApprovalPolicy");
        }
    }
}
