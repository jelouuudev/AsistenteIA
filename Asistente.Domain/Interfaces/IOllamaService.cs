using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IOllamaService
{
    Task<string> SendMessageAsync(IEnumerable<Mensaje> historial, CancellationToken cancellationToken = default);
    Task<string> SendMessageAsync(IEnumerable<Mensaje> historial, string? modelOverride = null, string? systemPrompt = null, double? temperature = null, int? maxTokens = null, CancellationToken cancellationToken = default);
    /// <summary>Verificación rápida de conectividad con Ollama (usado como pre-flight por el
    /// Orchestrator para fallar de inmediato cuando el LLM no está disponible, en vez de
    /// colgar todos los nodos del grafo). Timeout corto (~3s).</summary>
    Task<bool> IsDisponibleAsync(CancellationToken cancellationToken = default);
}
