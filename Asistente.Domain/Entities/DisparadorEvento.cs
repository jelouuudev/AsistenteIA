using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Origen configurable que enciende un evento sin intervención del usuario.
/// Permite crear cualquier evento automático 100% desde la UI.
/// Tipos ("Tipo"): Cron (por horario), SondeoBD (polling SQL) y Documento
/// (al procesarse un documento que cumpla filtros).
/// La configuración específica de cada tipo vive en ConfigJson.
/// </summary>
public class DisparadorEvento
{
    public int IdDisparador { get; set; }
    public int IdEvento { get; set; }

    /// <summary>Cron | SondeoBD | Documento.</summary>
    public string Tipo { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Cron: {"cron":"0 * * * * ?","contexto":{...}}.
    /// SondeoBD: {"idConexion":N,"consultaSql":"SELECT ...","intervaloSegundos":300,"modo":"PorFila|SiHayFilas","maxFilas":50,"contextoFijo":{...}}.
    /// Documento: {"idCategoria":null|N,"codigoDocumento":null|"..."}. Vacío/null = cualquier documento.
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    public DateTime? UltimaEjecucion { get; set; }
    public DateTime? ProximaEjecucion { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int UsuarioCreacion { get; set; } = 1;

    // Navegación
    public EventoEmpresarial? Evento { get; set; }

    public static class Tipos
    {
        public const string Cron = "Cron";
        public const string SondeoBD = "SondeoBD";
        public const string Documento = "Documento";
    }
}
