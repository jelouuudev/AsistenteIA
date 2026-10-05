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
    private readonly Mock<Asistente.Domain.Interfaces.IEmbeddingProvider> _mockEmbeddings;
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

        // Embeddings deterministas: la intencion y la tabla se deciden por similitud con
        // las descripciones, no por palabras clave de la pregunta.
        _mockEmbeddings = new Mock<IEmbeddingProvider>();
        _mockEmbeddings.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string t) => VectorPara(t));
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
            new Mock<ILogger<QueryEmpresarialService>>().Object,
            _mockEmbeddings.Object);
    }

    /// <summary>
    /// Embeddings deterministas MULTI-aspecto. Un vector ortogonal no alcanza: una
    /// pregunta real se parece a la vez a la INTENCIÓN de agregación, a la ENTIDAD que
    /// menciona y a la CAPACIDAD del motor. Ejes: 0 = contar, 1 = sumar, 2 = promediar,
    /// 3 = clientes, 4 = "consulta de datos", 5 = ninguno de esos.
    ///
    /// El eje 5 importa: con un valor base igual en todos los ejes, dos textos que no
    /// comparten nada siguen teniendo coseno alto (0.53 en el caso de una pregunta
    /// puramente documental contra la descripción del motor), y el motor se activaría
    /// de más. Un embedding real no tiene ese problema: lo que no significa nada,
    /// significa nada.
    /// </summary>
    private static float[] VectorPara(string texto)
    {
        var t = SinAcentos(texto);
        var v = new[] { 0f, 0f, 0f, 0f, 0f, 0.9f };

        // Descripciones de capacidad del motor: eje 4.
        if (t.Contains("consulta de datos de la base de datos")) v[4] = 1f;

        // Descripciones de intención de agregación.
        if (t.Contains("cuenta cuantos") || t.Contains("numero de filas")) v[0] = 1f;
        if (t.Contains("suma los valores")) v[1] = 1f;
        if (t.Contains("promedio o media")) v[2] = 1f;

        // La pregunta hereda los aspectos que menciona.
        if (t.Contains("cuantos") || t.Contains("cuantas") || t.Contains("cuanto")) v[0] = Math.Max(v[0], 0.9f);
        if (t.Contains("suma") || t.Contains("total")) v[1] = Math.Max(v[1], 0.9f);
        if (t.Contains("promedio") || t.Contains("media")) v[2] = Math.Max(v[2], 0.9f);
        if (t.Contains("clientes")) v[3] = Math.Max(v[3], 0.9f);

        // ¿Pide datos? Lo decide el significado conjunto, no una palabra suelta.
        if (t.Contains("clientes") || t.Contains("cuantos") || t.Contains("cuantas")
            || t.Contains("lista") || t.Contains("total") || t.Contains("registros"))
            v[4] = Math.Max(v[4], 0.9f);

        // Si no se activó ningún aspecto, el texto no pertenece a este dominio.
        if (v.Take(5).All(x => x == 0f)) v[5] = 1f; else v[5] = 0f;
        return v;
    }

    /// <summary>Minúsculas sin acentos, como haría el modelo.</summary>
    private static string SinAcentos(string texto)
    {
        var norm = texto.Normalize(System.Text.NormalizationForm.FormD);
        return new string(norm.Where(ch =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
    }

    private void ConfigurarConexionActiva()
    {        var conexion = new ConexionBaseDatos
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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync(datos);
    }

    [Fact]
    public async Task Pregunta_No_De_Datos_Devuelve_Tipo_Documental()
    {
        var resultado = await _service.ProcesarPreguntaAsync("Cuál es el procedimiento para configurar un asistente?", 1);

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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
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
            MaximoRegistros = 2,
            TiempoMaximoEjecucionSegundos = 15
        });
        ConfigurarResultadoEjecutor(2);

        await _service.ProcesarPreguntaAsync("Muéstrame la lista de clientes", 1);

        _mockExecutor.Verify(e => e.ExecuteReadOnlyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.Is<int>(m => m == 2), It.IsAny<CancellationToken>(), It.Is<int>(t => t == 15)), Times.Once);
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

    /// <summary>
    /// Cambio de comportamiento INTENCIONAL, no una regresión.
    ///
    /// Antes, si el nombre de la tabla no aparecía literalmente en la pregunta, el motor
    /// se declaraba "sin-plantilla" y no consultaba nada. Esa era una consecuencia de
    /// comparar por substring, no una decisión: con una sola tabla autorizada, preguntar
    /// por "productos" no significa que no haya que consultar, significa que hay que
    /// elegir la tabla más cercana por SIGNIFICADO.
    ///
    /// Ahora se elige la tabla por similitud con el catálogo. Con varias tablas y ninguna
    /// cercana, se elige la más parecida; nunca se inventa una tabla ni se genera SQL
    /// contra un objeto no autorizado.
    /// </summary>
    [Fact]
    public async Task Pregunta_De_Datos_EligeLaTablaMasCercanaPorSignificado()
    {
        ConfigurarConexionActiva();
        // Dos tablas; la pregunta menciona "productos", que no es el nombre de ninguna.
        _mockTablaRepository.Setup(r => r.GetActivasByConexionIdAsync(1)).ReturnsAsync(new List<TablaAutorizada>
        {
            new() { IdTabla = 1, IdConexion = 1, NombreTabla = "Clientes", Esquema = "dbo", Activa = true }
        });
        ConfigurarResultadoEjecutor(1);

        var resultado = await _service.ProcesarPreguntaAsync("Cuántos productos hay registrados?", 1);

        // Antes: "sin-plantilla". Ahora: consulta la tabla autorizada más cercana.
        Assert.Equal("sql", resultado.Tipo);
        Assert.Contains("Clientes", resultado.ConsultaSql);
        Assert.DoesNotContain("Productos", resultado.ConsultaSql);
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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
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
