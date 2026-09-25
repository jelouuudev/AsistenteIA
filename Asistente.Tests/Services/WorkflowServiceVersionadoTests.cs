using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class WorkflowServiceVersionadoTests
{
    private readonly Mock<IWorkflowRepository> _workflowRepo = new();
    private readonly Mock<IWorkflowPasoRepository> _pasoRepo = new();
    private readonly Mock<IWorkflowEjecucionRepository> _ejecRepo = new();
    private readonly Mock<IConfiguracionWorkflowRepository> _configRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IWorkflowEngine> _engine = new();

    private WorkflowService CrearService(int limitePasos = 10)
    {
        _configRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionWorkflow { LimitePasosPorWorkflow = limitePasos });
        return new(_workflowRepo.Object, _pasoRepo.Object, _ejecRepo.Object, _configRepo.Object, _uow.Object, _engine.Object);
    }

    private static Workflow WorkflowBase(int id, int version, string codigo)
    {
        var w = new Workflow
        {
            IdWorkflow = id,
            Nombre = "Reporte de Clientes",
            Codigo = codigo,
            Descripcion = "desc",
            Disparadores = "reporte de clientes",
            Version = version,
            Estado = EstadoWorkflow.Activo,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = 1,
            Pasos = new List<WorkflowPaso>
            {
                new() { IdPaso = 1, IdWorkflow = id, Orden = 1, Nombre = "Consultar", Herramienta = "SqlQueryTool",
                        Parametros = "{\"a\":1}", RequiereConfirmacion = false, ReintentosMaximos = 2,
                        TiempoMaximoMs = 30000, EstrategiaError = EstrategiaError.Cancelar },
                new() { IdPaso = 2, IdWorkflow = id, Orden = 2, Nombre = "Generar", Herramienta = "ReportTool",
                        Parametros = "{\"b\":\"{{resultado}}\"}", RequiereConfirmacion = true, ReintentosMaximos = 1,
                        TiempoMaximoMs = 15000, EstrategiaError = EstrategiaError.Cancelar }
            }
        };
        return w;
    }

    [Fact]
    public async Task CrearAsync_Debe_Rechazar_Cuando_Supera_Limite_Pasos()
    {
        var pasos = Enumerable.Range(1, 11).Select(i => new WorkflowPasoRequest
        {
            Orden = i, Nombre = $"Paso {i}", Herramienta = "SqlQueryTool", Parametros = "{}",
            RequiereConfirmacion = false, ReintentosMaximos = 1, TiempoMaximoMs = 60000, EstrategiaError = "Cancelar"
        }).ToList();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearService(limitePasos: 10).CrearAsync(
            new CrearWorkflowRequest { Nombre = "Grande", Codigo = "BIG", Pasos = pasos }, CancellationToken.None));
        Assert.Contains("límite de 10 pasos", ex.Message);
    }

    [Fact]
    public async Task EliminarAsync_Debe_Bloquear_Cuando_Hay_Ejecuciones()
    {
        var w = WorkflowBase(9, 1, "REP-TEST");
        _workflowRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(w);
        _ejecRepo.Setup(r => r.GetByWorkflowAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowEjecucion> { new() { IdEjecucion = 1, IdWorkflow = 9 } });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearService().EliminarAsync(9));
        Assert.Contains("auditor", ex.Message);
    }

    [Fact]
    public async Task VersionarAsync_Debe_Clonar_Flujo_En_Nuevo_Registro_Con_Version_Mas_Uno()
    {
        var original = WorkflowBase(1, 1, "REP-CLI");
        _workflowRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _workflowRepo.Setup(r => r.GetByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workflow?)null);
        Workflow? clonGuardado = null;
        _workflowRepo.Setup(r => r.AddAsync(It.IsAny<Workflow>(), It.IsAny<CancellationToken>()))
            .Callback<Workflow, CancellationToken>((w, _) => clonGuardado = w)
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CrearService().VersionarAsync(1);

        Assert.NotNull(clonGuardado);
        Assert.NotEqual(original.IdWorkflow, clonGuardado!.IdWorkflow); // nuevo registro
        Assert.Equal(2, clonGuardado.Version);                          // Version+1
        Assert.Equal(EstadoWorkflow.Borrador, clonGuardado.Estado);     // nuevo inicia en Borrador
        Assert.Equal(2, clonGuardado.Pasos.Count);                     // pasos copiados
        Assert.Equal("REP-CLI-v2", clonGuardado.Codigo);               // código único
        // Campos del paso copiados fielmente
        var p1 = clonGuardado.Pasos.First(p => p.Orden == 1);
        Assert.Equal("SqlQueryTool", p1.Herramienta);
        Assert.False(p1.RequiereConfirmacion);
        var p2 = clonGuardado.Pasos.First(p => p.Orden == 2);
        Assert.True(p2.RequiereConfirmacion);
        Assert.Equal("{\"b\":\"{{resultado}}\"}", p2.Parametros);
    }

    [Fact]
    public async Task VersionarAsync_No_Modifica_El_Original()
    {
        var original = WorkflowBase(1, 1, "REP-CLI");
        _workflowRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _workflowRepo.Setup(r => r.GetByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workflow?)null);
        _workflowRepo.Setup(r => r.AddAsync(It.IsAny<Workflow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var versionAntes = original.Version;
        await CrearService().VersionarAsync(1);

        Assert.Equal(versionAntes, original.Version);              // original intacto
        Assert.Equal(EstadoWorkflow.Activo, original.Estado);      // original sigue Activo
        _workflowRepo.Verify(r => r.UpdateAsync(It.IsAny<Workflow>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
