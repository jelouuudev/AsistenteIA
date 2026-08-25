using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Plan Builder (ETAPA 18, Actividad 2). Construye un plan estructurado a partir de una
/// solicitud en lenguaje natural. Es determinista (basado en intención/keywords) para ser
/// fiable en CPU, y OPCIONALMENTE enriquece el razonamiento con el LLM local (Ollama)
/// sin bloquear si este no responde.
/// </summary>
public class PlanBuilder
{
    private readonly IAsistenteRepository _asistenteRepo;
    private readonly IOllamaService _ollama;
    private readonly ILogger<PlanBuilder> _logger;

    public PlanBuilder(IAsistenteRepository asistenteRepo, IOllamaService ollama, ILogger<PlanBuilder> logger)
    {
        _asistenteRepo = asistenteRepo;
        _ollama = ollama;
        _logger = logger;
    }

    /// <summary>Genera el plan (pasos + dependencias) a partir del objetivo.</summary>
    public async Task<Plan> ConstruirAsync(string objetivo, int idUsuario, CancellationToken ct)
    {
        var plan = new Plan
        {
            IdUsuario = idUsuario,
            Objetivo = objetivo,
            Estado = "Borrador",
            FechaCreacion = DateTime.UtcNow,
            Version = 1
        };

        var agentes = (await _asistenteRepo.GetAllAsync()).Where(a => a.Activo).ToList();
        var principal = agentes.FirstOrDefault(a => a.Codigo == "COMERCIAL-01")
                        ?? agentes.FirstOrDefault(a => a.Activo)
                        ?? throw new InvalidOperationException("No hay agentes disponibles para planificar.");

        var lowers = objetivo.ToLowerInvariant();
        var pasos = new List<PlanStep>();
        var orden = 0;

        // Detecta intenciones y genera pasos con tipo/agente/herramienta apropiados.
        bool quiereSql = Contiene(lowers, "venta", "dato", "cliente", "sql", "consulta", "kpi", "indicador", "moros", "riesgo");
        bool quiereRag = Contiene(lowers, "manual", "procedimiento", "política", "normativa", "document", "vacacion", "compar");
        bool quiereReporte = Contiene(lowers, "resumen", "reporte", "informe", "ejecutivo", "pdf", "compar");
        bool quiereRiesgo = Contiene(lowers, "riesgo", "clasif", "moros");
        bool requiereAprobacion = Contiene(lowers, "eliminar", "borrar", "enviar", "pagar", "desactivar", "elimina");

        // Paso 1: Agente principal analiza/coordina.
        pasos.Add(Paso(ref orden, "Agent", $"Analizar la solicitud y coordinar respuesta", principal.IdAsistente,
            principal.Nombre, "El agente principal comprende el objetivo y orquesta los colaboradores."));

        // Paso 2: Consulta SQL (datos del negocio).
        if (quiereSql)
        {
            pasos.Add(Paso(ref orden, "Tool", "Consultar datos del negocio (SQL Server)", principal.IdAsistente,
                principal.Nombre, "Ejecuta SqlQueryTool para obtener los datos requeridos.", codigoHerramienta: "SqlQueryTool"));
        }

        // Paso 3: RAG (documentación / procedimiento).
        if (quiereRag)
        {
            var soporte = agentes.FirstOrDefault(a => a.Codigo == "SOPORTE-01") ?? agentes.FirstOrDefault(a => a.Activo);
            pasos.Add(Paso(ref orden, "RAG", "Consultar documentación mediante RAG", soporte?.IdAsistente ?? principal.IdAsistente,
                soporte?.Nombre ?? principal.Nombre, "Recupera el procedimiento/documento desde la base de conocimiento.", codigoHerramienta: "DocumentSearchTool"));
        }

        // Paso 4: Análisis / cálculo de indicadores.
        if (quiereSql || quiereRiesgo)
        {
            pasos.Add(Paso(ref orden, "Agent", "Analizar resultados y calcular indicadores", principal.IdAsistente,
                principal.Nombre, "Procesa los datos obtenidos para derivar métricas y conclusiones."));
        }

        // Paso 5: Reporte / resumen ejecutivo.
        if (quiereReporte)
        {
            var reportes = agentes.FirstOrDefault(a => a.Codigo == "REPORTES-01") ?? agentes.FirstOrDefault(a => a.Activo);
            pasos.Add(Paso(ref orden, "Tool", "Generar resumen ejecutivo / informe", reportes?.IdAsistente ?? principal.IdAsistente,
                reportes?.Nombre ?? principal.Nombre, "Consolida los hallazgos en un informe.", codigoHerramienta: "ReportTool"));
        }

        // Paso 6: Riesgos (si aplica).
        if (quiereRiesgo)
        {
            pasos.Add(Paso(ref orden, "Agent", "Clasificar por nivel de riesgo", principal.IdAsistente,
                principal.Nombre, "Evalúa y clasifica los elementos según su riesgo detectado."));
        }

        // Paso 7: Aprobación (si la acción es sensible) o entrega.
        if (requiereAprobacion)
        {
            plan.RequiereAprobacion = true;
            pasos.Add(Paso(ref orden, "Approval", "Aprobar acción sensible antes de ejecutar", principal.IdAsistente,
                principal.Nombre, "Requiere confirmación humana (aprobación opcional, Regla de negocio 12)."));
        }

        // Paso final: entrega del resultado.
        pasos.Add(Paso(ref orden, "Agent", "Entregar resultado final al usuario", principal.IdAsistente,
            principal.Nombre, "Consolida y presenta la respuesta final."));

        // Dependencias: cadena secuencial (DAG) — cada paso depende del anterior.
        // Garantiza un grafo acíclico (Regla 7) y es determinista.
        for (int i = 1; i < pasos.Count; i++)
        {
            plan.Dependencias.Add(new PlanDependency
            {
                IdPlan = 0,
                StepOrigen = pasos[i - 1].Orden,
                StepDestino = pasos[i].Orden
            });
        }

        plan.Pasos = pasos;
        plan.Razonamiento = await GenerarRazonamientoAsync(objetivo, pasos, ct);
        return plan;
    }

    private PlanStep Paso(ref int orden, string tipo, string nombre, int? idAsistente, string? nombreAgente, string descripcion, string? codigoHerramienta = null)
    {
        return new PlanStep
        {
            Orden = orden++,
            Tipo = tipo,
            Nombre = nombre,
            IdAsistente = idAsistente,
            Descripcion = descripcion,
            CodigoHerramienta = codigoHerramienta,
            Estado = "Pendiente"
        };
    }

    private static bool Contiene(string texto, params string[] palabras)
        => palabras.Any(p => texto.Contains(p));

    /// <summary>Razonamiento operativo opcional vía LLM. No bloquea si Ollama no responde.</summary>
    private async Task<string> GenerarRazonamientoAsync(string objetivo, List<PlanStep> pasos, CancellationToken ct)
    {
        try
        {
            var historial = new List<Mensaje>
            {
                new Mensaje { Rol = RolMensaje.User, Contenido = $"Objetivo: {objetivo}. Pasos: {string.Join("; ", pasos.Select(p => p.Nombre))}." }
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            return await _ollama.SendMessageAsync(historial, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: razonamiento LLM omitido (Ollama no disponible en CPU).");
            return $"Plan generado por el Plan Builder basado en intención. {pasos.Count} paso(s) estructurados como DAG.";
        }
    }
}
