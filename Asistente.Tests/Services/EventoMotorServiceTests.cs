using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Eventos;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class EventoMotorServiceTests
{
    private readonly Mock<IEventoEmpresarialRepository> _eventoRepo = new();
    private readonly Mock<IReglaEventoRepository> _reglaRepo = new();
    private readonly Mock<IEventoProcesadoRepository> _procesadoRepo = new();
    private readonly Mock<IConfiguracionEventoMotorRepository> _configRepo = new();
    private readonly Mock<IWorkflowEngine> _engine = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<IAuditoriaService> _auditoriaMock = new();
    private readonly List<EventoProcesado> _procesados = new();

    private EventoEmpresarial Evento(string codigo = "DOC_PROCESADO") => new()
    {
        IdEvento = 1,
        Codigo = codigo,
        Nombre = "Documento Procesado",
        Categoria = "Documento",
        Activo = true
    };

    private ConfiguracionEventoMotor Config(int reintentos = 2) => new()
    {
        IdConfiguracion = 1,
        ReintentosMaximos = reintentos,
        IntervaloReintentoMs = 0,
        TiempoMaximoEventoMs = 120000,
        EventosSimultaneosMax = 5,
        FrecuenciaProcesadorMs = 2000
    };

    private EventoMotorService CrearMotor()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _configRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Config());
        _procesadoRepo.Setup(r => r.AddAsync(It.IsAny<EventoProcesado>(), It.IsAny<CancellationToken>()))
            .Callback<EventoProcesado, CancellationToken>((e, _) =>
            {
                e.IdEventoProcesado = _procesados.Count + 1;
                _procesados.Add(e);
            })
            .Returns(Task.CompletedTask);
        _procesadoRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => _procesados.FirstOrDefault(x => x.IdEventoProcesado == id));

        _usuarioRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new Usuario { IdUsuario = 1, UsuarioNombre = "test", Activo = true });
        return new EventoMotorService(
            _eventoRepo.Object, _reglaRepo.Object, _procesadoRepo.Object,
            _configRepo.Object, _engine.Object, _uow.Object,
            _usuarioRepo.Object, _auditoriaMock.Object,
            new Mock<ILogger<EventoMotorService>>().Object);
    }

    [Fact]
    public async Task DispararEventoAsync_Encola_ConEstadoPendiente_YDevuelveDto()
    {
        _eventoRepo.Setup(r => r.GetByCodigoAsync("DOC_PROCESADO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Evento());

        var motor = CrearMotor();
        var dto = await motor.DispararEventoAsync("DOC_PROCESADO");

        Assert.Equal("Pendiente", dto.Estado);
        Assert.Equal("DOC_PROCESADO", dto.CodigoEvento);
        Assert.Equal("Documento Procesado", dto.NombreEvento);
        Assert.Single(_procesados);
        Assert.Equal(1, _procesados[0].IdEvento);
    }

    [Fact]
    public async Task DispararYProcesar_PreservaContextoDisparo_AunqueResultadoCambie()
    {
        _eventoRepo.Setup(r => r.GetByCodigoAsync("DOC_PROCESADO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Evento());
        _reglaRepo.Setup(r => r.GetByEventoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReglaEvento> { new() { IdRegla = 10, IdEvento = 1, IdWorkflow = 5, Activa = true } });
        _engine.Setup(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Exitoso = true, Estado = "Exitosa", ResultadoFinal = "ok" });

        var motor = CrearMotor();
        var dto = await motor.DispararEventoAsync("DOC_PROCESADO", "{\"Prioridad\": 5}");
        await motor.ProcesarEventoAsync(dto.IdEventoProcesado);

        var guardado = _procesados.Single();
        Assert.Equal("{\"Prioridad\": 5}", guardado.ContextoDisparo);
        Assert.Equal("{\"Prioridad\": 5}", dto.ContextoDisparo);
        Assert.NotEqual("{\"Prioridad\": 5}", guardado.Resultado); // el resultado se sobrescribe, el contexto no
    }

    [Fact]
    public async Task DispararEventoAsync_EventoInexistente_LanzaExcepcion()
    {
        _eventoRepo.Setup(r => r.GetByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventoEmpresarial?)null);

        var motor = CrearMotor();
        await Assert.ThrowsAsync<InvalidOperationException>(() => motor.DispararEventoAsync("NO_EXISTE"));
    }

    [Fact]
    public async Task DispararEventoAsync_EventoDesactivado_LanzaExcepcion()
    {
        _eventoRepo.Setup(r => r.GetByCodigoAsync("VIEJO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventoEmpresarial { IdEvento = 9, Codigo = "VIEJO", Nombre = "Viejo", Activo = false });

        var motor = CrearMotor();
        await Assert.ThrowsAsync<InvalidOperationException>(() => motor.DispararEventoAsync("VIEJO"));
    }

    [Fact]
    public async Task ProcesarEventoAsync_UsuarioInactivo_MarcaErrorYRegistraIncidente()
    {
        _procesados.Add(new EventoProcesado { IdEventoProcesado = 1, IdEvento = 1, Estado = "Pendiente", IdUsuario = 9 });

        var motor = CrearMotor();
        // Setup específico DESPUÉS del genérico de CrearMotor (Moq: gana el último).
        _usuarioRepo.Setup(r => r.GetByIdAsync(9))
            .ReturnsAsync(new Usuario { IdUsuario = 9, UsuarioNombre = "off", Activo = false });

        await motor.ProcesarEventoAsync(1);

        Assert.Equal("Error", _procesados[0].Estado);
        _engine.Verify(e => e.EjecutarAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
        _auditoriaMock.Verify(a => a.RegistrarActividadAsync(It.IsAny<int>(), "Eventos", "ErrorAutomatico", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ProcesarEventoAsync_ConReglaActiva_EjecutaWorkflowYMarcaCompletado()
    {
        _procesados.Add(new EventoProcesado { IdEventoProcesado = 1, IdEvento = 1, Estado = "Pendiente" });
        _reglaRepo.Setup(r => r.GetByEventoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReglaEvento> { new() { IdRegla = 10, IdEvento = 1, IdWorkflow = 5, Activa = true } });
        _engine.Setup(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Exitoso = true, Estado = "Exitosa", ResultadoFinal = "ok" });

        var motor = CrearMotor();
        await motor.ProcesarEventoAsync(1);

        _engine.Verify(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Once);
        Assert.Equal("Completado", _procesados[0].Estado);
        Assert.Equal(5, _procesados[0].IdWorkflow);
        Assert.Equal(10, _procesados[0].IdRegla);
    }

    [Fact]
    public async Task ProcesarEventoAsync_SinReglasActivas_NoEjecutaYMarcaCompletado()
    {
        _procesados.Add(new EventoProcesado { IdEventoProcesado = 1, IdEvento = 1, Estado = "Pendiente" });
        _reglaRepo.Setup(r => r.GetByEventoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReglaEvento>());

        var motor = CrearMotor();
        await motor.ProcesarEventoAsync(1);

        _engine.Verify(e => e.EjecutarAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Completado", _procesados[0].Estado);
        Assert.Contains("No hay reglas activas", _procesados[0].Resultado ?? "");
    }

    [Fact]
    public async Task ProcesarEventoAsync_CondicionNoCumplida_NoEjecutaWorkflow()
    {
        _procesados.Add(new EventoProcesado
        {
            IdEventoProcesado = 1,
            IdEvento = 1,
            Estado = "Pendiente",
            Resultado = "{\"Categoria\":\"Exito\"}" // contexto: Categoria != Error
        });
        _reglaRepo.Setup(r => r.GetByEventoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReglaEvento>
            {
                new() { IdRegla = 10, IdEvento = 1, IdWorkflow = 5, Activa = true, Condicion = "Categoria == 'Error'" }
            });

        var motor = CrearMotor();
        await motor.ProcesarEventoAsync(1);

        _engine.Verify(e => e.EjecutarAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Completado", _procesados[0].Estado);
    }

    [Fact]
    public async Task ProcesarEventoAsync_CondicionCumplida_EjecutaWorkflow()
    {
        _procesados.Add(new EventoProcesado
        {
            IdEventoProcesado = 1,
            IdEvento = 1,
            Estado = "Pendiente",
            Resultado = "{\"Categoria\":\"Error\"}" // contexto coincide con la condición
        });
        _reglaRepo.Setup(r => r.GetByEventoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReglaEvento>
            {
                new() { IdRegla = 10, IdEvento = 1, IdWorkflow = 5, Activa = true, Condicion = "Categoria == 'Error'" }
            });
        _engine.Setup(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Exitoso = true, Estado = "Exitosa" });

        var motor = CrearMotor();
        await motor.ProcesarEventoAsync(1);

        _engine.Verify(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task ProcesarEventoAsync_EngineFalla_LaPoliticaDeReintentosReintentaHastaExito()
    {
        _procesados.Add(new EventoProcesado { IdEventoProcesado = 1, IdEvento = 1, Estado = "Pendiente" });
        _reglaRepo.Setup(r => r.GetByEventoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReglaEvento> { new() { IdRegla = 10, IdEvento = 1, IdWorkflow = 5, Activa = true } });

        // ReintentosMaximos = 2 -> intentos = 3: falla, falla, exito.
        var results = new Queue<WorkflowExecutionResult>(new[]
        {
            new WorkflowExecutionResult { Exitoso = false, Estado = "Error", ResultadoFinal = "fallo1" },
            new WorkflowExecutionResult { Exitoso = false, Estado = "Error", ResultadoFinal = "fallo2" },
            new WorkflowExecutionResult { Exitoso = true, Estado = "Exitosa", ResultadoFinal = "ok" }
        });
        _engine.Setup(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(() => results.Dequeue());

        var motor = CrearMotor();
        await motor.ProcesarEventoAsync(1);

        _engine.Verify(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Exactly(3));
        Assert.Equal("Completado", _procesados[0].Estado);
    }

    [Fact]
    public async Task ProcesarEventoAsync_EngineFallaSiempre_ReintentosAgotadosMarcaError()
    {
        _procesados.Add(new EventoProcesado { IdEventoProcesado = 1, IdEvento = 1, Estado = "Pendiente" });
        _reglaRepo.Setup(r => r.GetByEventoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReglaEvento> { new() { IdRegla = 10, IdEvento = 1, IdWorkflow = 5, Activa = true } });
        _engine.Setup(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Exitoso = false, Estado = "Error", ResultadoFinal = "siempre falla" });

        var motor = CrearMotor();
        await motor.ProcesarEventoAsync(1);

        // ReintentosMaximos = 2 -> 3 intentos.
        _engine.Verify(e => e.EjecutarAsync(5, It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Exactly(3));
        Assert.Equal("Error", _procesados[0].Estado);
    }
}
