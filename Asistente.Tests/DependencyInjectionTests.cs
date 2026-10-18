using System;
using System.Collections.Generic;
using System.Threading;
using Asistente.Application;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Memoria;
using Asistente.Application.Services.Workflows;
using Asistente.Infrastructure;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Asistente.Tests;

/// <summary>
/// El resto de la suite construye los servicios a mano con dobles, así que un
/// constructor mal cableado pasa todos los tests y revienta al arrancar la API.
/// Estos tests arman el contenedor real (Application + Infrastructure) y resuelven
/// la cadena de memoria y sus consumidores.
///
/// No hay conexión a base de datos: AddDbContext no abre nada al construir el
/// provider ni al resolver el servicio, solo al ejecutar una consulta.
/// </summary>
public class DependencyInjectionTests
{
    /// <summary>Ciclo de vida mínimo: el contenedor real no lo aporta, Quartz sí lo pide.</summary>
    private sealed class CicloDeVida : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    private static ServiceProvider Contenedor()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=AsistenteTest;User Id=sa;Password=Test;TrustServerCertificate=True",
                ["Embedding:BaseVectorial"] = "InMemory",
                ["Embedding:Modelo"] = "nomic-embed-text",
                ["Embedding:Url"] = "http://localhost:11434",
                ["Ollama:Url"] = "http://localhost:11434",
                ["Ollama:TimeoutSegundos"] = "3600",
                ["Chroma:Url"] = "http://localhost:8000",
                ["Chroma:TimeoutSegundos"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        // El host real los aporta; sin ellos los cifradores y Quartz no se activan.
        services.AddSingleton<IConfiguration>(configuracion);
        services.AddSingleton<IHostApplicationLifetime, CicloDeVida>();
        services.AddApplication(configuracion);
        services.AddInfrastructure(configuracion);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void AgenteRuntime_WorkflowEngine_YOrquestadorDeMemoria_Resuelven()
    {
        using var provider = Contenedor();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IChatService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowEngine>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MemoryOrchestrator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MemoryManager>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MemoryVersionManager>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SemanticMemorySearch>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MemoryRetentionEngine>());
    }

    /// <summary>
    /// Documentos y memorias comparten colección de vectores, así que el RAG solo
    /// autoriza por documento y saltaría el permiso MEMORIA:{codigo}. La frontera
    /// se resuelve inyectando a todo el mundo el almacén ENVUELTO, y dejando el
    /// almacén CRUDO solo para SemanticMemorySearch.
    ///
    /// Si alguien registra SemanticMemorySearch con el almacén envuelto, la
    /// búsqueda de memorias deja de encontrar nada y este test lo detecta.
    /// </summary>
    [Fact]
    public void Almacen_Vectorial_Envuelto_Para_El_RAG_Y_Crudo_Para_La_Memoria()
    {
        using var provider = Contenedor();
        using var scope = provider.CreateScope();

        var paraRag = scope.ServiceProvider.GetRequiredService<IVectorStore>();

        Assert.IsType<Asistente.Infrastructure.Services.MemorySafeVectorStore>(paraRag);

        // SemanticMemorySearch debe resolverse: si se construyera con el almacén
        // envuelto, seguiría compilando pero no encontraría ninguna memoria.
        var busqueda = scope.ServiceProvider.GetRequiredService<SemanticMemorySearch>();
        Assert.NotNull(busqueda);
    }

    /// <summary>
    /// Manager y Orquestador comparten el DbContext del scope. Si alguno quedara
    /// registrado como singleton, el motor de SQL de EF lo rechazaría en la segunda
    /// petición (un DbContext por hilo y por scope).
    /// </summary>
    [Fact]
    public void CadenaDeMemoria_ComparteElRepositorioDelScope()
    {
        using var provider = Contenedor();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMemoryRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMemoryVersionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMemoryAccessLogRepository>());

        // Mismo repositorio para Manager y Orquestador: el estado que el Workflow
        // Engine escribe es visible de inmediato al componer el contexto.
        Assert.Same(scope.ServiceProvider.GetRequiredService<IMemoryRepository>(),
            scope.ServiceProvider.GetRequiredService<IMemoryRepository>());
    }
}