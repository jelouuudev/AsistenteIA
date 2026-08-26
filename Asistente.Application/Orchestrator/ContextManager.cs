using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;

namespace Asistente.Application.Orchestrator;

/// <summary>
/// Implementación de IContextManager (RF Actividad 4).
/// Construye, para cada agente, únicamente el contexto autorizado según las reglas
/// de colaboración (Regla 4: el contexto se comparte solo si hay autorización).
/// </summary>
public class ContextManager : IContextManager
{
    private readonly IAgentCollaborationRuleRepository _reglasRepo;

    public ContextManager(IAgentCollaborationRuleRepository reglasRepo)
        => _reglasRepo = reglasRepo;

    public async Task<AgentContext> BuildContextForAgentAsync(
        int idAgente, SharedContext contextoGlobal, CancellationToken cancellationToken = default)
    {
        var ctx = new AgentContext
        {
            IdAgente = idAgente,
            PreguntaAsignada = contextoGlobal.PreguntaOriginal
        };

        foreach (var previo in contextoGlobal.ResultadosPrevios)
        {
            // Solo comparte el resultado de un agente si está permitido por regla explícita.
            // Regla 4: contexto compartido únicamente cuando existe autorización.
            var permitido = previo.IdAgente == idAgente
                || await _reglasRepo.EstaPermitidoAsync(previo.IdAgente, idAgente, cancellationToken);

            if (permitido)
                ctx.ContextoAutorizado.Add(previo);
        }

        return ctx;
    }

    public bool PuedeCompartir(int idAgenteOrigen, int idAgenteDestino)
        => idAgenteOrigen == idAgenteDestino;
}
