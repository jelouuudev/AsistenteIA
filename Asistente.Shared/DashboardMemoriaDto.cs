using System;
using System.Collections.Generic;

namespace Asistente.Shared;

/// <summary>
/// Panel de memoria empresarial (ETAPA 21, secciones 7 y 17).
///
/// Agrupa lo que antes solo se podía mirar una consulta a la vez: las métricas del
/// sistema, el rastro real de quién leyó qué, y lo que la retención tiene pendiente
/// de apagar. El métrico solo no basta para operar: sin la auditoría al lado no se
/// ve qué agente está reutilizando la memoria ni si la retención está corriendo.
/// </summary>
public class DashboardMemoriaDto
{
    /// <summary>Contadores y desgloses por ámbito, agente, usuario y tipo.</summary>
    public MemoryMetricasDto Metricas { get; set; } = new();

    /// <summary>Últimos accesos registrados, con agente, tipo y duración.</summary>
    public List<MemoryAccessLogDto> UltimasAuditorias { get; set; } = new();

    /// <summary>Códigos de memorias caducadas que la política tocaría en el próximo barrido.</summary>
    public List<string> RetencionPendientes { get; set; } = new();
}
