using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services.Memoria;
using Asistente.Domain.Entities;
using Asistente.Shared;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Pruebas del Motor de Retención (ETAPA 21, sección "Retention Engine" del
/// plan de pruebas). Verifican el vencimiento, la desactivación, la purga con
/// trazabilidad y la vista previa de pendientes.
/// </summary>
public class MemoryRetentionEngineTests
{
    private static MemoryRecord Memoria(string codigo, int idScope = 2, int? idPropietario = null)
        => new MemoryRecord
        {
            Codigo = codigo,
            Titulo = codigo,
            Contenido = "contenido de " + codigo,
            IdScope = idScope,
            IdPropietario = idPropietario,
            Activo = true,
            VersionActual = 1,
            FechaCreacion = DateTime.UtcNow.AddDays(-400)
        };

    [Fact]
    public async Task ExpiradasAsync_SinPolitica_NoVenceNunca()
    {
        var (manager, repo, _, _, _, retencion) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await repo.AddAsync(Memoria("SIN-POLITICA"), CancellationToken.None);

        var expiradas = await retencion.ExpiradasAsync();

        Assert.Empty(expiradas);
    }

    [Fact]
    public async Task ExpiradasAsync_ConPoliticaPorScope_DetectaLaVencida()
    {
        var (manager, repo, _, _, _, retencion) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        var vieja = await repo.AddAsync(Memoria("VIEJA"), CancellationToken.None);
        await repo.AddAsync(Memoria("NUEVA"), CancellationToken.None);
        repo.Datos[1].FechaCreacion = DateTime.UtcNow;
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdScope = 2,
            DiasRetencion = 365,
            EliminarAlExpirar = false,
            ConservarTrazabilidad = true,
            Activo = true
        });

        var expiradas = await retencion.ExpiradasAsync();

        Assert.Single(expiradas);
        Assert.Equal("VIEJA", expiradas[0].Codigo);
    }

    [Fact]
    public async Task AplicarAsync_SinEliminarAlExpirar_DesactivaYConservaLaMemoria()
    {
        var (manager, repo, _, logs, _, retencion) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await repo.AddAsync(Memoria("CADUCA"), CancellationToken.None);
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdScope = 2,
            DiasRetencion = 30,
            EliminarAlExpirar = false,
            ConservarTrazabilidad = true,
            Activo = true
        });

        var (desactivadas, eliminadas) = await retencion.AplicarAsync(1);

        Assert.Equal(1, desactivadas);
        Assert.Equal(0, eliminadas);
        Assert.False(repo.Datos[0].Activo);
        Assert.Contains(logs.Datos, l => l.Operacion == "Expirar" && l.Codigo == "CADUCA");
    }

    [Fact]
    public async Task AplicarAsync_EliminarAlExpirar_PurgaYConservaElEventoDeTrazabilidad()
    {
        var (manager, repo, _, logs, _, retencion) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await repo.AddAsync(Memoria("PURGABLE"), CancellationToken.None);
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdScope = 2,
            DiasRetencion = 30,
            EliminarAlExpirar = true,
            ConservarTrazabilidad = true,
            Activo = true
        });

        var (desactivadas, eliminadas) = await retencion.AplicarAsync(7);

        Assert.Equal(0, desactivadas);
        Assert.Equal(1, eliminadas);
        Assert.Empty(repo.Datos);
        // Tras purgar la fila solo sobrevive el evento de auditoria con el codigo.
        var evento = Assert.Single(logs.Datos.Where(l => l.Operacion == "Purgar"));
        Assert.Equal("PURGABLE", evento.Codigo);
        Assert.Null(evento.IdMemory);
        Assert.Equal(7, evento.IdUsuario);
        Assert.NotNull(evento.Motivo);
    }

    [Fact]
    public async Task AplicarAsync_MemoriaVigente_NoSeToca()
    {
        var (manager, repo, _, logs, _, retencion) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        var memoria = Memoria("VIGENTE");
        memoria.FechaCreacion = DateTime.UtcNow;
        await repo.AddAsync(memoria, CancellationToken.None);
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdScope = 2,
            DiasRetencion = 365,
            Activo = true
        });

        var (desactivadas, eliminadas) = await retencion.AplicarAsync(1);

        Assert.Equal(0, desactivadas);
        Assert.Equal(0, eliminadas);
        Assert.True(repo.Datos[0].Activo);
        Assert.Empty(logs.Datos);
    }

    [Fact]
    public void VencimientoEfectivo_LaFechaExplicitaMandaSobreLaPolitica()
    {
        var memoria = Memoria("EXPL");
        memoria.FechaExpiracion = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var vence = MemoryRetentionEngine.VencimientoEfectivo(memoria, 30);

        Assert.Equal(memoria.FechaExpiracion, vence);
    }

    [Fact]
    public void VencimientoEfectivo_SinFechaNiPolitica_NoVence()
    {
        var memoria = Memoria("ETERN");

        Assert.Null(MemoryRetentionEngine.VencimientoEfectivo(memoria, null));
        Assert.Null(MemoryRetentionEngine.VencimientoEfectivo(memoria, 0));
    }
}
