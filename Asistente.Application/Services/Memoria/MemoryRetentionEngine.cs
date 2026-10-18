using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Memoria;

/// <summary>
/// Motor de retención (ETAPA 21): aplica políticas por ámbito con vencimiento
/// explícito o por días. Desactivar conserva todo (trazabilidad total); eliminar
/// purga el registro y su vector, dejando el evento de purga con el código
/// (trazabilidad cuando la política lo exige). También expone vista previa.
/// </summary>
public class MemoryRetentionEngine
{
    private readonly IMemoryRepository _repo;
    private readonly IMemoryAccessLogRepository _logs;
    private readonly SemanticMemorySearch _busqueda;
    private readonly ILogger<MemoryRetentionEngine> _logger;

    // Sin IMemoryVersionRepository: al purgar una memoria se borra su fila y su
    // vector, pero sus versiones quedan como historial (la trazabilidad manda) y
    // esta clase nunca las leyó. Se retiró la dependencia porque inyectarla sin
    // usarla oculta que el borrado de versiones es responsabilidad de otro capa.
    public MemoryRetentionEngine(
        IMemoryRepository repo,
        IMemoryAccessLogRepository logs,
        SemanticMemorySearch busqueda,
        ILogger<MemoryRetentionEngine> logger)
    {
        _repo = repo;
        _logs = logs;
        _busqueda = busqueda;
        _logger = logger;
    }

    private static MemoryRetentionPolicy? politicaPara(
        MemoryRecord memoria, List<MemoryRetentionPolicy> politicas)
        => politicas.FirstOrDefault(p => p.IdScope == memoria.IdScope)
            ?? politicas.FirstOrDefault(p => p.IdScope == null);

    public async Task<List<MemoryRecord>> ExpiradasAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var politicas = (await _repo.GetPoliticasAsync(ct))
            .Where(p => p.Activo).ToList();
        var salida = new List<MemoryRecord>();
        foreach (var memoria in await _repo.GetActivasAsync(ct))
        {
            var dias = politicaPara(memoria, politicas)?.DiasRetencion;
            var vence = VencimientoEfectivo(memoria, dias);
            if (vence.HasValue && vence.Value <= ahora)
                salida.Add(memoria);
        }
        return salida;
    }

    /// <summary>Aplica retención: devuelve (desactivadas, eliminadas).</summary>
    public async Task<(int Desactivadas, int Eliminadas)> AplicarAsync(
        int? idUsuario, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var politicas = (await _repo.GetPoliticasAsync(ct))
            .Where(p => p.Activo).ToList();
        int desactivadas = 0, eliminadas = 0;
        foreach (var memoria in await _repo.GetActivasAsync(ct))
        {
            var politica = politicaPara(memoria, politicas);
            var vence = VencimientoEfectivo(memoria, politica?.DiasRetencion);
            if (!vence.HasValue || vence.Value > ahora) continue;

            if (politica != null && politica.EliminarAlExpirar)
            {
                await _busqueda.DesindexarAsync(memoria, ct);
                await _repo.DeleteAsync(memoria, ct);
                eliminadas++;
                await _logs.AddAsync(new MemoryAccessLog
                {
                    IdMemory = null,
                    Codigo = memoria.Codigo,
                    IdUsuario = idUsuario,
                    Operacion = "Purgar",
                    Resultado = "Exitoso",
                    Motivo = politica.ConservarTrazabilidad
                        ? $"Purgada por retención ({politica.DiasRetencion} días); se conserva este evento."
                        : null,
                    Fecha = ahora
                }, ct);
            }
            else
            {
                memoria.Activo = false;
                memoria.FechaActualizacion = ahora;
                await _repo.UpdateAsync(memoria, ct);
                desactivadas++;
                await _logs.AddAsync(new MemoryAccessLog
                {
                    IdMemory = memoria.IdMemory,
                    Codigo = memoria.Codigo,
                    IdUsuario = idUsuario,
                    Operacion = "Expirar",
                    Resultado = "Exitoso",
                    Fecha = ahora
                }, ct);
            }
        }
        _logger.LogInformation("Memoria: retención aplicada: {Des} desactivadas, {Eli} eliminadas.",
            desactivadas, eliminadas);
        return (desactivadas, eliminadas);
    }

    /// <summary>
    /// Vencimiento efectivo: fecha explícita si hay, si no creación + días de
    /// la política aplicable, si no null (vive para siempre).
    /// </summary>
    public static DateTime? VencimientoEfectivo(MemoryRecord memoria, int? diasRetencion)
    {
        if (memoria.FechaExpiracion.HasValue) return memoria.FechaExpiracion.Value;
        if (diasRetencion.HasValue && diasRetencion.Value > 0)
            return memoria.FechaCreacion.AddDays(diasRetencion.Value);
        return null;
    }
}
