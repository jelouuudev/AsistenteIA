using System.Collections.Generic;

namespace Asistente.Shared;

// ---- Workflow ----
public class WorkflowDto
{
    public int IdWorkflow { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Disparadores { get; set; }
    public int Version { get; set; }
    public string Estado { get; set; } = "Borrador";
    public System.DateTime FechaCreacion { get; set; }
    public int UsuarioCreacion { get; set; }
    public int CantidadPasos { get; set; }
    public List<WorkflowPasoDto> Pasos { get; set; } = new();
}

public class CrearWorkflowRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Disparadores { get; set; }
    public int UsuarioCreacion { get; set; }
    public List<WorkflowPasoRequest>? Pasos { get; set; }
}

public class ActualizarWorkflowRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Disparadores { get; set; }
    public List<WorkflowPasoRequest>? Pasos { get; set; }
}

// ---- Workflow Paso ----
public class WorkflowPasoDto
{
    public int IdPaso { get; set; }
    public int IdWorkflow { get; set; }
    public int Orden { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Herramienta { get; set; } = string.Empty;
    public string? Parametros { get; set; }
    public bool RequiereConfirmacion { get; set; }
    public int ReintentosMaximos { get; set; }
    public int TiempoMaximoMs { get; set; }
    public string EstrategiaError { get; set; } = "Cancelar";
}

// DTO de paso usado en formularios (Crear / Editar)
public class WorkflowPasoRequest
{
    public int Orden { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Herramienta { get; set; } = string.Empty;
    public string? Parametros { get; set; }
    public bool RequiereConfirmacion { get; set; }
    public int ReintentosMaximos { get; set; } = 1;
    public int TiempoMaximoMs { get; set; } = 60000;
    public string EstrategiaError { get; set; } = "Cancelar";
}

// ---- Ejecución ----
public class WorkflowEjecucionDto
{
    public int IdEjecucion { get; set; }
    public int IdWorkflow { get; set; }
    public string NombreWorkflow { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public System.DateTime FechaInicio { get; set; }
    public System.DateTime? FechaFin { get; set; }
    public string Estado { get; set; } = string.Empty;
    public long? TiempoTotalMs { get; set; }
    public string? ResultadoFinal { get; set; }
    public List<WorkflowPasoEjecucionDto> Pasos { get; set; } = new();
}

public class WorkflowPasoEjecucionDto
{
    public int IdPasoEjecucion { get; set; }
    public int IdPaso { get; set; }
    public string NombrePaso { get; set; } = string.Empty;
    public System.DateTime FechaInicio { get; set; }
    public System.DateTime? FechaFin { get; set; }
    public string? Resultado { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
}

// ---- Configuración ----
public class ConfiguracionWorkflowDto
{
    public int ReintentosMaximos { get; set; }
    public int TiempoMaximoPasoMs { get; set; }
    public int TiempoMaximoFlujoMs { get; set; }
    public bool ConfirmacionesObligatorias { get; set; }
    public int LimitePasosPorWorkflow { get; set; }
}

// ---- Uso en chat ----
public class WorkflowUsoChatDto
{
    public int IdWorkflow { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public long TiempoMs { get; set; }
    public string? Mensaje { get; set; }
}
