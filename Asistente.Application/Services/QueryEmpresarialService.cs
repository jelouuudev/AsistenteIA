using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
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
        ILogger<QueryEmpresarialService> logger)
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
    }

    private static readonly string[] IndicadoresDatos =
    [
        "clientes", "cliente", "pedidos", "pedido", "facturas", "factura", "ventas", "venta",
        "productos", "producto", "stock", "inventario", "proveedores", "proveedor", "empleados",
        "empleado", "usuarios", "usuario activo", "categorías", "categoría", "contar", "cuántos",
        "cuantos", "cuántas", "cuantas", "total", "promedio", "media", "suma", "sumar", "máximo",
        "mínimo", "minimo", "registros", "tickets", "órdenes", "ordenes", "orden", "cantidad",
        "activos", "activo", "monedas", "moneda", "ubicaciones", "ubicación", "movimientos",
        "historial", "roles", "rol"
    ];

    private static readonly string[] PalabrasClaveDatos =
    [
        "cliente", "pedido", "factura", "venta", "producto", "stock", "proveedor", "empleado",
        "usuario", "categoria", "ticket", "orden", "inventario", "cantidad", "precio", "total",
        "cuenta", "registro", "membresia", "suscripcion", "asignacion", "activos", "activo",
        "moneda", "ubicacion", "movimiento", "historial", "rol"
    ];

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

            if (!EsPreguntaDeDatos(pregunta))
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

    private bool EsPreguntaDeDatos(string pregunta)
    {
        var texto = Normalizar(pregunta);

        foreach (var palabra in IndicadoresDatos)
        {
            if (texto.Contains(Normalizar(palabra)))
                return true;
        }

        return false;
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
            var sqlAdHoc = GenerarConsultaAdHoc(pregunta, tablasFallback, vistasFallback);
            if (!string.IsNullOrWhiteSpace(sqlAdHoc))
                return (sqlAdHoc, null, new List<string>());
            return (string.Empty, mejorSinResolver, faltantesMejor);
        }

        var tablas = (await _tablaRepository.GetActivasByConexionIdAsync(conexion.IdConexion)).ToList();
        var vistas = (await _vistaRepository.GetActivasByConexionIdAsync(conexion.IdConexion)).ToList();

        var sqlGenerada = GenerarConsultaAdHoc(pregunta, tablas, vistas);
        return (sqlGenerada, null, new List<string>());
    }

    private string GenerarConsultaAdHoc(string pregunta, List<TablaAutorizada> tablas, List<VistaAutorizada> vistas)
    {
        var normalizada = Normalizar(pregunta);
        var esConteo = Contiene(normalizada, "cuantos", "cuántos", "cuantas", "cuántas", "contar", "total de");
        var esPromedio = Contiene(normalizada, "promedio", "media de");
        var esSuma = Contiene(normalizada, "suma", "sumar", "total de");

        var tabla = SeleccionarTablaPorPregunta(normalizada, tablas, vistas);
        if (tabla == null)
            return string.Empty;

        var tablaCalificada = $"[{tabla.Value.Esquema}].[{tabla.Value.Nombre}]";

        if (esConteo && !esSuma)
        {
            return $"SELECT COUNT(*) AS Total FROM {tablaCalificada};";
        }

        var columnaAgregacion = SeleccionarColumnaAgregacion(normalizada);
        if (esSuma && columnaAgregacion != null)
        {
            return $"SELECT SUM([{columnaAgregacion}]) AS Total FROM {tablaCalificada};";
        }

        if (esPromedio && columnaAgregacion != null)
        {
            return $"SELECT AVG([{columnaAgregacion}]) AS Promedio FROM {tablaCalificada};";
        }

        var columnas = SeleccionarColumnas(normalizada);
        if (columnas.Count > 0)
        {
            return $"SELECT {string.Join(", ", columnas.Select(c => $"[{c}]"))} FROM {tablaCalificada};";
        }

        return $"SELECT * FROM {tablaCalificada};";
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

    private static string? SeleccionarColumnaAgregacion(string pregunta)
    {
        if (Contiene(pregunta, "cantidad")) return "Cantidad";
        if (Contiene(pregunta, "precio", "monto", "importe", "valor", "total")) return "Total";
        if (Contiene(pregunta, "monto")) return "Monto";
        return null;
    }

    private static List<string> SeleccionarColumnas(string pregunta)
    {
        var columnas = new List<string>();
        if (Contiene(pregunta, "nombre")) columnas.Add("Nombre");
        if (Contiene(pregunta, "correo", "email")) columnas.Add("Correo");
        if (Contiene(pregunta, "telefono", "celular")) columnas.Add("Telefono");
        if (Contiene(pregunta, "estado")) columnas.Add("Estado");
        if (Contiene(pregunta, "fecha")) columnas.Add("Fecha");
        return columnas.Distinct().ToList();
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
        var sqlNormalizado = sql.TrimStart();
        if (!sqlNormalizado.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "solo se permiten consultas SELECT de solo lectura.");
        }

        if (sql.Contains(';') && !sqlNormalizado.TrimEnd().EndsWith(";", StringComparison.Ordinal))
        {
            return (false, "solo se permite una sola sentencia por consulta.");
        }

        if (Contiene(sql, "insert", "update", "delete", "drop", "alter", "truncate", "exec", "sp_", "xp_", "grant", "revoke", "create"))
        {
            return (false, "la consulta contiene operaciones no permitidas.");
        }

        var tablasReferenciadas = ExtraerTablasDeSql(sql);
        var tablasAutorizadas = (await _tablaRepository.GetActivasByConexionIdAsync(idConexion)).ToList();
        var vistasAutorizadas = (await _vistaRepository.GetActivasByConexionIdAsync(idConexion)).ToList();

        var nombresAutorizados = new HashSet<string>(
            tablasAutorizadas.Select(t => t.NombreTabla.ToLowerInvariant())
                .Concat(vistasAutorizadas.Select(v => v.NombreVista.ToLowerInvariant())));

        foreach (var tabla in tablasReferenciadas)
        {
            if (!nombresAutorizados.Contains(tabla))
            {
                return (false, $"la tabla u objeto '{tabla}' no está autorizado para consultas.");
            }
        }

        return (true, null);
    }

    private static List<string> ExtraerTablasDeSql(string sql)
    {
        var tablas = new List<string>();
        var patronIdentificador = @"(?:\[?[A-Za-z_][A-Za-z0-9_]*\]?\.\s*)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?";

        var fromMatch = Regex.Match(sql, $@"\bFROM\s+{patronIdentificador}", RegexOptions.IgnoreCase);
        if (fromMatch.Success)
            tablas.Add(fromMatch.Groups[1].Value);

        foreach (Match m in Regex.Matches(sql, $@"\bJOIN\s+{patronIdentificador}", RegexOptions.IgnoreCase))
        {
            tablas.Add(m.Groups[1].Value);
        }

        return tablas.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(t => t.ToLowerInvariant())
            .ToList();
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

    private static bool Contiene(string texto, params string[] terminos)
    {
        foreach (var t in terminos)
        {
            if (texto.Contains(Normalizar(t)))
                return true;
        }
        return false;
    }

    private static string? Truncar(string? valor, int max)
    {
        if (string.IsNullOrEmpty(valor)) return valor;
        return valor.Length <= max ? valor : valor[..max];
    }
}
