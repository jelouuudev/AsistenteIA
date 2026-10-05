using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class QueryEmpresarialService : IQueryEmpresarialService
{
    private readonly IConexionBaseDatosRepository _conexionRepository;
    private readonly ITablaAutorizadaRepository _tablaRepository;
    private readonly IVistaAutorizadaRepository _vistaRepository;
    private readonly IConsultaPlantillaRepository _plantillaRepository;
    private readonly IConfiguracionMotorConsultasRepository _configRepository;
    private readonly IConsultaEjecutadaRepository _consultaRepository;
    private readonly IConexionCifrador _cifrador;
    private readonly ISqlQueryExecutor _executor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<QueryEmpresarialService> _logger;

    private readonly IEmbeddingProvider? _embeddingProvider;

    public QueryEmpresarialService(
        IConexionBaseDatosRepository conexionRepository,
        ITablaAutorizadaRepository tablaRepository,
        IVistaAutorizadaRepository vistaRepository,
        IConsultaPlantillaRepository plantillaRepository,
        IConfiguracionMotorConsultasRepository configRepository,
        IConsultaEjecutadaRepository consultaRepository,
        IConexionCifrador cifrador,
        ISqlQueryExecutor executor,
        IUnitOfWork unitOfWork,
        ILogger<QueryEmpresarialService> logger,
        IEmbeddingProvider? embeddingProvider = null)
    {
        _conexionRepository = conexionRepository;
        _tablaRepository = tablaRepository;
        _vistaRepository = vistaRepository;
        _plantillaRepository = plantillaRepository;
        _configRepository = configRepository;
        _consultaRepository = consultaRepository;
        _cifrador = cifrador;
        _executor = executor;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _embeddingProvider = embeddingProvider;
    }

    /// <summary>
    /// Descripción de la CAPACIDAD de este motor, no una lista de palabras. Antes había
    /// dos listas fijas: IndicadoresDatos (clientes, pedidos, facturas, ventas, productos,
    /// stock, tickets, órdenes, monedas, roles...) y PalabrasClaveDatos (idéntica, otra vez).
    /// Es vocabulario del negocio actual: con otra base de datos ninguna de esas palabras
    /// aparece y el motor se declara inactivo aunque la pregunta sí sea de datos; peor,
    /// dos listas que deben mantenerse sincronizadas a mano.
    /// Ahora se compara la pregunta con esta descripción por similitud: describe lo que
    /// el motor HACE, que es lo mismo en cualquier esquema.
    /// </summary>
    private static readonly string DescripcionCapacidadDatos =
        "consulta de datos de la base de datos: inquire, consulta, lista, busca registros y " +
        "reporta totales, conteos, promedios y sumas de tablas y vistas autorizadas";

    // Control de concurrencia (MaxConsultasSimultaneas; antes no se leia: parametro muerto).
    private static int _consultasEnCurso;
    private static readonly object _candadoConcurrencia = new();

    private static bool IntentarEntrar(int maximo)
    {
        lock (_candadoConcurrencia)
        {
            if (_consultasEnCurso >= Math.Max(1, maximo)) return false;
            _consultasEnCurso++;
            return true;
        }
    }

    private static void Salir()
    {
        lock (_candadoConcurrencia)
        {
            if (_consultasEnCurso > 0) _consultasEnCurso--;
        }
    }

    public async Task<ResultadoProcesarPreguntaDto> ProcesarPreguntaAsync(
        string pregunta,
        int idUsuario,
        CancellationToken cancellationToken = default)
    {
        var respuesta = new ResultadoProcesarPreguntaDto { Exitoso = false };

        try
        {
            if (string.IsNullOrWhiteSpace(pregunta))
            {
                respuesta.Error = "La pregunta no puede estar vacía.";
                return respuesta;
            }

            if (!await EsPreguntaDeDatosAsync(pregunta, cancellationToken))
            {
                respuesta.Tipo = "documental";
                respuesta.Exitoso = true;
                respuesta.Fuente = "rag";
                return respuesta;
            }

            var config = await _configRepository.GetActivaAsync();
            if (config == null || !config.Activo)
            {
                respuesta.Tipo = "deshabilitado";
                respuesta.Respuesta = "El motor de consultas empresariales está deshabilitado. Contacte al administrador.";
                respuesta.Exitoso = true;
                return respuesta;
            }

            var conexionesActivas = (await _conexionRepository.GetActivasAsync()).ToList();
            if (conexionesActivas.Count == 0)
            {
                respuesta.Tipo = "sin-conexion";
                respuesta.Error = "No hay conexiones a bases de datos configuradas. Contacte al administrador.";
                return respuesta;
            }

            var conexion = SeleccionarConexion(conexionesActivas, config, pregunta);
            var (sql, plantilla, parametrosFaltantes) = await ConstruirConsultaAsync(pregunta, conexion, cancellationToken);

            if (string.IsNullOrWhiteSpace(sql))
            {
                if (plantilla != null && parametrosFaltantes.Count > 0)
                {
                    respuesta.Tipo = "falta-parametro";
                    respuesta.Error = $"La plantilla '{plantilla.Nombre}' necesita el valor de {string.Join(", ", parametrosFaltantes.Select(f => "@" + f))}. " +
                        $"Indíquelo en la pregunta (ej: {parametrosFaltantes[0]}: Finanzas) o ejecútela desde Consultas Empresariales.";
                    return respuesta;
                }
                respuesta.Tipo = "sin-plantilla";
                respuesta.Error = "No se pudo interpretar la consulta. Intente reformularla o use una plantilla existente.";
                return respuesta;
            }

            var validacion = await ValidarAutorizacionAsync(sql, conexion.IdConexion, cancellationToken);
            if (!validacion.Autorizado)
            {
                respuesta.Tipo = "bloqueada";
                respuesta.Error = $"La consulta fue bloqueada: {validacion.Motivo}";
                respuesta.Exitoso = true;
                await RegistrarAuditoriaAsync(pregunta, "Consulta", sql, conexion.IdConexion, idUsuario,
                    EstadoConsulta.Bloqueada, 0, 0, respuesta.Error);
                return respuesta;
            }

            var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
            var inicio = DateTime.UtcNow;
            if (!IntentarEntrar(config.MaxConsultasSimultaneas))
            {
                respuesta.Tipo = "ocupado";
                respuesta.Error = "El motor de consultas está ocupado (máximo de consultas simultáneas alcanzado). Intente de nuevo en unos segundos.";
                return respuesta;
            }
            IEnumerable<Dictionary<string, object?>> datos;
            try
            {
                datos = await _executor.ExecuteReadOnlyAsync(
                    cadena, sql, null, config.MaximoRegistros, cancellationToken, config.TiempoMaximoEjecucionSegundos);
            }
            finally
            {
                Salir();
            }
            var tiempoMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;

            var registros = datos.ToList();
            var columnas = registros.Count > 0
                ? registros[0].Keys.ToList()
                : ExtraerColumnasDeSql(sql);

            var resumen = GenerarResumen(pregunta, registros, plantilla);

            await RegistrarAuditoriaAsync(pregunta, "Consulta", sql, conexion.IdConexion, idUsuario,
                EstadoConsulta.Completada, registros.Count, tiempoMs, resumen);

            respuesta.Exitoso = true;
            respuesta.Tipo = "sql";
            respuesta.Fuente = conexion.Nombre;
            respuesta.IdConexion = conexion.IdConexion;
            respuesta.ConsultaSql = sql;
            respuesta.CantidadRegistros = registros.Count;
            respuesta.Datos = registros;
            respuesta.Respuesta = resumen;
            respuesta.Error = null;

            _logger.LogInformation("Consulta empresarial ejecutada ({Tipo}) en {Ms}ms con {N} registros. Pregunta: {Pregunta}",
                plantilla?.Nombre ?? "ad-hoc", tiempoMs, registros.Count, pregunta);

            return respuesta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando pregunta empresarial: {Pregunta}", pregunta);
            respuesta.Tipo = "error";
            respuesta.Error = $"Ocurrió un error al procesar la consulta: {ex.Message}";
            return respuesta;
        }
    }

    public async Task<EjecutarConsultaResponse> EjecutarConsultaAsync(
        EjecutarConsultaRequest request,
        int idUsuario,
        CancellationToken cancellationToken = default)
    {
        var response = new EjecutarConsultaResponse { Exitoso = false };

        try
        {
            var conexion = await _conexionRepository.GetByIdAsync(request.IdConexion)
                ?? throw new KeyNotFoundException($"Conexión con ID {request.IdConexion} no encontrada.");

            string sql;
            if (request.IdPlantilla.HasValue)
            {
                var plantilla = await _plantillaRepository.GetByIdAsync(request.IdPlantilla.Value)
                    ?? throw new KeyNotFoundException($"Plantilla con ID {request.IdPlantilla} no encontrada.");
                sql = plantilla.ConsultaSql;
            }
            else
            {
                sql = request.ConsultaSql ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(sql))
            {
                response.Error = "No se proporcionó una consulta SQL válida.";
                return response;
            }

            var validacion = await ValidarAutorizacionAsync(sql, conexion.IdConexion, cancellationToken);
            if (!validacion.Autorizado)
            {
                response.Estado = nameof(EstadoConsulta.Bloqueada);
                response.Error = $"La consulta fue bloqueada: {validacion.Motivo}";
                await RegistrarAuditoriaAsync(request.Pregunta ?? sql, "Consulta manual", sql,
                    conexion.IdConexion, idUsuario, EstadoConsulta.Bloqueada, 0, 0, response.Error);
                return response;
            }

            var config = await _configRepository.GetActivaAsync();
            var maxRows = config?.MaximoRegistros ?? 100;
            var timeoutSegundos = config?.TiempoMaximoEjecucionSegundos ?? 15;

            var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
            var inicio = DateTime.UtcNow;
            if (!IntentarEntrar(config?.MaxConsultasSimultaneas ?? 5))
            {
                response.Error = "El motor de consultas está ocupado (máximo de consultas simultáneas alcanzado). Intente de nuevo en unos segundos.";
                response.Estado = nameof(EstadoConsulta.Bloqueada);
                return response;
            }
            IEnumerable<Dictionary<string, object?>> datosRaw;
            try
            {
                datosRaw = await _executor.ExecuteReadOnlyAsync(
                    cadena, sql, request.Parametros, maxRows, cancellationToken, timeoutSegundos);
            }
            finally
            {
                Salir();
            }
            var datos = datosRaw;
            var tiempoMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;

            var registros = datos.ToList();
            response.Exitoso = true;
            response.Estado = nameof(EstadoConsulta.Completada);
            response.TiempoEjecucionMs = tiempoMs;
            response.CantidadRegistros = registros.Count;
            response.Columnas = registros.Count > 0
                ? registros[0].Keys.ToList()
                : ExtraerColumnasDeSql(sql);
            response.Registros = registros;
            response.ResumenDatos = GenerarResumen(request.Pregunta ?? "Consulta", registros, null);

            await RegistrarAuditoriaAsync(request.Pregunta ?? "Consulta manual", "Consulta manual", sql,
                conexion.IdConexion, idUsuario, EstadoConsulta.Completada, registros.Count, tiempoMs, response.ResumenDatos);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando consulta empresarial en conexión {IdConexion}.", request.IdConexion);
            response.Estado = nameof(EstadoConsulta.Error);
            response.Error = $"Error al ejecutar la consulta: {ex.Message}";

            try
            {
                await RegistrarAuditoriaAsync(request.Pregunta ?? "Consulta manual",
                    "Consulta manual", request.ConsultaSql ?? string.Empty, request.IdConexion,
                    idUsuario, EstadoConsulta.Error, 0, 0, response.Error);
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "No se pudo registrar la auditoría del error.");
            }

            return response;
        }
    }

    /// <summary>
    /// ¿La pregunta pide datos de la base? Antes se respondía buscando ~40 palabras del
    /// negocio actual (clientes, facturas, stock, tickets, monedas, roles...). Contra una
    /// base nueva ninguna aparece y el motor se declaraba inactivo aunque la pregunta sí
    /// fuera de datos. Ahora se compara la pregunta con la DESCRIPCIÓN de la capacidad:
    /// "consulta de datos de la base de datos..." describe el comportamiento del motor, no
    /// el dominio, y vale para cualquier esquema.
    /// Sin embeddings disponible no se decide (se responde que el motor no aplica): es
    /// preferible no activar la consulta a activarla por una lista de palabras.
    /// </summary>
    private async Task<bool> EsPreguntaDeDatosAsync(string pregunta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pregunta)) return false;
        if (_embeddingProvider is null) return false;
        try
        {
            var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);
            var embCapacidad = await _embeddingProvider.GenerateEmbeddingAsync(DescripcionCapacidadDatos).WaitAsync(ct);
            return SeleccionHerramientaSemantica.Coseno(embPregunta, embCapacidad) >= 0.35;
        }
        catch
        {
            return false;
        }
    }

    private static ConexionBaseDatos SeleccionarConexion(
        List<ConexionBaseDatos> conexiones,
        ConfiguracionMotorConsultas config,
        string pregunta)
    {
        if (config.IdConexionPredeterminada.HasValue)
        {
            var predeterminada = conexiones.FirstOrDefault(c => c.IdConexion == config.IdConexionPredeterminada.Value);
            if (predeterminada != null)
                return predeterminada;
        }

        return conexiones.First();
    }

    private async Task<(string Sql, ConsultaPlantilla? Plantilla, List<string> ParametrosFaltantes)> ConstruirConsultaAsync(
        string pregunta,
        ConexionBaseDatos conexion,
        CancellationToken cancellationToken = default)
    {
        var plantillas = (await _plantillaRepository.GetActivasByConexionIdAsync(conexion.IdConexion)).ToList();

        var candidatas = plantillas
            .Select(p => new { Plantilla = p, Score = CalcularCoincidencia(pregunta, $"{p.Nombre} {p.Descripcion}") })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        // Prefiere la mejor plantilla cuyos parametros se puedan resolver con la pregunta.
        // Si ninguna se resuelve, se informa la de mayor puntaje con sus parametros faltantes.
        ConsultaPlantilla? mejorSinResolver = null;
        List<string> faltantesMejor = new();
        foreach (var c in candidatas)
        {
            var (sqlCandidata, faltantesCandidata) = ResolverParametrosPlantilla(
                c.Plantilla.ConsultaSql, pregunta, c.Plantilla.Parametros);
            if (faltantesCandidata.Count == 0)
                return (sqlCandidata, c.Plantilla, new List<string>());
            mejorSinResolver ??= c.Plantilla;
            if (faltantesMejor.Count == 0)
                faltantesMejor = faltantesCandidata;
        }

        if (mejorSinResolver != null)
        {
            // La plantilla no se pudo resolver: intentar consulta ad-hoc antes de
            // exigir parametros. El lenguaje natural debe funcionar sin plantillas.
            var tablasFallback = (await _tablaRepository.GetActivasByConexionIdAsync(conexion.IdConexion)).ToList();
            var vistasFallback = (await _vistaRepository.GetActivasByConexionIdAsync(conexion.IdConexion)).ToList();
            var sqlAdHoc = await GenerarConsultaAdHocAsync(pregunta, tablasFallback, vistasFallback, conexion, cancellationToken);
            if (!string.IsNullOrWhiteSpace(sqlAdHoc))
                return (sqlAdHoc, null, new List<string>());
            return (string.Empty, mejorSinResolver, faltantesMejor);
        }

        var tablas = (await _tablaRepository.GetActivasByConexionIdAsync(conexion.IdConexion)).ToList();
        var vistas = (await _vistaRepository.GetActivasByConexionIdAsync(conexion.IdConexion)).ToList();

        var sqlGenerada = await GenerarConsultaAdHocAsync(pregunta, tablas, vistas, conexion, cancellationToken);
        return (sqlGenerada, null, new List<string>());
    }

    /// <summary>
    /// Generador ad-hoc de consultas.
    ///
    /// Antes elegía intención, tabla y columnas por PALABRAS FIJAS: buscaba
    /// "cuantos/contar/total de", "promedio", "suma", y devolvía literalmente las columnas
    /// "Cantidad", "Total", "Monto", "Nombre", "Correo", "Telefono", "Estado" y "Fecha".
    /// Eso solo funciona en tablas con ese esquema: en cualquier base nueva esas columnas
    /// no existen y el SQL falla, y pedir "el promedio de edad" nunca encuentra "Edad"
    /// porque no está en la lista.
    ///
    /// Ahora la intención se decide por similitud con descripciones de cada una, la
    /// tabla por su Descripcion configurada, y las columnas salen del CATALOGO real de la
    /// tabla elegida (la de agregación, entre las numéricas). Agregar una base o tabla no
    /// requiere tocar este método.
    /// </summary>
    private async Task<string> GenerarConsultaAdHocAsync(
        string pregunta, List<TablaAutorizada> tablas, List<VistaAutorizada> vistas,
        ConexionBaseDatos conexion, CancellationToken ct)
    {
        var tabla = await SeleccionarTablaPorDescripcionAsync(pregunta, tablas, vistas, ct);
        if (tabla is null) return string.Empty;

        var tablaCalificada = $"[{tabla.Value.Esquema}].[{tabla.Value.Nombre}]";
        var catalogo = await ObtenerCatalogoTablaAsync(conexion, tabla.Value.Esquema, tabla.Value.Nombre, ct);
        var intencion = await DetectarIntencionAsync(pregunta, ct);

        if (intencion == IntencionConsulta.Conteo)
            return $"SELECT COUNT(*) AS Total FROM {tablaCalificada};";

        var numericas = catalogo.Where(c => c.EsNumerica).Select(c => c.Nombre).ToList();
        var agregacion = numericas.Count > 0 ? await ElegirColumnaAsync(pregunta, numericas, ct) : null;

        if (!string.IsNullOrEmpty(agregacion))
        {
            if (intencion == IntencionConsulta.Suma)
                return $"SELECT SUM([{agregacion}]) AS Total FROM {tablaCalificada};";
            if (intencion == IntencionConsulta.Promedio)
                return $"SELECT AVG([{agregacion}]) AS Promedio FROM {tablaCalificada};";
        }

        var columnas = await ElegirColumnasAsync(pregunta, catalogo.Select(c => c.Nombre).ToList(), ct);
        if (columnas.Count > 0)
            return $"SELECT {string.Join(", ", columnas.Select(c => $"[{c}]"))} FROM {tablaCalificada};";

        return $"SELECT * FROM {tablaCalificada};";
    }

    private enum IntencionConsulta { Conteo, Suma, Promedio, Listado }

    /// <summary>
    /// Descripciones de cada intención de agregación. La pregunta se compara con ellas por
    /// similitud: no hay que enumerar "cuantos", "total de", "media de" y sus variantes.
    /// </summary>
    private static readonly string DescConteo = "cuenta cuantos registros hay en total, numero de filas";
    private static readonly string DescSuma = "suma los valores de una columna numerica, total acumulado";
    private static readonly string DescPromedio = "promedio o media aritmetica de una columna numerica";

    private async Task<IntencionConsulta> DetectarIntencionAsync(string pregunta, CancellationToken ct)
    {
        if (_embeddingProvider is null) return IntencionConsulta.Listado;
        try
        {
            var emb = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);
            async Task<double> Sim(string descripcion)
                => SeleccionHerramientaSemantica.Coseno(emb, await _embeddingProvider!.GenerateEmbeddingAsync(descripcion).WaitAsync(ct));

            var c = await Sim(DescConteo);
            var s = await Sim(DescSuma);
            var p = await Sim(DescPromedio);
            if (p >= c && p >= s) return IntencionConsulta.Promedio;
            if (s >= c) return IntencionConsulta.Suma;
            if (c > 0.30) return IntencionConsulta.Conteo;
            return IntencionConsulta.Listado;
        }
        catch { return IntencionConsulta.Listado; }
    }

    private async Task<(string Esquema, string Nombre)?> SeleccionarTablaPorDescripcionAsync(
        string pregunta, List<TablaAutorizada> tablas, List<VistaAutorizada> vistas, CancellationToken ct)
    {
        var candidatas = new List<(string Esquema, string Nombre, string Descripcion)>();
        foreach (var t in (tablas ?? new List<TablaAutorizada>()).Where(t => t.Activa))
            candidatas.Add((t.Esquema, t.NombreTabla, t.Descripcion ?? ""));
        foreach (var v in (vistas ?? new List<VistaAutorizada>()).Where(v => v.Activa))
            candidatas.Add(("dbo", v.NombreVista, v.Descripcion ?? ""));
        if (candidatas.Count == 0) return null;

        if (_embeddingProvider is not null)
        {
            try
            {
                var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);
                var mejor = candidatas[0];
                var mejorSim = double.NegativeInfinity;
                foreach (var c in candidatas)
                {
                    var texto = string.IsNullOrWhiteSpace(c.Descripcion) ? c.Nombre : c.Nombre + " " + c.Descripcion;
                    var sim = SeleccionHerramientaSemantica.Coseno(
                        embPregunta, await _embeddingProvider.GenerateEmbeddingAsync(texto).WaitAsync(ct));
                    if (sim > mejorSim) { mejorSim = sim; mejor = c; }
                }
                return (mejor.Esquema, mejor.Nombre);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MotorConsultas: no se pudo elegir tabla por similitud.");
            }
        }

        // SIN embeddings no se decide tabla: devolver la primera sería adivinar y
        // consultar la tabla equivocada. Antes se comparaba el nombre de la tabla con la
        // pregunta y, si no coincidía, se respondía "sin plantilla". Se conserva ese
        // contrato: es preferible no generar SQL a generar el de otra tabla.
        return null;
    }

    private sealed record ColumnaCatalogo(string Nombre, bool EsNumerica);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, List<ColumnaCatalogo> Cols)> _cacheCatalogoColumnas
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Columnas reales de una tabla y su tipo, desde INFORMATION_SCHEMA.</summary>
    private async Task<List<ColumnaCatalogo>> ObtenerCatalogoTablaAsync(
        ConexionBaseDatos conexion, string esquema, string tabla, CancellationToken ct)
    {
        var clave = $"{conexion.IdConexion}:{esquema}.{tabla}";
        if (_cacheCatalogoColumnas.TryGetValue(clave, out var hit) && hit.Expira > DateTime.UtcNow)
            return hit.Cols;

        var resultado = new List<ColumnaCatalogo>();
        try
        {
            var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
            var filas = (await _executor.ExecuteReadOnlyAsync(
                cadena,
                "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION",
                new Dictionary<string, object?> { ["t"] = tabla }, 200, ct)).ToList();
            foreach (var f in filas)
            {
                var nombre = f.Values.ElementAtOrDefault(0)?.ToString();
                var tipo = (f.Values.ElementAtOrDefault(1)?.ToString() ?? "").ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(nombre)) continue;
                resultado.Add(new ColumnaCatalogo(nombre,
                    tipo is "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric"
                         or "money" or "smallmoney" or "float" or "real"));
            }
        }
        catch { }

        _cacheCatalogoColumnas[clave] = (DateTime.UtcNow.AddMinutes(10), resultado);
        return resultado;
    }

    private async Task<string?> ElegirColumnaAsync(string pregunta, List<string> candidatas, CancellationToken ct)
    {
        if (candidatas.Count == 0) return null;
        if (_embeddingProvider is null) return candidatas[0];
        try
        {
            var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);
            var mejor = candidatas[0];
            var mejorSim = double.NegativeInfinity;
            foreach (var c in candidatas)
            {
                var sim = SeleccionHerramientaSemantica.Coseno(embPregunta, await _embeddingProvider.GenerateEmbeddingAsync(c).WaitAsync(ct));
                if (sim > mejorSim) { mejorSim = sim; mejor = c; }
            }
            return mejor;
        }
        catch { return candidatas[0]; }
    }

    private async Task<List<string>> ElegirColumnasAsync(string pregunta, List<string> catalogo, CancellationToken ct)
    {
        if (catalogo.Count == 0 || _embeddingProvider is null) return new();

        // Umbral alto: preferimos traer menos columnas que elegir una que la tabla no tiene.
        const double umbral = 0.55;
        try
        {
            var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);
            var elegidas = new List<string>();
            foreach (var c in catalogo)
            {
                var sim = SeleccionHerramientaSemantica.Coseno(embPregunta, await _embeddingProvider.GenerateEmbeddingAsync(c).WaitAsync(ct));
                if (sim >= umbral) elegidas.Add(c);
            }
            return elegidas;
        }
        catch { return new(); }
    }

    private static (string? Esquema, string Nombre)? SeleccionarTablaPorPregunta(
        string pregunta,
        List<TablaAutorizada> tablas,
        List<VistaAutorizada> vistas)
    {
        var candidatas = new List<(string Esquema, string Nombre)>();

        foreach (var t in tablas)
            candidatas.Add((t.Esquema, t.NombreTabla));

        foreach (var v in vistas)
            candidatas.Add(("dbo", v.NombreVista));

        var tablasOrdenadas = candidatas
            .Select(t => new { Tabla = t, Score = CalcularCoincidencia(pregunta, t.Nombre) })
            .OrderByDescending(x => x.Score)
            .ToList();

        var mejor = tablasOrdenadas.FirstOrDefault(x => x.Score > 0);
        if (mejor != null)
            return (mejor.Tabla.Esquema, mejor.Tabla.Nombre);

        return null;
    }

    // Resuelve los parametros de una plantilla con valores extraidos de la pregunta.
    // Devuelve el SQL y la lista de parametros que no se pudieron resolver.
    private static (string Sql, List<string> Faltantes) ResolverParametrosPlantilla(
        string sql, string pregunta, string? definicionesJson)
    {
        sql = ResolverParametros(sql, pregunta);

        var faltantes = new List<string>();
        var definiciones = new List<DefinicionParametroPlantilla>();
        if (!string.IsNullOrWhiteSpace(definicionesJson))
        {
            try
            {
                definiciones = JsonSerializer.Deserialize<List<DefinicionParametroPlantilla>>(
                    definicionesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch { /* definicion invalida: se ignora */ }
        }

        // Parametros presentes en el SQL pero no definidos: tambien deben resolverse o fallar.
        var enSql = Regex.Matches(sql, @"@([A-Za-z_][A-Za-z0-9_]*)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var nombre in enSql)
        {
            var def = definiciones.FirstOrDefault(d =>
                string.Equals(d.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
            var valor = ExtraerValorParametro(pregunta, nombre);
            if (valor == null)
            {
                faltantes.Add(nombre);
                continue;
            }
            sql = Regex.Replace(sql, "@" + Regex.Escape(nombre) + @"\b",
                FormatearValorParametro(valor, def?.Tipo), RegexOptions.IgnoreCase);
        }

        return (sql, faltantes);
    }

    // Busca "Nombre: valor", "Nombre = valor" o "Nombre 'valor'" en la pregunta.
    private static string? ExtraerValorParametro(string pregunta, string nombre)
    {
        var patron = Regex.Escape(nombre) + @"\s*[:=]\s*(?:""([^""]+)""|'([^']+)'|(\S+))";
        var m = Regex.Match(pregunta, patron, RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value).Trim().TrimEnd('.', ',', ';');

        // Fallback: unico valor entre comillas en la pregunta.
        var comillas = Regex.Matches(pregunta, @"""([^""]+)""|'([^']+)'");
        if (comillas.Count == 1)
        {
            var g = comillas[0];
            return (g.Groups[1].Success ? g.Groups[1].Value : g.Groups[2].Value).Trim();
        }

        return null;
    }

    private static string FormatearValorParametro(string valor, string? tipo)
    {
        var t = (tipo ?? "string").Trim().ToLowerInvariant();
        if (t.Contains("int") && long.TryParse(valor, out _)) return valor;
        if ((t.Contains("decimal") || t.Contains("numeric") || t.Contains("double") || t.Contains("float") || t.Contains("number")) && double.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            return valor.Replace(',', '.');
        if (t.Contains("bool") && bool.TryParse(valor, out var b)) return b ? "1" : "0";
        if (t.Contains("date") && DateTime.TryParse(valor, out var f)) return $"'{f:yyyy-MM-dd}'";
        return "'" + valor.Replace("'", "''") + "'";
    }

    private sealed class DefinicionParametroPlantilla
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Tipo { get; set; }
    }

    private static string ResolverParametros(string sql, string pregunta)
    {
        if (sql.Contains("@FechaInicio", StringComparison.OrdinalIgnoreCase) || sql.Contains("@FechaFin", StringComparison.OrdinalIgnoreCase))
        {
            var hoy = DateTime.Today;
            sql = sql.Replace("@FechaInicio", $"'{hoy:yyyy-MM-dd}'")
                     .Replace("@FechaFin", $"'{hoy:yyyy-MM-dd}'");
        }

        return sql;
    }

    private async Task<(bool Autorizado, string? Motivo)> ValidarAutorizacionAsync(
        string sql,
        int idConexion,
        CancellationToken cancellationToken = default)
    {
        // Validación ESTRUCTURAL: tokenizador + gramática. Antes era un StartsWith("SELECT")
        // más una lista de palabras prohibidas buscadas por SUBSTRING, lo que rechazaba
        // consultas de solo lectura que tuvieran una columna "LastUpdate" o "IsDeleted"
        // y no detectaba un verbo dentro de un comentario. El analizador no depende del
        // dominio y es más estricto.
        var seguridad = AnalizadorSql.ValidarSoloLectura(sql);
        if (!seguridad.Seguro)
            return (false, seguridad.Motivo);

        var tablasReferenciadas = AnalizadorSql.ObjetosReferenciados(sql);
        var tablasAutorizadas = (await _tablaRepository.GetActivasByConexionIdAsync(idConexion)).ToList();
        var vistasAutorizadas = (await _vistaRepository.GetActivasByConexionIdAsync(idConexion)).ToList();

        var nombresAutorizados = new HashSet<string>(
            tablasAutorizadas.Select(t => t.NombreTabla.ToLowerInvariant())
                .Concat(vistasAutorizadas.Select(v => v.NombreVista.ToLowerInvariant())));

        foreach (var tabla in tablasReferenciadas)
        {
            if (!nombresAutorizados.Contains(tabla.Trim('[', ']', '"')))
            {
                return (false, $"la tabla u objeto '{tabla}' no está autorizado para consultas.");
            }
        }

        return (true, null);
    }


    private static List<string> ExtraerColumnasDeSql(string sql)
    {
        var columnas = new List<string>();
        foreach (Match m in Regex.Matches(sql, @"\[([A-Za-z_][A-Za-z0-9_]*)\]"))
        {
            columnas.Add(m.Groups[1].Value);
        }

        if (columnas.Count == 0)
        {
            var asMatch = Regex.Match(sql, @"AS\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
            while (asMatch.Success)
            {
                columnas.Add(asMatch.Groups[1].Value);
                asMatch = asMatch.NextMatch();
            }
        }

        return columnas.Distinct().ToList();
    }

    private async Task RegistrarAuditoriaAsync(
        string pregunta,
        string operacion,
        string sql,
        int idConexion,
        int idUsuario,
        EstadoConsulta estado,
        int cantidadRegistros,
        long tiempoMs,
        string? resultado)
    {
        var consulta = new ConsultaEjecutada
        {
            IdUsuario = idUsuario,
            IdConexion = idConexion,
            FechaHora = DateTime.UtcNow,
            PreguntaUsuario = pregunta,
            OperacionEjecutada = operacion,
            ConsultaGenerada = sql,
            TiempoEjecucion = tiempoMs,
            CantidadRegistros = cantidadRegistros,
            Estado = estado.ToString(),
            Resultado = Truncar(resultado, 3900)
        };

        await _consultaRepository.AddAsync(consulta);
        await _unitOfWork.SaveChangesAsync();
    }

    private static string GenerarResumen(string pregunta, List<Dictionary<string, object?>> registros, ConsultaPlantilla? plantilla)
    {
        if (registros.Count == 0)
        {
            return "La consulta se ejecutó correctamente pero no devolvió registros.";
        }

        if (registros.Count == 1 && registros[0].Count == 1)
        {
            var kvp = registros[0].First();
            var valor = FormatearValor(kvp.Value);
            return $"Total: {valor}";
        }

        if (plantilla != null)
        {
            var primerRegistro = registros[0];
            return string.Join("; ", primerRegistro.Select(kvp => $"{kvp.Key}: {FormatearValor(kvp.Value)}"));
        }

        return $"La consulta devolvió {registros.Count} registro(s).";
    }

    private static string FormatearValor(object? valor)
    {
        if (valor == null) return "N/A";
        if (valor is decimal d) return d.ToString(CultureInfo.InvariantCulture);
        if (valor is double dd) return dd.ToString(CultureInfo.InvariantCulture);
        if (valor is DateTime dt) return dt.ToString("dd/MM/yyyy");
        return valor.ToString() ?? string.Empty;
    }

    private static int CalcularCoincidencia(string pregunta, string texto)
    {
        var palabrasPregunta = Normalizar(pregunta)
            .Split([' ', ',', ';', '.', '¿', '?', '¡', '!', ':', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 2)
            .ToList();

        var textoNormalizado = Normalizar(texto);
        var coincidencias = palabrasPregunta.Count(p => textoNormalizado.Contains(p));

        var puntaje = coincidencias * 10;
        var textoPalabras = textoNormalizado.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var palabra in textoPalabras)
        {
            if (palabrasPregunta.Any(p => p.Contains(palabra) || palabra.Contains(p)))
                puntaje += 5;
        }

        return puntaje;
    }

    private static string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var reemplazos = new Dictionary<string, string>
        {
            ["á"] = "a", ["é"] = "e", ["í"] = "i", ["ó"] = "o", ["ú"] = "u",
            ["ü"] = "u", ["ñ"] = "n"
        };

        foreach (var kvp in reemplazos)
            texto = texto.Replace(kvp.Key, kvp.Value);

        return texto.ToLowerInvariant();
    }


    private static string? Truncar(string? valor, int max)
    {
        if (string.IsNullOrEmpty(valor)) return valor;
        return valor.Length <= max ? valor : valor[..max];
    }
}
