using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta que obtiene información autorizada desde SQL Server.
/// Implementación autónoma con su propia lógica de autorización (solo SELECT de solo lectura
/// sobre tablas/vistas autorizadas). El modelo nunca accede directamente a la base de datos.
/// </summary>
public class SqlQueryTool : ITool
{
    private readonly IConexionBaseDatosRepository _conexionRepository;
    private readonly ITablaAutorizadaRepository _tablaRepository;
    private readonly IVistaAutorizadaRepository _vistaRepository;
    private readonly IConexionCifrador _cifrador;
    private readonly ISqlQueryExecutor _executor;
    private readonly ILogger<SqlQueryTool> _logger;

    public SqlQueryTool(
        IConexionBaseDatosRepository conexionRepository,
        ITablaAutorizadaRepository tablaRepository,
        IVistaAutorizadaRepository vistaRepository,
        IConexionCifrador cifrador,
        ISqlQueryExecutor executor,
        ILogger<SqlQueryTool> logger)
    {
        _conexionRepository = conexionRepository;
        _tablaRepository = tablaRepository;
        _vistaRepository = vistaRepository;
        _cifrador = cifrador;
        _executor = executor;
        _logger = logger;
    }

    public string Name => "SqlQueryTool";
    public string Description =>
        "Ejecuta una consulta SELECT de solo lectura sobre la base de datos empresarial autorizada. " +
        "Úsala para preguntas de datos (totales, conteos, listados). " +
        "Parámetro 'pregunta' (lo que el usuario quiere saber) o 'consulta' (SQL explícito). " +
        "Solo se permiten SELECT sobre tablas/vistas previamente autorizadas.";
    public string Categoria => "ConsultaSQL";

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var pregunta = ObtenerString(request.Parametros, "pregunta", request.PreguntaOriginal ?? string.Empty);
        var sqlSugerido = ObtenerString(request.Parametros, "consulta", string.Empty);
        // ETAPA 19.2: el Planner puede forzar la agregación (GROUP BY) o el modo raw (SELECT *)
        // según el nombre del paso.
        var forzarAgregacion = request.Parametros.TryGetValue("forzarAgregacion", out var fa) && fa is true;
        var forzarRaw = request.Parametros.TryGetValue("forzarRaw", out var fr) && fr is true;

        if (string.IsNullOrWhiteSpace(pregunta) && string.IsNullOrWhiteSpace(sqlSugerido))
            return new ToolExecutionResult { Exitoso = false, Error = "No se proporcionó una pregunta ni una consulta SQL." };

        // Selección de conexión: se elige la conexión activa CUYA lista de tablas autorizadas
        // contenga la tabla que mapea la pregunta (ej. "activos" -> [Activos] en ControlActivos).
        var conexionesActivas = (await _conexionRepository.GetActivasAsync()).ToList();
        if (conexionesActivas.Count == 0)
            return new ToolExecutionResult { Exitoso = false, Error = "No hay conexiones a bases de datos configuradas." };

        ConexionBaseDatos? conexion = null;
        if (!string.IsNullOrWhiteSpace(pregunta))
        {
            foreach (var c in conexionesActivas)
            {
                if (GenerarSqlDesdePregunta(pregunta, c, false, false) != null) { conexion = c; break; }
            }
            // Respaldo: la pregunta original suele nombrar la tabla
            // (el Planner la incluye: "Consultar datos de Empleados...").
            if (conexion == null && !string.IsNullOrWhiteSpace(request.PreguntaOriginal) && request.PreguntaOriginal != pregunta)
            {
                foreach (var c in conexionesActivas)
                {
                    if (GenerarSqlDesdePregunta(request.PreguntaOriginal, c, false, false) != null) { conexion = c; break; }
                }
            }
        }
        conexion ??= conexionesActivas.First();

        var sql = string.IsNullOrWhiteSpace(sqlSugerido)
            ? GenerarSqlDesdePregunta(pregunta, conexion, forzarAgregacion, forzarRaw)
            : sqlSugerido;
        if (string.IsNullOrWhiteSpace(sql) && !string.IsNullOrWhiteSpace(request.PreguntaOriginal) && request.PreguntaOriginal != pregunta)
            sql = GenerarSqlDesdePregunta(request.PreguntaOriginal, conexion, forzarAgregacion, forzarRaw);

        if (string.IsNullOrWhiteSpace(sql))
            return new ToolExecutionResult { Exitoso = false, Error = "No se pudo determinar una consulta válida para la pregunta." };

        // Si la consulta sugerida por el modelo no referencia ninguna tabla autorizada,
        // pero tenemos la pregunta original, regeneramos a partir de ella.
        var validacionInicial = ValidarAutorizacion(sql, conexion);
        if (!validacionInicial.Autorizado && !string.IsNullOrWhiteSpace(pregunta))
        {
            var sqlDesdePregunta = GenerarSqlDesdePregunta(pregunta, conexion);
            if (sqlDesdePregunta != null)
            {
                sql = sqlDesdePregunta;
                validacionInicial = ValidarAutorizacion(sql, conexion);
            }
        }

        var validacion = validacionInicial;
        if (!validacion.Autorizado)
            return new ToolExecutionResult { Exitoso = false, Error = $"Consulta rechazada por políticas de seguridad: {validacion.Motivo}" };

        var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
        var inicio = DateTime.UtcNow;
        _logger.LogInformation("SqlQueryTool SQL: {Sql}", sql);
        var datos = (await _executor.ExecuteReadOnlyAsync(cadena, sql, null, 100, cancellationToken)).ToList();
        var tiempoMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;

        var columnas = datos.Count > 0 ? datos[0].Keys.ToList() : new List<string>();
        var resumen = GenerarResumen(datos, columnas);

        var tablas = ExtraerTablas(sql);
        var origen = tablas.Count > 0 ? tablas[0] : "la base de datos";
        _logger.LogInformation("SqlQueryTool ejecutó consulta en {Ms}ms con {N} registros.", tiempoMs, datos.Count);

        return new ToolExecutionResult
        {
            Exitoso = true,
            Contenido = $"Datos obtenidos de la tabla '{origen}' (base de datos '{conexion.BaseDatos}'):\n{resumen}",
            Metadatos = new()
            {
                ["sql"] = sql,
                ["cantidadRegistros"] = datos.Count,
                ["columnas"] = string.Join(", ", columnas),
                ["tiempoMs"] = tiempoMs,
                ["conexion"] = conexion.Nombre
            }
        };
    }

    private static (bool Autorizado, string? Motivo) ValidarAutorizacion(string sql, ConexionBaseDatos conexion)
    {
        var normalizado = sql.TrimStart();
        if (!normalizado.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return (false, "solo se permiten consultas SELECT de solo lectura.");

        if (sql.Contains(';') && !normalizado.TrimEnd().EndsWith(';'))
            return (false, "solo se permite una sola sentencia por consulta.");

        if (Contiene(sql, "insert", "update", "delete", "drop", "alter", "truncate", "exec", "sp_", "xp_", "grant", "revoke", "create"))
            return (false, "la consulta contiene operaciones no permitidas.");

        var tablasReferenciadas = ExtraerTablas(sql);
        var autorizadas = new HashSet<string>(
            conexion.TablasAutorizadas.Select(t => t.NombreTabla.ToLowerInvariant())
                .Concat(conexion.VistasAutorizadas.Select(v => v.NombreVista.ToLowerInvariant())));

        foreach (var tabla in tablasReferenciadas)
            if (!autorizadas.Contains(tabla))
                return (false, $"el objeto '{tabla}' no está autorizado para consultas.");

        return (true, null);
    }

    private static List<string> ExtraerTablas(string sql)
    {
        // Enfoque sin regex frágil: busca los tokens que siguen a FROM / JOIN / INNER JOIN / LEFT JOIN ...
        var tablas = new List<string>();
        var clausulas = new[] { "FROM", "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN" };

        var texto = sql;
        foreach (var clausula in clausulas)
        {
            var idx = 0;
            while ((idx = texto.IndexOf(clausula, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var despues = texto.Substring(idx + clausula.Length).TrimStart();
                // El nombre de la tabla es el primer token (puede venir como [esq].[tabla], esq.tabla o tabla)
                var finToken = despues.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
                var token = (finToken < 0 ? despues : despues.Substring(0, finToken)).Trim().TrimEnd(',');
                var nombre = LimpiarNombreObjeto(token);
                if (!string.IsNullOrEmpty(nombre))
                    tablas.Add(nombre);
                idx += clausula.Length;
            }
        }

        return tablas.Distinct(StringComparer.OrdinalIgnoreCase).Select(t => t.ToLowerInvariant()).ToList();
    }

    private static string LimpiarNombreObjeto(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return string.Empty;
        // Quita corchetes y toma la última parte de un nombre esquema.tabla
        var partes = token.Split('.')
            .Select(p => p.Trim().TrimStart('[').TrimEnd(']'))
            .Where(p => p.Length > 0);
        var nombre = string.Join(".", partes);
        // Si viene con alias "Cliente c", nos quedamos con la primera palabra real
        if (nombre.Contains(' '))
            nombre = nombre.Substring(0, nombre.IndexOf(' ')).Trim().TrimStart('[').TrimEnd(']');
        // Deja solo caracteres de identificador (letras, dígitos, punto, guion bajo)
        nombre = System.Text.RegularExpressions.Regex.Replace(nombre, @"[^A-Za-z0-9_.]", "");
        return nombre;
    }

    private static bool Contiene(string texto, params string[] terminos)
        => terminos.Any(t => texto.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static string GenerarSqlDesdePregunta(string pregunta, ConexionBaseDatos conexion, bool forzarAgregacion = false, bool forzarRaw = false)
    {
        var normalizada = pregunta.ToLowerInvariant();
        var esConteo = Contiene(normalizada, "cuantos", "cuántos", "cuantas", "cuántas", "contar", "total de", "cantidad de");
        // ETAPA 19.2: detección de intención de agregación. Se busca en la pregunta (palabras clave)
        // y también puede forzarse desde el parámetro "forzarAgregacion" del Planner.
        // forzarRaw = true → SELECT * sin GROUP BY (paso 1: consultar datos).
        var quiereAgregacion = !forzarRaw && (forzarAgregacion || Contiene(normalizada, "indicador", "indicadores", "calcular", "métrica", "métricas", "metrica", "metricas", "conteos", "totales", "agrupado", "agrupar", "resumen"));
        var tablas = conexion.TablasAutorizadas.ToList();
        var vistas = conexion.VistasAutorizadas.ToList();

        var candidata = tablas.Cast<object>()
            .Concat(vistas.Cast<object>())
            .Select(t =>
            {
                var nombre = t is TablaAutorizada tb ? tb.NombreTabla : ((VistaAutorizada)t).NombreVista;
                return new { Nombre = nombre, Score = CalcularCoincidencia(normalizada, nombre) };
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault(x => x.Score > 0);

        if (candidata == null) return null;

        // Detectar filtro de estado en la pregunta.
        string? filtroEstado = null;
        if (Contiene(normalizada, "inactivo", "inactivos", "dado de baja", "de baja", "fuera de servicio", "desactivado", "desactivados"))
            filtroEstado = "INACTIVO";
        else if (Contiene(normalizada, "en estado activo", "en estado activos", "estado activo", "estado activos", "activos operativos", "activo operativo", "dados de alta", "de alta"))
            filtroEstado = "ACTIVO";

        var whereEstado = filtroEstado != null ? $" WHERE Estado = '{filtroEstado}'" : string.Empty;

        // Si pide agregación (indicadores, métricas, totales), generar GROUP BY automático.
        if (quiereAgregacion)
        {
            // Verificar si la tabla tiene columna Estado (solo agrupar por Estado si existe)
            var tieneEstado = TieneColumnaEstado(candidata.Nombre);
            
            // Detectar si hay campo numérico para sumar (Precio/Valor/Monto/Costo).
            var pideSuma = Contiene(normalizada, "valor", "precio", "suma", "monto", "total", "valuado", "costo");
            
            if (tieneEstado)
            {
                if (pideSuma)
                    return $"SELECT Estado, COUNT(*) AS Cantidad, SUM(Precio) AS ValorTotal FROM [{candidata.Nombre}]{whereEstado} GROUP BY Estado ORDER BY Estado;";
                return $"SELECT Estado, COUNT(*) AS Cantidad FROM [{candidata.Nombre}]{whereEstado} GROUP BY Estado ORDER BY Estado;";
            }
            
            // Sin columna Estado: solo conteo simple
            if (pideSuma)
                return $"SELECT COUNT(*) AS Cantidad, SUM(Precio) AS ValorTotal FROM [{candidata.Nombre}]{whereEstado};";
            return $"SELECT COUNT(*) AS Cantidad FROM [{candidata.Nombre}]{whereEstado};";
        }

        if (esConteo)
        {
            var pideValor = Contiene(normalizada, "valor", "precio", "suma", "monto", "total", "valuado", "costo");
            if (pideValor)
                return $"SELECT COUNT(*) AS Cantidad, SUM(Precio) AS ValorTotal FROM [{candidata.Nombre}]{whereEstado};";
            return $"SELECT COUNT(*) AS Total FROM [{candidata.Nombre}]{whereEstado};";
        }

        return $"SELECT TOP 20 * FROM [{candidata.Nombre}]{whereEstado};";
    }

    private static int CalcularCoincidencia(string pregunta, string nombre)
    {
        var p = QuitarAcentos(pregunta);
        var n = QuitarAcentos(nombre.ToLowerInvariant());
        if (p.Contains(n)) return 3;
        // Partir camelCase/PascalCase/snake en palabras: "OrdenesCompra" -> ordenes + compra.
        // Sin esto, "lista las órdenes de compra" nunca matcheaba (buscaba "ordenescompra" pegado).
        var palabras = PartirEnPalabras(nombre);
        return palabras.Count(pal => p.Contains(pal));
    }

    private static List<string> PartirEnPalabras(string nombre)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in nombre)
        {
            if (c == '_' || c == ' ' || c == '-')
            {
                sb.Append(' ');
                continue;
            }
            if (char.IsUpper(c) && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                sb.Append(' ');
            sb.Append(c);
        }
        return QuitarAcentos(sb.ToString().ToLowerInvariant())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToList();
    }

    private static string QuitarAcentos(string texto)
        => new string(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());

    /// <summary>Verifica si una tabla tiene columna Estado (para GROUP BY).</summary>
    private static bool TieneColumnaEstado(string nombreTabla)
    {
        // Solo tablas confirmadas con columna Estado
        var tablasConEstado = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Activos", "Usuarios", "MovimientosActivos", "HistorialActivos", 
            "Documento", "Asistente"
        };
        return tablasConEstado.Contains(nombreTabla);
    }

    private static string GenerarResumen(List<Dictionary<string, object?>> datos, List<string> columnas)
    {
        if (datos.Count == 0) return "La consulta no devolvió registros.";
        var lineas = datos.Take(20).Select(fila =>
            string.Join(" | ", columnas.Select(c => $"{c}: {Formatear(fila.TryGetValue(c, out var v) ? v : null)}")));
        return string.Join("\n", lineas);
    }

    private static string Formatear(object? valor)
        => valor switch
        {
            null => "NULL",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
            _ => valor.ToString()
        };

    private static string ObtenerString(Dictionary<string, object?> parametros, string clave, string fallback)
    {
        if (parametros.TryGetValue(clave, out var valor) && valor != null)
            return valor.ToString() ?? fallback;
        return fallback;
    }
}
