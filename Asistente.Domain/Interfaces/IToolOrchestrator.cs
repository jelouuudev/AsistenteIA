using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

/// <summary>
/// Contrato del Motor de Herramientas (Tool Orchestrator).
/// Responsable de registrar, descubrir, resolver, validar permisos y ejecutar herramientas,
/// registrando auditoría de toda ejecución.
/// </summary>
public interface IToolOrchestrator
{
    /// <summary>Descubre las herramientas disponibles (activas) en el sistema.</summary>
    Task<IEnumerable<Herramienta>> DescubrirHerramientasAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene las herramientas autorizadas para un asistente específico.</summary>
    Task<IEnumerable<Herramienta>> ObtenerHerramientasParaAsistenteAsync(int idAsistente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta una herramienta bajo autorización. Valida usuario, rol, permisos,
    /// herramienta autorizada y estado. Registra auditoría en cualquier caso.
    /// </summary>
    Task<ToolExecutionResult> EjecutarAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Indica si el motor de herramientas está habilitado globalmente.</summary>
    Task<bool> MotorHabilitadoAsync(CancellationToken cancellationToken = default);

    /// <summary>Indica si se exige autorización por asistente para ejecutar herramientas.</summary>
    Task<bool> RequiereAutorizacionAsync(CancellationToken cancellationToken = default);
}
