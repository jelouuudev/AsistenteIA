using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Infrastructure.Migrations
{
    /// <summary>
    /// Corrige el nombre de la tabla de configuración de RAG.
    ///
    /// La migración 20260810162521_Etapa14_SeguridadGobernanza creó la tabla como
    /// 'ConfiguracionRAG' (singular) mientras el modelo, el script legado
    /// scripts/Etapa9_ConfiguracionRAG.sql y todas las migraciones posteriores la
    /// nombran 'ConfiguracionesRAG' (plural). El rename nunca se aplicó, así que el
    /// modelo y la base llevan desalineados desde el 10 de agosto de 2026: en cuanto
    /// una pregunta dispara RAG, RecuperacionService lanza
    /// "Invalid object name 'ConfiguracionesRAG'" y POST /api/Chat/enviar devuelve 500.
    ///
    /// Se usa sp_rename con guarda en lugar de RenameTable para que la migración
    /// sea inocua en bases donde el nombre ya sea correcto (y se pueda reintentar).
    /// </summary>
    [DbContext(typeof(Data.AsistenteDbContext))]
    [Migration("20261017180000_CorregirNombreConfiguracionRAG")]
    public partial class CorregirNombreConfiguracionRAG : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ConfiguracionRAG]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[ConfiguracionesRAG]', N'U') IS NULL
    EXEC sp_rename N'[dbo].[ConfiguracionRAG]', N'ConfiguracionesRAG';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ConfiguracionesRAG]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[ConfiguracionRAG]', N'U') IS NULL
    EXEC sp_rename N'[dbo].[ConfiguracionesRAG]', N'ConfiguracionRAG';");
        }
    }
}