using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Infrastructure.Data;
using Asistente.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Regresión del Plan #5031: los pasos Tool quedaban en Pendiente/NULL aunque el log
/// decía "PasoToolEjecutado con éxito", y el plan se atascaba en IniciandoEjecucion.
/// Causa: PlanStepRepository.UpdateAsync usaba DbSet.Update (graph-attach) y el
/// background actualizaba el paso Agent con la entidad stale de plan.Pasos
/// (navegación Plan poblada) → SaveChanges revertía las filas Tool y el Plan.
/// </summary>
public class PlanStepRepositoryTests : IDisposable
{
    private readonly DbConnection _conn;

    public PlanStepRepositoryTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
    }

    public void Dispose()
    {
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private AsistenteDbContext NuevoContexto()
    {
        var ctx = new AsistenteDbContext(new DbContextOptionsBuilder<AsistenteDbContext>()
            .UseSqlite(_conn)
            .Options);
        // El modelo usa nvarchar(max) (SQL Server); en SQLite se crean solo las
        // tablas mínimas con DDL compatible.
        ctx.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS [Plan] (
                IdPlan INTEGER PRIMARY KEY AUTOINCREMENT,
                IdUsuario INTEGER NOT NULL,
                Objetivo TEXT NOT NULL,
                Estado TEXT NOT NULL DEFAULT 'Borrador',
                FechaCreacion TEXT NOT NULL,
                FechaInicio TEXT NULL,
                FechaFin TEXT NULL,
                TiempoTotalMs INTEGER NULL,
                Version INTEGER NOT NULL DEFAULT 1,
                Razonamiento TEXT NULL,
                RequiereAprobacion INTEGER NOT NULL DEFAULT 0,
                Aprobado INTEGER NOT NULL DEFAULT 0,
                IdExecution TEXT NULL);
            CREATE TABLE IF NOT EXISTS [PlanStep] (
                IdStep INTEGER PRIMARY KEY AUTOINCREMENT,
                IdPlan INTEGER NOT NULL,
                Orden INTEGER NOT NULL,
                Tipo TEXT NOT NULL,
                Nombre TEXT NOT NULL,
                Descripcion TEXT NULL,
                Estado TEXT NOT NULL DEFAULT 'Pendiente',
                Resultado TEXT NULL,
                IdAsistente INTEGER NULL,
                CodigoHerramienta TEXT NULL,
                IdWorkflow INTEGER NULL,
                Intentos INTEGER NOT NULL DEFAULT 0,
                Metadatos TEXT NULL,
                Entrada TEXT NULL);
            CREATE TABLE IF NOT EXISTS [PlanDependency] (
                IdDependency INTEGER PRIMARY KEY AUTOINCREMENT,
                IdPlan INTEGER NOT NULL,
                StepOrigen INTEGER NOT NULL,
                StepDestino INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS [PlanExecutionLog] (
                IdLog INTEGER PRIMARY KEY AUTOINCREMENT,
                IdPlan INTEGER NOT NULL,
                IdStep INTEGER NULL,
                Evento TEXT NOT NULL,
                Detalle TEXT NULL,
                Fecha TEXT NOT NULL);
            """);
        return ctx;
    }

    private static async Task<int> SembrarPlanAsync(AsistenteDbContext ctx, string estadoPlan)
    {
        var plan = new Plan
        {
            IdUsuario = 1,
            Objetivo = "Consulta las ventas de Ropa y las ventas de hogar en paralelo",
            Estado = estadoPlan,
            FechaCreacion = DateTime.UtcNow
        };
        plan.Pasos.Add(new PlanStep { Orden = 0, Tipo = "Coordination", Nombre = "Analizar", Estado = "Pendiente" });
        plan.Pasos.Add(new PlanStep { Orden = 1, Tipo = "Tool", Nombre = "Consultar [1/2]", Estado = "Pendiente", CodigoHerramienta = "SqlQueryTool" });
        plan.Pasos.Add(new PlanStep { Orden = 2, Tipo = "Tool", Nombre = "Consultar [2/2]", Estado = "Pendiente", CodigoHerramienta = "SqlQueryTool" });
        plan.Pasos.Add(new PlanStep { Orden = 3, Tipo = "Agent", Nombre = "Entregar", Estado = "Pendiente" });
        await ctx.Planes.AddAsync(plan);
        await ctx.SaveChangesAsync();
        return plan.IdPlan;
    }

    [Fact]
    public async Task UpdatePasoAgentStale_NoReviertePasosToolCompletados()
    {
        // Arrange: plan como lo deja la fase Tool (0 y 3 pendientes, 1-2 completados).
        int idPlan;
        using (var ctx = NuevoContexto())
        {
            idPlan = await SembrarPlanAsync(ctx, "EnEjecucion");
            var repo = new PlanStepRepository(ctx);
            foreach (var orden in new[] { 1, 2 })
            {
                var s = (await repo.GetByPlanAsync(idPlan)).First(p => p.Orden == orden);
                s.Estado = "Completado";
                s.Resultado = $"datos reales del paso {orden} " + new string('x', 100);
                await repo.UpdateAsync(s);
            }
            // Coordinación también completada (como en producción).
            var c0 = (await repo.GetByPlanAsync(idPlan)).First(p => p.Orden == 0);
            c0.Estado = "Completado";
            c0.Resultado = "plan de acción coordinado";
            await repo.UpdateAsync(c0);
        }

        // Act: el background carga el plan STALE (pasos Tool aún en Pendiente/NULL,
        // plan en IniciandoEjecucion) y actualiza el paso Agent con esa entidad,
        // tal como hacía EjecutarNodoUnaVezAsync antes del fix.
        using (var ctxStale = NuevoContexto())
        {
            // Simular staleness: cargar y revertir en memoria a valores viejos.
            var planRepo = new PlanRepository(ctxStale);
            var planStale = await planRepo.GetByIdAsync(idPlan);
            Assert.NotNull(planStale);
            planStale!.Estado = "IniciandoEjecucion";
            foreach (var p in planStale.Pasos.Where(p => p.Orden == 1 || p.Orden == 2))
            {
                p.Estado = "Pendiente";
                p.Resultado = null;
            }
            // Detach para simular otro DbContext ya dispuesto.
            foreach (var e in ctxStale.ChangeTracker.Entries().ToList())
                e.State = EntityState.Detached;

            using var ctxBg = NuevoContexto();
            var stepRepoBg = new PlanStepRepository(ctxBg);
            var pasoAgentStale = planStale.Pasos.First(p => p.Orden == 3);
            pasoAgentStale.Estado = "Completado";
            pasoAgentStale.Resultado = "comparación final";
            await stepRepoBg.UpdateAsync(pasoAgentStale);
        }

        // Assert: los Tool siguen Completados con su resultado y el plan intacto.
        using (var ctx = NuevoContexto())
        {
            var pasos = await ctx.PlanSteps.Where(s => s.IdPlan == idPlan).OrderBy(s => s.Orden).ToListAsync();
            Assert.Equal("Completado", pasos[0].Estado);
            Assert.Equal("Completado", pasos[1].Estado);
            Assert.Equal("Completado", pasos[2].Estado);
            Assert.Contains("datos reales del paso 1", pasos[1].Resultado);
            Assert.Contains("datos reales del paso 2", pasos[2].Resultado);
            Assert.Equal("Completado", pasos[3].Estado);
            Assert.Equal("comparación final", pasos[3].Resultado);
            var plan = await ctx.Planes.FindAsync(idPlan);
            Assert.Equal("EnEjecucion", plan!.Estado);
        }
    }

    [Fact]
    public async Task UpdatePasoYaRastreado_SoloTocaEsaFila()
    {
        using var ctx = NuevoContexto();
        var idPlan = await SembrarPlanAsync(ctx, "EnEjecucion");
        var repo = new PlanStepRepository(ctx);
        var paso = (await repo.GetByPlanAsync(idPlan)).First(p => p.Orden == 1);
        paso.Estado = "Completado";
        paso.Resultado = "ok";
        await repo.UpdateAsync(paso, CancellationToken.None);

        var resto = (await repo.GetByPlanAsync(idPlan)).Where(p => p.Orden != 1).ToList();
        Assert.All(resto, p => Assert.Equal("Pendiente", p.Estado));
        Assert.All(resto, p => Assert.Null(p.Resultado));
    }

    /// <summary>
    /// Regresión del síntoma "los pasos desaparecen cuando se completa el plan":
    /// tras UpdateEstadoAsync (ExecuteUpdate) la siguiente lectura en el MISMO
    /// DbContext devolvía la entidad cacheada (Borrador + todos los pasos
    /// Pendiente) y el endpoint /ejecutar respondía ese snapshot vacío.
    /// GetByIdAsync debe releer siempre de la BD.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_TrasUpdateEstadoAsync_DevuelveEstadoYPasosReales()
    {
        using var ctx = NuevoContexto();
        var idPlan = await SembrarPlanAsync(ctx, "Borrador");
        var planRepo = new PlanRepository(ctx);
        var stepRepo = new PlanStepRepository(ctx);

        // El endpoint lee el plan (cacheándolo en el contexto) y luego escala el estado.
        var antes = await planRepo.GetByIdAsync(idPlan);
        Assert.NotNull(antes);
        Assert.Equal("Borrador", antes!.Estado);
        Assert.All(antes.Pasos, p => Assert.Equal("Pendiente", p.Estado));

        // Un paso completa su trabajo y el plan pasa a EnEjecucion.
        var paso = (await stepRepo.GetByPlanAsync(idPlan)).First(p => p.Orden == 1);
        paso.Estado = "Completado";
        paso.Resultado = "datos reales del paso 1";
        await stepRepo.UpdateAsync(paso, CancellationToken.None);
        await planRepo.UpdateEstadoAsync(idPlan, "EnEjecucion", null, CancellationToken.None);

        // La lectura siguiente (mismo contexto) debe ver la BD, no el snapshot viejo.
        var despues = await planRepo.GetByIdAsync(idPlan);
        Assert.NotNull(despues);
        Assert.Equal("EnEjecucion", despues!.Estado);
        var paso1 = despues.Pasos.Single(p => p.Orden == 1);
        Assert.Equal("Completado", paso1.Estado);
        Assert.Equal("datos reales del paso 1", paso1.Resultado);
        Assert.Equal(4, despues.Pasos.Count);
    }

    /// <summary>
    /// Regresión de la duplicación: con GetByIdAsync en AsNoTracking, EF no lleva
    /// snapshot de Plan.Pasos y al fixupear los PlanStep ya rastreados los agrega de
    /// nuevo a la lista -> cada paso aparece dos veces (el coordinador listaba
    /// "Paso 0, Paso 0, Paso 1, Paso 1...") y el grafo reventaba con
    /// "An item with the same key has already been added. Key: 0".
    /// Tras leer el plan y consult sus pasos (como hace LanzarEjecucionGrafo), la
    /// colección debe seguir sin duplicados.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_NoDuplicaPasosAlConsultarLosPasosEnElMismoContexto()
    {
        using var ctx = NuevoContexto();
        var idPlan = await SembrarPlanAsync(ctx, "EnEjecucion");
        var planRepo = new PlanRepository(ctx);
        var stepRepo = new PlanStepRepository(ctx);

        // Secuencia real de producción (PlannerController.Ejecutar + PlannerEngine):
        //   1) el endpoint lee el plan           -> GetByIdAsync
        //   2) escala el estado                  -> UpdateEstadoAsync (ExecuteUpdate)
        //   3) el PlannerEngine revalida         -> UpdateAsync (adjunta el GRAFO)
        //   4) lista los pasos para ejecutarlos  -> GetByPlanAsync
        //   5) el coordinador redacta el plan    -> GenerarTextoCoordinacion(plan.Pasos)
        var plan = await planRepo.GetByIdAsync(idPlan);
        Assert.NotNull(plan);
        Assert.Equal(4, plan!.Pasos.Count);

        await planRepo.UpdateEstadoAsync(idPlan, "IniciandoEjecucion", null, CancellationToken.None);

        plan.Estado = "Validado";
        await planRepo.UpdateAsync(plan, CancellationToken.None);

        var pasos = await stepRepo.GetByPlanAsync(idPlan);
        Assert.Equal(4, pasos.Count);

        // Y la colección del plan sigue sin duplicados (origen del texto del coordinador).
        Assert.Equal(4, plan.Pasos.Count);
        Assert.Equal(4, plan.Pasos.Select(p => p.Orden).Distinct().Count());

        // Tampoco al releer el plan completo otra vez.
        var otraLectura = await planRepo.GetByIdAsync(idPlan);
        Assert.Equal(4, otraLectura!.Pasos.Count);
        Assert.Equal(4, otraLectura.Pasos.Select(p => p.Orden).Distinct().Count());
    }
}
