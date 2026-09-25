using Moq;
using Xunit;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Tests.Services;

/// <summary>
/// Pruebas unitarias para AsistenteService (ETAPA 16).
/// </summary>
public class AgenteManagerTests
{
    private readonly Mock<IAsistenteRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly AsistenteService _service;

    public AgenteManagerTests()
    {
        _mockRepo = new Mock<IAsistenteRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _service = new AsistenteService(_mockRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task CrearAsistenteAsync_DatosValidos_CreaCorrectamente()
    {
        var request = new CrearAsistenteRequest
        {
            Codigo = "COMERCIAL-01",
            Nombre = "Comercial",
            Objetivo = "Atender ventas",
            PromptSistema = "Eres un agente comercial.",
            ModeloIA = "deepseek-r1:7b",
            Fuentes = new List<int> { 1 },
            Herramientas = new List<int> { 2 },
            Workflows = new List<int> { 3 },
            Roles = new List<int> { 1 },
            Usuarios = new List<int> { 5 }
        };

        var result = await _service.CrearAsistenteAsync(request);

        Assert.Equal("COMERCIAL-01", result.Codigo);
        Assert.Equal("Atender ventas", result.Objetivo);
        Assert.Equal("Eres un agente comercial.", result.PromptSistema);
        Assert.Equal(EstadoAgente.Activo, result.Estado);
        Assert.Equal(1, result.Version);
        Assert.Equal(1, result.Fuentes.Count);
        Assert.Equal(1, result.Herramientas.Count);
        Assert.Equal(1, result.Workflows.Count);
        Assert.Equal(1, result.Roles.Count);
        Assert.Equal(1, result.Usuarios.Count);
        _mockRepo.Verify(x => x.AddAsync(It.IsAny<Domain.Entities.Asistente>()), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ActivarAsync_Cambia_Estado_A_Activo()
    {
        var agente = new Domain.Entities.Asistente { IdAsistente = 1, Nombre = "X", ModeloIA = "m", Estado = EstadoAgente.Inactivo, Activo = true };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(agente);
        _mockRepo.Setup(x => x.Update(It.IsAny<Domain.Entities.Asistente>())).Verifiable();

        await _service.ActivarAsync(1);

        Assert.Equal(EstadoAgente.Activo, agente.Estado);
        _mockRepo.Verify(x => x.Update(agente), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DesactivarAsync_Cambia_Estado_A_Inactivo()
    {
        var agente = new Domain.Entities.Asistente { IdAsistente = 1, Nombre = "X", ModeloIA = "m", Estado = EstadoAgente.Activo, Activo = true };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(agente);

        await _service.DesactivarAsync(1);

        Assert.Equal(EstadoAgente.Inactivo, agente.Estado);
    }

    [Fact]
    public async Task CrearVersionAsync_Incrementa_Version_Y_Registra_Historial()
    {
        var agente = new Domain.Entities.Asistente
        {
            IdAsistente = 1,
            Nombre = "X",
            ModeloIA = "deepseek-r1:7b",
            Estado = EstadoAgente.Activo,
            Version = 1,
            AsistentesFuentes = new List<AsistenteFuente> { new() { IdFuente = 1, Activo = true } },
            AsistentesHerramientas = new List<AsistenteHerramienta> { new() { IdHerramienta = 2, Activa = true } }
        };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(agente);

        var versionDto = await _service.CrearVersionAsync(1, "admin");

        Assert.Equal(2, agente.Version);
        Assert.Equal(2, versionDto.Version);
        Assert.Single(agente.Versiones);
        Assert.Equal("admin", versionDto.UsuarioCreacion);
    }

    [Fact]
    public async Task DuplicarAsync_Crea_Copia_Con_NuevoCodigo_Y_MismoEstadoActivo()
    {
        var orig = new Domain.Entities.Asistente
        {
            IdAsistente = 1,
            Codigo = "ORIG-01",
            Nombre = "Original",
            ModeloIA = "m",
            Objetivo = "Obj",
            PromptSistema = "Prompt",
            Estado = EstadoAgente.Activo,
            Version = 3,
            AsistentesFuentes = new List<AsistenteFuente> { new() { IdFuente = 1, Activo = true } },
            AsistentesHerramientas = new List<AsistenteHerramienta> { new() { IdHerramienta = 2, Activa = true } },
            AgentesWorkflows = new List<AgenteWorkflow> { new() { IdWorkflow = 3, Activo = true } },
            AgentesRoles = new List<AgenteRol> { new() { IdRol = 1, Activo = true } },
            UsuariosAsistentes = new List<UsuarioAsistente> { new() { IdUsuario = 5, Activo = true } }
        };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(orig);
        _mockRepo.Setup(x => x.AddAsync(It.IsAny<Domain.Entities.Asistente>())).Returns(Task.CompletedTask);

        var copia = await _service.DuplicarAsync(1);

        Assert.NotEqual("ORIG-01", copia.Codigo);
        Assert.Contains("ORIG-01", copia.Codigo);
        Assert.Equal(EstadoAgente.Activo, copia.Estado);
        Assert.Single(copia.Fuentes);
        Assert.Single(copia.Herramientas);
        Assert.Single(copia.Workflows);
        Assert.Single(copia.Roles);
        Assert.Single(copia.Usuarios);
    }

    [Fact]
    public async Task ActualizarAsistenteAsync_Sincroniza_Asignaciones()
    {
        var agente = new Domain.Entities.Asistente
        {
            IdAsistente = 1,
            Codigo = "C-1",
            Nombre = "X",
            ModeloIA = "m",
            AsistentesFuentes = new List<AsistenteFuente> { new() { IdFuente = 99, Activo = true } }
        };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(agente);
        _mockRepo.Setup(x => x.Update(It.IsAny<Domain.Entities.Asistente>())).Verifiable();

        var request = new ActualizarAsistenteRequest
        {
            Codigo = "C-1",
            Nombre = "X2",
            Fuentes = new List<int> { 7 },
            Herramientas = new List<int>(),
            Workflows = new List<int>(),
            Roles = new List<int>(),
            Usuarios = new List<int>()
        };

        await _service.ActualizarAsistenteAsync(1, request);

        Assert.Equal("X2", agente.Nombre);
        Assert.Single(agente.AsistentesFuentes);
        Assert.Equal(7, agente.AsistentesFuentes.First().IdFuente);
        _mockRepo.Verify(x => x.Update(agente), Times.Once);
    }
}
