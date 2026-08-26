using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Paso de un flujo de trabajo. Cada paso ejecuta una herramienta autorizada del
/// Motor de Herramientas (Tool Orchestrator) de forma secuencial y ordenada.
/// </summary>
public class WorkflowPaso
{
    public int IdPaso { get; set; }
    public int IdWorkflow { get; set; }
    public int Orden { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Código de la herramienta a ejecutar (debe coincidir con Herramienta.Codigo / ITool.Name).</summary>
    public string Herramienta { get; set; } = string.Empty;

    /// <summary>Parámetros en formato JSON. Soporta tokens {{resultado}} y {{PasoN}} del contexto compartido.</summary>
    public string? Parametros { get; set; }

    public bool RequiereConfirmacion { get; set; }
    public int ReintentosMaximos { get; set; } = 0;
    public int TiempoMaximoMs { get; set; } = 30000;

    /// <summary>Estrategia ante fallo persistente del paso.</summary>
    public EstrategiaError EstrategiaError { get; set; } = EstrategiaError.Cancelar;

    public Workflow? Workflow { get; set; }
}
