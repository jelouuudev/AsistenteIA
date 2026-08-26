using System;
using System.Collections.Generic;
using System.Linq;

namespace Asistente.Application.Orchestrator;

/// <summary>
/// Nodo del Execution Graph (mejora arquitectónica recomendada en el RF):
/// cada nodo es una tarea (invocar un agente, ejecutar Tool o consultar RAG).
/// El Orchestrator resuelve qué nodos corren en paralelo y cuáles esperan resultados previos.
/// </summary>
public class ExecutionNode
{
    public int IdNodo { get; set; }
    public int IdAgente { get; set; }
    public string NombreAgente { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string PreguntaAsignada { get; set; } = string.Empty;
    public List<int> DependeDe { get; set; } = new(); // ids de nodos previos requeridos
    public int Profundidad { get; set; } = 0;

    public string? Resultado { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente|EnEjecucion|Completado|Error|Omitido
    public string? Error { get; set; }
    public long TiempoMs { get; set; }
}

/// <summary>
/// Grafo de ejecución. Resuelve el orden topológico y agrupa en "capas" para
/// paralelizar tareas independientes (RF Actividades 5 y 6).
/// </summary>
public class ExecutionGraph
{
    public List<ExecutionNode> Nodos { get; set; } = new();

    /// <summary>
    /// Devuelve los nodos agrupados por capas (nivel de profundidad). Los nodos de una
    /// misma capa pueden ejecutarse en paralelo; las capas se procesan en orden secuencial.
    /// </summary>
    public List<List<ExecutionNode>> ObtenerCapas()
    {
        var capas = new List<List<ExecutionNode>>();
        var resueltos = new HashSet<int>();
        var nodosRestantes = Nodos.ToDictionary(n => n.IdNodo);

        while (resueltos.Count < Nodos.Count)
        {
            var capa = Nodos
                .Where(n => !resueltos.Contains(n.IdNodo)
                            && n.DependeDe.All(d => resueltos.Contains(d)))
                .ToList();

            if (capa.Count == 0)
                throw new InvalidOperationException(
                    "Ciclo detectado en el Execution Graph (dependencias circulares). Regla de arquitectura violada.");

            foreach (var n in capa)
                n.Profundidad = n.DependeDe.Any()
                    ? nodosRestantes.Values.Where(x => n.DependeDe.Contains(x.IdNodo)).Max(x => x.Profundidad) + 1
                    : 0;

            capas.Add(capa);
            foreach (var n in capa) resueltos.Add(n.IdNodo);
        }

        return capas;
    }

    public int ProfundidadMaxima() => Nodos.Count == 0 ? 0 : Nodos.Max(n => n.Profundidad) + 1;
}
