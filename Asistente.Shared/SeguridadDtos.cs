using System;
using System.Collections.Generic;

namespace Asistente.Shared;

// ---- Permiso ----
public class PermisoDto
{
    public int IdPermiso { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

public class CrearPermisoRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = string.Empty;
}

// ---- Asistente autorizado ----
public class AsistenteAutorizadoDto
{
    public int IdAsistente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Autorizado { get; set; }
}

public class AsignarAsistentesUsuarioRequest
{
    public int IdUsuario { get; set; }
    public List<int> IdsAsistentes { get; set; } = new();
}

// ---- Fuente autorizada ----
public class FuenteAutorizadaDto
{
    public int IdFuente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Autorizada { get; set; }
}

public class AsignarFuentesUsuarioRequest
{
    public int IdUsuario { get; set; }
    public List<int> IdsFuentes { get; set; } = new();
}

// ---- Política IA ----
public class PoliticaIADto
{
    public int IdPolitica { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public bool Activa { get; set; }
}

public class CrearPoliticaIARequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
}

// ---- Auditoría IA ----
public class AuditoriaIADto
{
    public int IdAuditoriaIA { get; set; }
    public int IdUsuario { get; set; }
    public int? IdConversacion { get; set; }
    public string? AsistenteNombre { get; set; }
    public string Modelo { get; set; } = string.Empty;
    public string Pregunta { get; set; } = string.Empty;
    public string Respuesta { get; set; } = string.Empty;
    public long TiempoRespuestaMs { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
}

// ---- Métricas ----
public class MetricasIADto
{
    public int IdMetrica { get; set; }
    public int IdUsuario { get; set; }
    public DateTime FechaHora { get; set; }
    public long TiempoGeneracionMs { get; set; }
    public long TiempoRecuperacionRagMs { get; set; }
    public int DocumentosRecuperados { get; set; }
    public int HerramientasEjecutadas { get; set; }
    public int? TokensEntrada { get; set; }
    public int? TokensSalida { get; set; }
}

// ---- Dashboard ----
public class DashboardSeguridadDto
{
    public int UsuariosActivos { get; set; }
    public int Conversaciones { get; set; }
    public int ConsultasRealizadas { get; set; }
    public int UsoHerramientas { get; set; }
    public int ConsultasSql { get; set; }
    public int WorkflowsEjecutados { get; set; }
    public int EventosProcesados { get; set; }
    public int Errores { get; set; }
    public double TiempoPromedioRespuestaMs { get; set; }
    public List<AuditoriaActividadDto> UltimasAuditorias { get; set; } = new();
}

// ---- Resultado de autorización ----
public class ResultadoAutorizacion
{
    public bool Permitido { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
