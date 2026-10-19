using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Memoria;

/// <summary>
/// Dashboard de memoria empresarial (ETAPA 21, secciones 7 y 17).
///
/// Compone tres lecturas que antes había que pedir una a una: las métricas del
/// sistema, el rastro real de accesos y lo que la retención tiene pendiente.
/// Las tres viven en los mismos componentes que ya existen (MemoryManager y
/// MemoryRetentionEngine), así que el panel no introduce un cuarto camino para
/// leer memoria ni una copia de sus contadores.
/// </summary>
public class DashboardMemoriaService : IDashboardMemoriaService
{
    private readonly MemoryManager _manager;
    private readonly MemoryRetentionEngine _retencion;

    public DashboardMemoriaService(MemoryManager manager, MemoryRetentionEngine retencion)
    {
        _manager = manager;
        _retencion = retencion;
    }

    public async Task<DashboardMemoriaDto> ObtenerAsync(CancellationToken ct = default)
    {
        return new DashboardMemoriaDto
        {
            Metricas = await _manager.MetricasAsync(ct),
            // Sin permiso por MEMORIA:{codigo}: quien llega aquí ya superó
            // AUDITORIA_CONSULTAR en el endpoint, y exigir el permiso de cada fila
            // dejaría la tabla en blanco salvo para el dueño de todo.
            UltimasAuditorias = await _manager.AuditoriaRecienteAsync(50, ct),
            RetencionPendientes = (await _retencion.ExpiradasAsync(ct)).ConvertAll(m => m.Codigo)
        };
    }
}
