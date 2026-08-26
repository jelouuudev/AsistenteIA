using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class QueryEmpresarialServiceTests
{
    private readonly Mock<IConexionBaseDatosRepository> _mockConexionRepository;
    private readonly Mock<ITablaAutorizadaRepository> _mockTablaRepository;
    private readonly Mock<IVistaAutorizadaRepository> _mockVistaRepository;
    private readonly Mock<IConsultaPlantillaRepository> _mockPlantillaRepository;
    private readonly Mock<IConfiguracionMotorConsultasRepository> _mockConfigRepository;
    private readonly Mock<IConsultaEjecutadaRepository> _mockConsultaRepository;
    private readonly Mock<IConexionCifrador> _mockCifrador;
    private readonly Mock<ISqlQueryExecutor> _mockExecutor;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly QueryEmpresarialService _service;

    public QueryEmpresarialServiceTests()
    {
        _mockConexionRepository = new Mock<IConexionBaseDatosRepository>();
        _mockTablaRepository = new Mock<ITablaAutorizadaRepository>();
        _mockVistaRepository = new Mock<IVistaAutorizadaRepository>();
        _mockPlantillaRepository = new Mock<IConsultaPlantillaRepository>();
        _mockConfigRepository = new Mock<IConfiguracionMotorConsultasRepository>();
        _mockConsultaRepository = new Mock<IConsultaEjecutadaRepository>();
        _mockCifrador = new Mock<IConexionCifrador>();
        _mockExecutor = new Mock<ISqlQueryExecutor>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockCifrador.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("cadena-de-conexion");
        _mockConsultaRepository.Setup(r => r.AddAsync(It.IsAny<ConsultaEjecutada>())).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var config = new ConfiguracionMotorConsultas
        {
            IdConfiguracion = 1,
            Activo = true,
            TiempoMaximoEjecucionSegundos = 15,
            MaximoRegistros = 100,
            MaxConsultasSimultaneas = 5
        };
        _mockConfigRepository.Setup(r => r.GetActivaAsync()).ReturnsAsync(config);

        _service = new QueryEmpresarialService(
            _mockConexionRepository.Object,
            _mockTablaRepository.Object,
            _mockVistaRepository.Object,
            _mockPlantillaRepository.Object,
            _mockConfigRepository.Object,
            _mockConsultaRepository.Object,
            _mockCifrador.Object,
            _mockExecutor.Object,
            _mockUnitOfWork.Object,
            new Mock<ILogger<QueryEmpresarialService>>().Object);
    }

    private void ConfigurarConexionActiva()
    {
        var conexion = new ConexionBaseDatos
        {
            IdConexion = 1, Nombre = "Restaurante Demo", Activa = true, CadenaConexionCifrada = "x"
        };
        _mockConexionRepository.Setup(r => r.GetActivasAsync()).ReturnsAsync(new List<ConexionBaseDatos> { conexion });
        _mockConexionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(conexion);
    }

    private void ConfigurarTablaClientes()
    {
        _mockTablaRepository.Setup(r => r.GetActivasByConexionIdAsync(1)).ReturnsAsync(new List<TablaAutorizada>
        {
            new() { IdTabla = 1, IdConexion = 1, NombreTabla = "Clientes", Esquema = "dbo", Activa = true }
        });
    }

    private void ConfigurarResultadoEjecutor(int filas)
    {
        var datos = new List<Dictionary<string, object?>>();
        for (int i = 0; i < filas; i++)
        {
            datos.Add(new Dictionary<string, object?>
            {
                ["IdCliente"] = i + 1,
                ["Nombres"] = $"Cliente {i + 1}"
            });
        }
        _mockExecutor.Setup(e => e.ExecuteReadOnlyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(datos);
    }

    [Fact]
    public async Task Pregunta_No_De_Datos_Devuelve_Tipo_Documental()
    {
        var resultado = await _service.ProcesarPreguntaAsync("Cuál es el procedimiento para configurar un asistente?", 1);

        Assert.True(resultado.Exitoso);
        Assert.Equal("documental", resultado.Tipo);
        Assert.Equal("rag", resultado.Fuente);
        _mockConexionRepository.Verify(r => r.GetActivasAsync(), Times.Never);
    }

    [Fact]
    public async Task Motor_Deshabilitado_Devuelve_Tipo_Deshabilitado()
    {
        _mockConfigRepository.Setup(r => r.GetActivaAsync()).ReturnsAsync((ConfiguracionMotorConsultas?)null);

        var resultado = await _service.ProcesarPreguntaAsync("Cuántos clientes hay?", 1);

        Assert.True(resultado.Exitoso);
        Assert.Equal("deshabilitado", resultado.Tipo);
    }

    [Fact]
    public async Task Sin_Conexiones_Activas_Devuelve_Tipo_SinConexion()
    {
        _mockConexionRepository.Setup(r => r.GetActivasAsync()).ReturnsAsync(new List<ConexionBaseDatos>());

        var resultado = await _service.ProcesarPreguntaAsync("Cuántos clientes hay?", 1);

        Assert.Equal("sin-conexion", resultado.Tipo);
        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public async Task Pregunta_De_Conteo_Genera_Sql_Count_Y_Responde_Total()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();
        _mockExecutor.Setup(e => e.ExecuteReadOnlyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, object?>> { new() { ["Total"] = 5L } });

        var resultado = await _service.ProcesarPreguntaAsync("¿Cuántos clientes hay en la base de datos?", 1);

        Assert.True(resultado.Exitoso);
        Assert.Equal("sql", resultado.Tipo);
        Assert.Equal("Restaurante Demo", resultado.Fuente);
        Assert.Contains("COUNT(*)", resultado.ConsultaSql);
        Assert.Equal("Total: 5", resultado.Respuesta);
    }

    [Fact]
    public async Task Pregunta_De_Listado_Ejecuta_Select_Y_Devuelve_Registros()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();
        ConfigurarResultadoEjecutor(3);

        var resultado = await _service.ProcesarPreguntaAsync("Muéstrame la lista de clientes del restaurante", 1);

        Assert.True(resultado.Exitoso);
        Assert.Equal("sql", resultado.Tipo);
        Assert.Equal(3, resultado.CantidadRegistros);
        Assert.Equal(3, resultado.Datos?.Count);
    }

    [Fact]
    public async Task Aplica_Limite_Maximo_De_Registros_Configurado()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();
        _mockConfigRepository.Setup(r => r.GetActivaAsync()).ReturnsAsync(new ConfiguracionMotorConsultas
        {
            IdConfiguracion = 1,
            Activo = true,
            MaximoRegistros = 2
        });
        ConfigurarResultadoEjecutor(2);

        await _service.ProcesarPreguntaAsync("Muéstrame la lista de clientes", 1);

        _mockExecutor.Verify(e => e.ExecuteReadOnlyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.Is<int>(m => m == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consulta_Manual_Con_Tabla_No_Autorizada_Se_Bloquea()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();

        var respuesta = await _service.EjecutarConsultaAsync(new Asistente.Shared.EjecutarConsultaRequest
        {
            IdConexion = 1,
            ConsultaSql = "SELECT * FROM dbo.Productos;",
            Pregunta = "Productos"
        }, 1);

        Assert.False(respuesta.Exitoso);
        Assert.Equal("Bloqueada", respuesta.Estado);
        Assert.Contains("productos", respuesta.Error);
    }

    [Fact]
    public async Task Consulta_Manual_Con_Operacion_No_Select_Se_Bloquea()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();

        var respuesta = await _service.EjecutarConsultaAsync(new Asistente.Shared.EjecutarConsultaRequest
        {
            IdConexion = 1,
            ConsultaSql = "DELETE FROM dbo.Clientes;",
            Pregunta = "borrar"
        }, 1);

        Assert.False(respuesta.Exitoso);
        Assert.Equal("Bloqueada", respuesta.Estado);
        Assert.Contains("SELECT", respuesta.Error);
    }

    [Fact]
    public async Task Consulta_Manual_Valida_Tabla_Calificada_Con_Esquema()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();
        ConfigurarResultadoEjecutor(1);

        var respuesta = await _service.EjecutarConsultaAsync(new Asistente.Shared.EjecutarConsultaRequest
        {
            IdConexion = 1,
            ConsultaSql = "SELECT * FROM [dbo].[Clientes];",
            Pregunta = "lista"
        }, 1);

        Assert.True(respuesta.Exitoso);
        Assert.Equal("Completada", respuesta.Estado);
    }

    [Fact]
    public async Task Pregunta_De_Datos_Sin_Tabla_Coincidente_Devuelve_SinPlantilla()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();

        var resultado = await _service.ProcesarPreguntaAsync("Cuántos productos hay registrados?", 1);

        Assert.Equal("sin-plantilla", resultado.Tipo);
        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public async Task Usa_Plantilla_Cuando_Existe_Coincidencia()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();
        _mockPlantillaRepository.Setup(r => r.GetActivasByConexionIdAsync(1)).ReturnsAsync(new List<ConsultaPlantilla>
        {
            new()
            {
                IdPlantilla = 1,
                Nombre = "Total de clientes",
                IdConexion = 1,
                ConsultaSql = "SELECT COUNT(*) AS Total FROM [dbo].[Clientes];",
                Activa = true
            }
        });
        _mockExecutor.Setup(e => e.ExecuteReadOnlyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, object?>> { new() { ["Total"] = 5L } });

        var resultado = await _service.ProcesarPreguntaAsync("Cuál es el total de clientes?", 1);

        Assert.True(resultado.Exitoso);
        Assert.Equal("sql", resultado.Tipo);
        Assert.Equal("SELECT COUNT(*) AS Total FROM [dbo].[Clientes];", resultado.ConsultaSql);
        Assert.Equal("Total: 5", resultado.Respuesta);
    }

    [Fact]
    public async Task Consulta_Exitosa_Registra_Auditoria()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();
        ConfigurarResultadoEjecutor(2);

        await _service.ProcesarPreguntaAsync("Muéstrame la lista de clientes", 1);

        _mockConsultaRepository.Verify(r => r.AddAsync(It.Is<ConsultaEjecutada>(c =>
            c.Estado == "Completada" && c.CantidadRegistros == 2)), Times.Once);
    }

    [Fact]
    public async Task Consulta_Bloqueada_Registra_Auditoria_Bloqueada()
    {
        ConfigurarConexionActiva();
        ConfigurarTablaClientes();

        await _service.EjecutarConsultaAsync(new Asistente.Shared.EjecutarConsultaRequest
        {
            IdConexion = 1,
            ConsultaSql = "SELECT * FROM dbo.Productos;",
            Pregunta = "Productos"
        }, 1);

        _mockConsultaRepository.Verify(r => r.AddAsync(It.Is<ConsultaEjecutada>(c =>
            c.Estado == "Bloqueada")), Times.Once);
    }
}
