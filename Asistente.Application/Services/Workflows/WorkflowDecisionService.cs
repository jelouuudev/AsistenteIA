using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Workflows;

/// <summary>
/// Resultado de la identificación de un workflow a partir del mensaje del usuario.
/// </summary>
public class WorkflowDecision
{
    public bool RequiereWorkflow { get; set; }
    public int? IdWorkflow { get; set; }
    public string? CodigoWorkflow { get; set; }
    public string? NombreWorkflow { get; set; }
}

/// <summary>
/// Identifica el flujo de trabajo correspondiente a una solicitud del usuario (Actividad 9).
/// Utiliza coincidencia determinista por frases disparadoras (Workflow.Disparadores)
/// para evitar depender del modelo de IA y garantizar reproducibilidad en la demostración.
/// </summary>
public class WorkflowDecisionService : IWorkflowDecisionService
{
    private readonly IWorkflowRepository _workflowRepository;

    public WorkflowDecisionService(IWorkflowRepository workflowRepository)
    {
        _workflowRepository = workflowRepository;
    }

    public async Task<WorkflowDecision> DecidirAsync(string mensaje, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
            return new WorkflowDecision { RequiereWorkflow = false };

        var activos = (await _workflowRepository.GetActivosAsync(ct)).ToList();
        if (activos.Count == 0)
            return new WorkflowDecision { RequiereWorkflow = false };

        var texto = QuitarAcentos(mensaje.ToLowerInvariant());

        // 1) Coincidencia por disparadores explícitos
        foreach (var wf in activos)
        {
            if (string.IsNullOrWhiteSpace(wf.Disparadores)) continue;
            var disparadores = wf.Disparadores.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var d in disparadores)
            {
                if (texto.Contains(QuitarAcentos(d.ToLowerInvariant())))
                    return new WorkflowDecision { RequiereWorkflow = true, IdWorkflow = wf.IdWorkflow, CodigoWorkflow = wf.Codigo, NombreWorkflow = wf.Nombre };
            }
        }

        // 2) Coincidencia por nombre del flujo mencionado en el mensaje
        foreach (var wf in activos)
        {
            if (texto.Contains(QuitarAcentos(wf.Nombre.ToLowerInvariant())))
                return new WorkflowDecision { RequiereWorkflow = true, IdWorkflow = wf.IdWorkflow, CodigoWorkflow = wf.Codigo, NombreWorkflow = wf.Nombre };
        }

        return new WorkflowDecision { RequiereWorkflow = false };
    }

    private static string QuitarAcentos(string texto)
    {
        return new string(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
    }
}
