using System.Collections.Generic;

namespace Asistente.Shared;

public class AgentExecutionResultDto
{
    public int IdExecution { get; set; }
    public bool Exitoso { get; set; }
    public string? RespuestaFinal { get; set; }
    public string Estado { get; set; } = "Completado";
    public long TiempoTotalMs { get; set; }
    public int CantidadAgentes { get; set; }
    public int ProfundidadAlcanzada { get; set; }
    public List<string> AgentesParticipantes { get; set; } = new();
    public List<AgentStepResultDto> Pasos { get; set; } = new();
    public List<ExecutionTraceDto> Trazas { get; set; } = new();
    public string? Error { get; set; }
}

public class AgentStepResultDto
{
    public int Orden { get; set; }
    public int IdAgente { get; set; }
    public string NombreAgente { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public long TiempoMs { get; set; }
    public string Estado { get; set; } = "Completado";
    public List<int> Dependencias { get; set; } = new();
}

public class ExecutionTraceDto
{
    public string Evento { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public System.DateTime FechaHora { get; set; }
}

public class AgentCollaborationRuleDto
{
    public int IdRule { get; set; }
    public int AgenteOrigen { get; set; }
    public int AgenteDestino { get; set; }
    public bool Permitido { get; set; }
    public int Prioridad { get; set; } = 100;
    public bool Activa { get; set; } = true;
    public string? NombreOrigen { get; set; }
    public string? NombreDestino { get; set; }
}

public class AgenteSimpleDto
{
    public int IdAsistente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public int Estado { get; set; }
}

// ===== Planner Engine (ETAPA 18) =====
public class PlanDto
{
    public int IdPlan { get; set; }
    public string Objetivo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public bool RequiereAprobacion { get; set; }
    public bool Aprobado { get; set; }
    public string? IdExecution { get; set; }
    public long? TiempoTotalMs { get; set; }
    public string? Razonamiento { get; set; }
    public List<PlanStepDto> Pasos { get; set; } = new();
    public List<PlanDepDto> Dependencias { get; set; } = new();
    public List<PlanLogDto> Logs { get; set; } = new();
}

public class PlanStepDto
{
    public int IdStep { get; set; }
    public int Orden { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public int? IdAsistente { get; set; }
    public string? CodigoHerramienta { get; set; }
    public int Intentos { get; set; }
}

public class PlanDepDto { public int StepOrigen { get; set; } public int StepDestino { get; set; } }
public class PlanLogDto { public string Evento { get; set; } = string.Empty; public string? Detalle { get; set; } public System.DateTime Fecha { get; set; } }

public class PlannerDashboardDto
{
    public int Total { get; set; }
    public int Activos { get; set; }
    public int Finalizados { get; set; }
    public int Fallidos { get; set; }
    public long TiempoPromedioMs { get; set; }
    public List<PlanDto> Planes { get; set; } = new();
}

public class ResultadoValidacionPlanDto
{
    public bool Valido { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
    public List<string> Riesgos { get; set; } = new();
}
