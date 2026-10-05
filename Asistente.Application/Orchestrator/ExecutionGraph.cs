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

    /// <summary>True si el nodo representa un paso de aprobación humana (ETAPA 19).
    /// Estos nodos NO los ejecuta el Orchestrator: los gestiona el ApprovalManager a nivel
    /// de Planner (Human-in-the-Loop). El Orchestrator los marca Omitido.</summary>
    public bool EsAprobacion { get; set; }

    public string? Resultado { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente|EnEjecucion|Completado|Error|Omitido
    public string? Error { get; set; }
    public long TiempoMs { get; set; }

    /// <summary>
    /// Ámbito de recuperación del nodo ("documental" | "datos" | null), decidido por la
    /// capacidad asignada. Antes se deducía parseando el texto de Accion
    /// (`Accion.Contains("(RAG)")`), lo que rompía en cuanto el rol cambiaba de nombre.
    /// Ahora viaja como dato desde el Agent Candidate.
    /// </summary>
    public string? Alcance { get; set; }
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
        // Índice por IdNodo tolerante a duplicados: el bucle de capas avanza por
        // resueltos (HashSet) mientras Nodos.Count los cuenta, así que un IdNodo
        // repetido dejaría el while sin salida. Se conserva el primer nodo.
        var nodosRestantes = new Dictionary<int, ExecutionNode>();
        foreach (var n in Nodos)
        {
            if (!nodosRestantes.ContainsKey(n.IdNodo))
                nodosRestantes[n.IdNodo] = n;
        }

        // Se itera sobre el índice deduplicado: con Nodos duplicados por IdNodo,
        // resueltos.Count nunca alcanzaría Nodos.Count (bucle infinito).
        while (resueltos.Count < nodosRestantes.Count)
        {
            var capa = nodosRestantes.Values
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

    /// <summary>
    /// Huella determinista del grafo: nodos, dependencias y capas. Es la prueba de que
    /// el grafo que se muestra/valida es el mismo que se ejecuta: si el auditor compara
    /// la huella de la simulación con la del log de ejecución y coinciden, ambos grafos
    /// son estructuralmente idénticos. No incluye tiempos ni identidad de objetos, así
    /// que dos construcciones del mismo plan producen siempre la misma huella.
    /// </summary>
    public string CalcularHuella()
    {
        var nodos = Nodos
            .OrderBy(n => n.IdNodo)
            .Select(n => $"{n.IdNodo}|{n.Accion}|{n.DependeDe.OrderBy(d => d).Aggregate(string.Empty, (a, d) => a + "," + d)}")
            .Aggregate(string.Empty, (a, s) => a + ";" + s);

        var capas = ObtenerCapas()
            .Select((capa, i) => $"c{i}=[{string.Join(",", capa.OrderBy(n => n.IdNodo).Select(n => n.IdNodo))}]")
            .Aggregate(string.Empty, (a, s) => a + s);

        var bytes = System.Text.Encoding.UTF8.GetBytes(nodos + capas);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..16];
    }
}
