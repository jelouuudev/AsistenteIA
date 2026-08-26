using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Paso (nodo) de una ejecución del Orchestrator. Cada paso corresponde a la
/// invocación de un agente colaborador, una herramienta o una consulta RAG,
/// dentro del Execution Graph.
/// </summary>
public class AgentExecutionStep
{
    public int IdStep { get; set; }
    public int IdExecution { get; set; }
    public int Orden { get; set; }
    public int IdAgente { get; set; }
    public string Accion { get; set; } = string.Empty; // Ej: "Consultar SQL", "Recuperar RAG", "Generar Resumen"
    public string? Resultado { get; set; }
    public long TiempoMs { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente | EnEjecucion | Completado | Error | Omitido
    public string? Error { get; set; }

    // Dependencias: ids de pasos que deben completarse antes (para grafo secuencial/paralelo)
    public string? Dependencias { get; set; } // JSON array de int

    public AgentExecution? Ejecucion { get; set; }
    public Asistente? Agente { get; set; }
}
