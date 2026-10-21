using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services.Memoria;
using Asistente.Shared;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Pruebas funcionales de la ETAPA 21 (sección 15 del plan de pruebas):
/// crear memoria, buscarla, versionarla, aplicar retención y auditar las
/// operaciones. Recorren el flujo completo con los dobles en memoria, en el
/// mismo orden en que un usuario lo haria desde la interfaz.
/// </summary>
public class MemoriaFuncionalTests
{
    private static CrearMemoryRequest Peticion(string codigo = "FORM-REPORTE-MENSUAL") => new()
    {
        Codigo = codigo,
        Titulo = "Formato de reporte mensual",
        Contenido = "1. Resumen ejecutivo. 2. Indicadores. 3. Incidencias. 4. Cierre.",
        Ambito = "Organizacion",
        Categoria = "FormatoReporte",
        RequierePermiso = false
    };

    [Fact]
    public async Task Funcional_CrearMemoria_NaceEnVersion1YQuedaAuditada()
    {
        var (manager, repo, versiones, logs, _, _) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());

        var creada = await manager.CrearAsync(Peticion(), 1);

        Assert.Equal("FORM-REPORTE-MENSUAL", creada.Codigo);
        Assert.Equal(1, creada.VersionActual);
        Assert.True(creada.Activo);
        Assert.Single(versiones.Datos.Where(v => v.IdMemory == repo.Datos[0].IdMemory && v.Numero == 1));
        Assert.Contains(logs.Datos, l => l.Operacion == "Crear" && l.Resultado == "Exitoso");
    }

    [Fact]
    public async Task Funcional_BuscarMemoria_DevuelveLaMemoriaQueContieneElTexto()
    {
        var (manager, _, _, _, _, _) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await manager.CrearAsync(Peticion(), 1);

        var hits = await manager.BuscarAsync("formato de reporte mensual", 1);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Codigo == "FORM-REPORTE-MENSUAL");
    }

    [Fact]
    public async Task Funcional_Versionar_CadaActualizacionAgregaUnaVersionYNoBorraLaAnterior()
    {
        var (manager, repo, versiones, logs, _, _) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await manager.CrearAsync(Peticion(), 1);

        await manager.ActualizarAsync("FORM-REPORTE-MENSUAL",
            new ActualizarMemoryRequest { Contenido = "Formato v2 con nuevas secciones.", Motivo = "Ajuste de sección" }, 1);
        await manager.ActualizarAsync("FORM-REPORTE-MENSUAL",
            new ActualizarMemoryRequest { Contenido = "Formato v3 definitivo.", Motivo = "Cierre" }, 1);

        var vs = await manager.VersionesAsync("FORM-REPORTE-MENSUAL", 1);
        Assert.Equal(3, vs.Count);
        Assert.Equal(new[] { 1, 2, 3 }, vs.Select(v => v.Numero).ToArray());
        // La v1 conserva su contenido: el versionado no reescribe historia.
        Assert.Contains("Resumen ejecutivo", vs.First(v => v.Numero == 1).Contenido);
        Assert.Contains("Formato v3 definitivo", vs.First(v => v.Numero == 3).Contenido);
        Assert.Contains(logs.Datos, l => l.Operacion == "Actualizar");
    }

    [Fact]
    public async Task Funcional_Revertir_CreaUnaVersionNuevaConElContenidoElegido()
    {
        var (manager, _, _, _, _, _) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await manager.CrearAsync(Peticion(), 1);
        await manager.ActualizarAsync("FORM-REPORTE-MENSUAL",
            new ActualizarMemoryRequest { Contenido = "Formato v2 cambiado." }, 1);

        var revertida = await manager.RevertirAsync("FORM-REPORTE-MENSUAL", 1, "Volver al formato original", 1);

        Assert.Equal(3, revertida.VersionActual);
        Assert.Contains("Resumen ejecutivo", revertida.Contenido);
        var vs = await manager.VersionesAsync("FORM-REPORTE-MENSUAL", 1);
        Assert.Equal(3, vs.Count);
    }

    [Fact]
    public async Task Funcional_AplicarRetencion_DesactivaLaMemoriaVencidaYLaAudita()
    {
        var (manager, repo, _, logs, _, retencion) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await manager.CrearAsync(Peticion(), 1);
        // La política vence a los 30 días y la memoria nació hace 400.
        repo.Datos[0].FechaCreacion = DateTime.UtcNow.AddDays(-400);
        repo.Politicas.Add(new Domain.Entities.MemoryRetentionPolicy
        {
            IdScope = 2,
            DiasRetencion = 30,
            EliminarAlExpirar = false,
            ConservarTrazabilidad = true,
            Activo = true
        });

        var pendientes = await retencion.ExpiradasAsync();
        Assert.Single(pendientes);

        var (desactivadas, eliminadas) = await retencion.AplicarAsync(1);

        Assert.Equal(1, desactivadas);
        Assert.Equal(0, eliminadas);
        Assert.False(repo.Datos[0].Activo);
        Assert.Contains(logs.Datos, l => l.Operacion == "Expirar" && l.Codigo == "FORM-REPORTE-MENSUAL");
    }

    [Fact]
    public async Task Funcional_Auditar_RegistraCadaOperacionConSuResultado()
    {
        var (manager, _, _, logs, _, _) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await manager.CrearAsync(Peticion(), 1);
        await manager.ObtenerAsync("FORM-REPORTE-MENSUAL", 1);
        await manager.BuscarAsync("reporte", 1);

        var auditoria = await manager.AuditoriaAsync("FORM-REPORTE-MENSUAL", 1, 100);

        Assert.Contains(auditoria, a => a.Operacion == "Crear" && a.Resultado == "Exitoso");
        Assert.Contains(auditoria, a => a.Operacion == "Leer" && a.Resultado == "Exitoso");
        Assert.All(auditoria, a => Assert.Equal("FORM-REPORTE-MENSUAL", a.Codigo));
        // La busqueda es transversal: se audita con su propio codigo (no cuelga de
        // una memoria), y de ahi salen las metricas de memorias consultadas.
        Assert.Contains(logs.Datos, l => l.Operacion == "Buscar" && l.Resultado == "Exitoso");
    }

    [Fact]
    public async Task Funcional_FlujoCompleto_CrearBuscarVersionarRetenerAuditar()
    {
        var (manager, repo, _, logs, _, retencion) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());

        // 1. Crear
        await manager.CrearAsync(Peticion(), 1);
        // 2. Buscar
        var hits = await manager.BuscarAsync("reporte mensual", 1);
        Assert.NotEmpty(hits);
        // 3. Versionar
        await manager.ActualizarAsync("FORM-REPORTE-MENSUAL",
            new ActualizarMemoryRequest { Contenido = "Formato v2." }, 1);
        await manager.RevertirAsync("FORM-REPORTE-MENSUAL", 1, "Revertir", 1);
        // 4. Retención
        repo.Datos[0].FechaCreacion = DateTime.UtcNow.AddDays(-400);
        repo.Politicas.Add(new Domain.Entities.MemoryRetentionPolicy
        {
            IdScope = 2,
            DiasRetencion = 30,
            Activo = true
        });
        var (desactivadas, _) = await retencion.AplicarAsync(1);
        Assert.Equal(1, desactivadas);
        // 5. Auditoría
        var auditoria = await manager.AuditoriaAsync("FORM-REPORTE-MENSUAL", 1, 100);
        Assert.Contains(auditoria, a => a.Operacion == "Crear");
        Assert.Contains(auditoria, a => a.Operacion == "Actualizar");
        Assert.Contains(logs.Datos, l => l.Operacion == "Expirar");
    }

    [Fact]
    public async Task Funcional_MemoriaRestringida_SinPermiso_NoSeLeeYQuedaDenegadoEnAuditoria()
    {
        var (manager, _, _, logs, _, _) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "SECRETA",
            Titulo = "Credencial restringida",
            Contenido = "token sk-demo-0000",
            Ambito = "Organizacion",
            Categoria = "Definicion",
            RequierePermiso = true
        }, 1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => manager.ObtenerAsync("SECRETA", 2));
        Assert.Contains(logs.Datos, l => l.Operacion == "Leer" && l.Resultado == "Denegado");
    }
}
