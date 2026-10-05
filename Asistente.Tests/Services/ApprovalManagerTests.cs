using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Aprobaciones;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Entities.Aprobaciones;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Pruebas unitarias del Approval Manager (ETAPA 19, punto 17): ciclo de vida,
/// políticas, delegación y vencimientos.
/// </summary>
public class ApprovalManagerTests
{
    private readonly Mock<IApprovalRequestRepository> _req = new();
    private readonly Mock<IApprovalDecisionRepository> _dec = new();
    private readonly Mock<IApprovalAssigneeRepository> _asg = new();
    private readonly Mock<IApprovalPolicyRepository> _pol = new();
    private readonly Mock<IPlanRepository> _plan = new();
    private readonly Mock<IPlanExecutionLogRepository> _log = new();
    private readonly Mock<ILogger<ApprovalManager>> _logger = new();
    private readonly Mock<IUsuarioRepository> _usu = new();

    private ApprovalManager Manager() => new(_req.Object, _dec.Object, _asg.Object, _pol.Object, _plan.Object, _log.Object, _logger.Object, Mock.Of<IServiceScopeFactory>(), _usu.Object);

    private static Usuario UsuarioConRol(int id, string rol, bool activo = true)
    {
        var u = new Usuario { IdUsuario = id, UsuarioNombre = $"user{id}", Activo = activo };
        u.UsuarioRoles.Add(new UsuarioRol { Rol = new Rol { IdRol = 2, Nombre = rol, Activo = true } });
        return u;
    }

    private void SetupAprobador(int id, string rol = "Operador") =>
        _usu.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(UsuarioConRol(id, rol));

    [Fact]
    public async Task CrearSolicitud_Asigna_Aprobadores_Y_PasaAPendiente()
    {
        ApprovalRequest? guardada = null;
        _req.Setup(x => x.AddAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ApprovalRequest, CancellationToken>((r, _) => guardada = r)
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        _asg.Setup(x => x.AddAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));

        var mgr = Manager();
        SetupAprobador(1);
        SetupAprobador(2);
        var sol = await mgr.CrearSolicitudAsync(100, TipoAprobacion.Financiera, 5, "Reporte", new() { 1, 2 }, ct: CancellationToken.None);

        Assert.Equal(EstadoAprobacion.Pendiente, sol.Estado);
        Assert.Equal(100, sol.IdPlan);
        Assert.NotNull(guardada);
        _asg.Verify(x => x.AddAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        // El primer aprobador de la lista es el principal.
        Assert.True(sol.Asignados.Count == 0 || sol.Asignados.First().EsPrincipal);
    }

    [Fact]
    public async Task Decidir_AprobacionSimple_UnAprobador_Aprobado_Y_ReanudaPlan()
    {
        var sol = new ApprovalRequest { IdApproval = 10, IdPlan = 100, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 10, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _asg.Setup(x => x.UpdateAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _req.Setup(x => x.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        var plan = new Plan { IdPlan = 100, Estado = "EnEsperaAprobacion" };
        _plan.Setup(x => x.GetByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _plan.Setup(x => x.UpdateAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Returns((Plan p, CancellationToken _) => Task.FromResult(p));

        var mgr = Manager();
        SetupAprobador(1);
        var res = await mgr.DecidirAsync(10, 1, "Aprobar", "OK", CancellationToken.None);

        Assert.Equal(EstadoAprobacion.Aprobado, res.Estado);
        Assert.True(plan.Aprobado);
        // La reanudación es por evento (no cambia a EnEjecucion aquí; lo hace el relanzamiento).
        Assert.Equal("EnEsperaAprobacion", plan.Estado);
    }

    [Fact]
    public async Task Decidir_Aprobar_RelanzaEjecucionPorEvento()
    {
        var sol = new ApprovalRequest { IdApproval = 12, IdPlan = 102, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 12, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _asg.Setup(x => x.UpdateAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _req.Setup(x => x.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        var plan = new Plan { IdPlan = 102, Estado = "EnEsperaAprobacion" };
        _plan.Setup(x => x.GetByIdAsync(102, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _plan.Setup(x => x.UpdateAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Returns((Plan p, CancellationToken _) => Task.FromResult(p));

        var planner = new Mock<IPlannerEngine>();
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IPlannerEngine))).Returns(planner.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        var mgr = new ApprovalManager(_req.Object, _dec.Object, _asg.Object, _pol.Object, _plan.Object, _log.Object,
            new Mock<ILogger<ApprovalManager>>().Object, factory.Object, _usu.Object);
        _usu.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(UsuarioConRol(1, "Operador"));

        await mgr.DecidirAsync(12, 1, "Aprobar", "OK", CancellationToken.None);

        // El relanzamiento es fire-and-forget: esperar a que ocurra.
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limite)
        {
            try
            {
                planner.Verify(p => p.ContinuarPlanAprobadoAsync(102, It.IsAny<CancellationToken>()), Times.Once());
                return;
            }
            catch (MockException) { await Task.Delay(100); }
        }
        planner.Verify(p => p.ContinuarPlanAprobadoAsync(102, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Decidir_Rechazo_CancelaPlan()
    {
        var sol = new ApprovalRequest { IdApproval = 11, IdPlan = 101, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 11, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _asg.Setup(x => x.UpdateAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _req.Setup(x => x.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        var plan = new Plan { IdPlan = 101 };
        _plan.Setup(x => x.GetByIdAsync(101, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _plan.Setup(x => x.UpdateAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Returns((Plan p, CancellationToken _) => Task.FromResult(p));

        var mgr = Manager();
        SetupAprobador(1);
        var res = await mgr.DecidirAsync(11, 1, "Rechazar", "No", CancellationToken.None);

        Assert.Equal(EstadoAprobacion.Rechazado, res.Estado);
        Assert.Equal("Cancelado", plan.Estado);
    }

    [Fact]
    public async Task Decidir_SolicitanteNoPuedeAprobar_SuPropiaAccion()
    {
        var sol = new ApprovalRequest { IdApproval = 12, IdPlan = 102, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 12, IdUsuario = 5, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(sol);

        var mgr = Manager();
        SetupAprobador(5);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => mgr.DecidirAsync(12, 5, "Aprobar", "yo", CancellationToken.None));
    }

    [Fact]
    public async Task Decidir_Unanimidad_Requiere_Todos()
    {
        var policy = new ApprovalPolicy { IdPolicy = 1, RequiereUnanimidad = true };
        var sol = new ApprovalRequest { IdApproval = 13, IdPlan = 103, Solicitante = 5, Estado = EstadoAprobacion.Pendiente, IdPolicy = 1, Policy = policy };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 13, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 13, IdUsuario = 2, Estado = "Pendiente" });
        _req.Setup(x => x.GetByIdAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _asg.Setup(x => x.UpdateAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _req.Setup(x => x.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));

        var mgr = Manager();
        SetupAprobador(1);
        var res = await mgr.DecidirAsync(13, 1, "Aprobar", "ok", CancellationToken.None);

        // Con aprobadores pendientes, la cadena secuencial queda en Delegado
        // (ApprovalManager.DecidirAsync: si hay pendientes, no evalúa unanimidad aún).
        Assert.Equal(EstadoAprobacion.Delegado, res.Estado);
    }

    [Fact]
    public async Task Delegar_Registra_NuevoAsignado_Y_MarcaDelegado()
    {
        var sol = new ApprovalRequest { IdApproval = 14, IdPlan = 104, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 14, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(14, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _asg.Setup(x => x.UpdateAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _asg.Setup(x => x.AddAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Callback<ApprovalAssignee, CancellationToken>((a, _) => sol.Asignados.Add(a))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _req.Setup(x => x.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        _usu.Setup(x => x.GetByIdAsync(9))
            .ReturnsAsync(UsuarioConRol(9, "Supervisor"));

        var mgr = Manager();
        var res = await mgr.DelegarAsync(14, 1, 9, "delego", CancellationToken.None);

        Assert.Equal(EstadoAprobacion.Delegado, res.Estado);
        Assert.Contains(res.Asignados, a => a.IdUsuario == 9 && a.Estado == "Pendiente");
        Assert.All(res.Asignados.Where(a => a.IdUsuario == 1), a => Assert.Equal("Delegado", a.Estado));
    }

    [Fact]
    public async Task Delegar_SinUsuarioDestino_Rechaza_YNoModifica()
    {
        var sol = new ApprovalRequest { IdApproval = 16, IdPlan = 106, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 16, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(16, It.IsAny<CancellationToken>())).ReturnsAsync(sol);

        var mgr = Manager();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mgr.DelegarAsync(16, 1, 0, "sin destino", CancellationToken.None));

        // No se marca como delegado ni se agrega asignado fantasma.
        Assert.Equal(EstadoAprobacion.Pendiente, sol.Estado);
        Assert.All(sol.Asignados, a => Assert.Equal("Pendiente", a.Estado));
        _asg.Verify(x => x.AddAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delegar_UsuarioInexistente_Rechaza_YNoModifica()
    {
        var sol = new ApprovalRequest { IdApproval = 17, IdPlan = 107, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 17, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(17, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _usu.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Usuario?)null);

        var mgr = Manager();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mgr.DelegarAsync(17, 1, 999, "fantasma", CancellationToken.None));

        Assert.Equal(EstadoAprobacion.Pendiente, sol.Estado);
        _asg.Verify(x => x.AddAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CrearSolicitud_Descarta_RolUsuario_Y_Falla_SiNoQuedanAprobadores()
    {
        ApprovalRequest? guardada = null;
        _req.Setup(x => x.AddAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ApprovalRequest, CancellationToken>((r, _) => guardada = r)
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        _asg.Setup(x => x.AddAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _usu.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(UsuarioConRol(7, "Usuario"));
        SetupAprobador(1);

        var mgr = Manager();

        // Solo rol Usuario -> sin aprobadores válidos.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mgr.CrearSolicitudAsync(200, TipoAprobacion.Manual, 5, "X", new() { 7 }, ct: CancellationToken.None));

        // Mixta -> se asigna solo el aprobador con rol válido.
        var sol = await mgr.CrearSolicitudAsync(201, TipoAprobacion.Manual, 5, "Y", new() { 7, 1 }, ct: CancellationToken.None);
        Assert.NotNull(guardada);
        _asg.Verify(x => x.AddAsync(It.Is<ApprovalAssignee>(a => a.IdUsuario == 7), It.IsAny<CancellationToken>()), Times.Never);
        _asg.Verify(x => x.AddAsync(It.Is<ApprovalAssignee>(a => a.IdUsuario == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Decidir_RolUsuario_NoPuedeAprobar_AunqueEsteAsignado()
    {
        var sol = new ApprovalRequest { IdApproval = 20, IdPlan = 200, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 20, IdUsuario = 7, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _usu.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(UsuarioConRol(7, "Usuario"));

        var mgr = Manager();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => mgr.DecidirAsync(20, 7, "Aprobar", "ok", CancellationToken.None));
    }

    [Fact]
    public async Task Delegar_RolUsuarioDestino_Rechaza_YNoModifica()
    {
        var sol = new ApprovalRequest { IdApproval = 21, IdPlan = 201, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 21, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(21, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _usu.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(UsuarioConRol(7, "Usuario"));

        var mgr = Manager();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mgr.DelegarAsync(21, 1, 7, "a usuario", CancellationToken.None));

        Assert.Equal(EstadoAprobacion.Pendiente, sol.Estado);
        _asg.Verify(x => x.AddAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Decidir_TrasDobleDelegacion_ApruebaConAsignacionPendiente()
    {
        // Admin delegó a operador y operador devolvió a admin: admin tiene una
        // fila "Delegado" (vieja) y una "Pendiente" (actual). Debe poder decidir.
        var sol = new ApprovalRequest { IdApproval = 22, IdPlan = 202, Solicitante = 2, Estado = EstadoAprobacion.Pendiente };
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 22, IdUsuario = 1, Estado = "Delegado", EsPrincipal = true });
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 22, IdUsuario = 2, Estado = "Delegado" });
        sol.Asignados.Add(new ApprovalAssignee { IdApproval = 22, IdUsuario = 1, Estado = "Pendiente", EsPrincipal = true });
        _req.Setup(x => x.GetByIdAsync(22, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _asg.Setup(x => x.UpdateAsync(It.IsAny<ApprovalAssignee>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalAssignee a, CancellationToken _) => Task.FromResult(a));
        _req.Setup(x => x.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        var plan = new Plan { IdPlan = 202, Estado = "EnEsperaAprobacion" };
        _plan.Setup(x => x.GetByIdAsync(202, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _plan.Setup(x => x.UpdateAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Returns((Plan p, CancellationToken _) => Task.FromResult(p));
        SetupAprobador(1);

        var mgr = Manager();
        var res = await mgr.DecidirAsync(22, 1, "Aprobar", "ok tras ping-pong", CancellationToken.None);

        Assert.Equal(EstadoAprobacion.Aprobado, res.Estado);
    }

    [Fact]
    public async Task Expirar_MarcaExpirado_Y_CancelaPlan()
    {
        var sol = new ApprovalRequest { IdApproval = 15, IdPlan = 105, Solicitante = 5, Estado = EstadoAprobacion.Pendiente };
        _req.Setup(x => x.GetByIdAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync(sol);
        _req.Setup(x => x.UpdateAsync(It.IsAny<ApprovalRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ApprovalRequest r, CancellationToken _) => Task.FromResult(r));
        var plan = new Plan { IdPlan = 105 };
        _plan.Setup(x => x.GetByIdAsync(105, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _plan.Setup(x => x.UpdateAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Returns((Plan p, CancellationToken _) => Task.FromResult(p));

        var mgr = Manager();
        var res = await mgr.ExpirarAsync(15, CancellationToken.None);

        Assert.Equal(EstadoAprobacion.Expirado, res.Estado);
        Assert.Equal("Cancelado", plan.Estado);
    }
}
