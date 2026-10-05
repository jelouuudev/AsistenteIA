using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta que obtiene información autorizada desde SQL Server.
/// Generación SEMÁNTICA de SQL vía LLM con esquema real (tablas/columnas/valores
/// observados en BD). Sin listas fijas de palabras clave de intención.
/// </summary>
public class SqlQueryTool : ITool
{
    private readonly IConexionBaseDatosRepository _conexionRepository;
    private readonly ITablaAutorizadaRepository _tablaRepository;
    private readonly IVistaAutorizadaRepository _vistaRepository;
    private readonly IConexionCifrador _cifrador;
    private readonly ISqlQueryExecutor _executor;
    private readonly ILogger<SqlQueryTool> _logger;
    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly IOllamaService? _ollama;

    public SqlQueryTool(
        IConexionBaseDatosRepository conexionRepository,
        ITablaAutorizadaRepository tablaRepository,
        IVistaAutorizadaRepository vistaRepository,
        IConexionCifrador cifrador,
        ISqlQueryExecutor executor,
        ILogger<SqlQueryTool> logger,
        IOllamaService? ollama = null,
        IEmbeddingProvider? embeddingProvider = null)
    {
        _conexionRepository = conexionRepository;
        _tablaRepository = tablaRepository;
        _vistaRepository = vistaRepository;
        _cifrador = cifrador;
        _executor = executor;
        _logger = logger;
        _ollama = ollama;
        _embeddingProvider = embeddingProvider;
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
        // Sugerencia del Planner (plan #9130): la tabla que el plan ya resolvió
        // (por valor observado o esquema). Se VALIDA contra las autorizadas: si no
        // está autorizada se ignora y decide la herramienta. Permite un paso SQL
        // por tabla en preguntas multi-intención sin que cada llamada re-adivine.
        var tablaSugerida = ObtenerString(request.Parametros, "tabla", string.Empty);
        // Contrato estructural con el Planner (sin keywords): "CONSULTA" trae filas
        // filtradas, "ANALISIS" agrega sobre lo consultado, "AUTO" lo decide el LLM.
        // Se aceptan los flags legacy forzarAgregacion/forzarRaw como compatibilidad.
        var modo = ObtenerString(request.Parametros, "modo", string.Empty).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(modo))
        {
            var forzarAgregacionLegacy = request.Parametros.TryGetValue("forzarAgregacion", out var fa) && fa is true;
            var forzarRawLegacy = request.Parametros.TryGetValue("forzarRaw", out var fr) && fr is true;
            modo = forzarRawLegacy ? "CONSULTA" : forzarAgregacionLegacy ? "ANALISIS" : "AUTO";
        }

        if (string.IsNullOrWhiteSpace(pregunta) && string.IsNullOrWhiteSpace(sqlSugerido))
            return new ToolExecutionResult { Exitoso = false, Error = "No se proporcionó una pregunta ni una consulta SQL." };

        var conexionesActivas = (await _conexionRepository.GetActivasAsync()).ToList();
        if (conexionesActivas.Count == 0)
            return new ToolExecutionResult { Exitoso = false, Error = "No hay conexiones a bases de datos configuradas." };

        // Selección de conexión por SIGNIFICADO: la conexión cuyo catálogo (tablas
        // autorizadas) se parece más a la pregunta. Antes comparaba el texto de la
        // pregunta contra el NOMBRE de cada tabla por substring, lo que no identifica
        // una base de datos ni una nueva: preguntar por "los ingresos del mes" no
        // encuentra la base `contabilidad` porque la palabra no está en ningún nombre.
        // Sin embeddings se usa la primera conexión activa y el LLM trabaja con el
        // esquema completo.
        ConexionBaseDatos? conexion = null;
        if (!string.IsNullOrWhiteSpace(pregunta))
            conexion = await SeleccionarConexionAsync(pregunta, conexionesActivas, cancellationToken, tablaSugerida);
        conexion ??= conexionesActivas.First();

        // Vía rápida semántica (sin LLM en el caso común): esquema + valores
        // observados. El LLM solo se usa si no se encuentra ningún filtro.
        var plan = string.IsNullOrWhiteSpace(sqlSugerido)
            ? await GenerarSqlSemanticoAsync(pregunta, conexion, modo, cancellationToken, tablaSugerida)
            : new PlanSql(sqlSugerido, null, sqlSugerido.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        if ((plan is null || string.IsNullOrWhiteSpace(plan.Sql)) && !string.IsNullOrWhiteSpace(request.PreguntaOriginal) && request.PreguntaOriginal != pregunta)
            plan = await GenerarSqlSemanticoAsync(request.PreguntaOriginal, conexion, modo, cancellationToken, tablaSugerida);

        var sql = plan?.Sql;
        if (string.IsNullOrWhiteSpace(sql))
            return new ToolExecutionResult { Exitoso = false, Error = "No se pudo determinar una consulta válida para la pregunta." };

        // Si la consulta sugerida por el modelo no referencia ninguna tabla autorizada,
        // pero tenemos la pregunta original, regeneramos a partir de ella.
        var validacionInicial = ValidarAutorizacion(sql, conexion);
        if (!validacionInicial.Autorizado && !string.IsNullOrWhiteSpace(pregunta))
        {
            var sqlDesdePregunta = await GenerarSqlSemanticoAsync(pregunta, conexion, modo, cancellationToken, tablaSugerida);
            if (sqlDesdePregunta != null)
            {
                plan = sqlDesdePregunta;
                sql = plan.Sql;
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

        // Agregación acompañante (misma tabla y WHERE): filas agrupadas si se desglosa
        // por la dimensión filtrada, o una fila de total. Las cifras las calcula SQL,
        // no el LLM.
        List<Dictionary<string, object?>>? filasAgregado = null;
        string? columnaGrupo = plan?.ColumnaGrupo;
        if (plan?.ConteoSql != null && ValidarAutorizacion(plan.ConteoSql, conexion).Autorizado)
        {
            try
            {
                _logger.LogInformation("SqlQueryTool SQL agregación: {Sql}", plan.ConteoSql);
                filasAgregado = (await _executor.ExecuteReadOnlyAsync(cadena, plan.ConteoSql, null, 100, cancellationToken)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SqlQueryTool: agregación acompañante falló; se entregan solo las filas.");
            }
        }

        var tablas = ExtraerTablas(sql);
        var origen = tablas.Count > 0 ? tablas[0] : "la base de datos";
        _logger.LogInformation("SqlQueryTool ejecutó consulta en {Ms}ms con {N} registros.", tiempoMs, datos.Count);

        var contenido = ConstruirContenido(origen, conexion, plan, filasAgregado, columnaGrupo, resumen);

        return new ToolExecutionResult
        {
            Exitoso = true,
            Contenido = contenido,
            Metadatos = new()
            {
                ["sql"] = sql,
                ["sqlAgregacion"] = plan?.ConteoSql,
                ["columnaGrupo"] = columnaGrupo,
                ["cantidadRegistros"] = datos.Count,
                ["columnas"] = string.Join(", ", columnas),
                ["tiempoMs"] = tiempoMs,
                ["conexion"] = conexion.Nombre
            }
        };
    }

    /// <summary>
    /// Presenta los resultados. Si la agregación trae una columna Grupo, imprime el
    /// desglose por grupo con sus cifras ya calculadas; el agente solo tiene que
    /// presentarlas, no sumarlas. La etiqueta del importe es la real: "importe total"
    /// solo si la tabla tiene cantidad y precio.
    /// </summary>
    private static string ConstruirContenido(
        string origen, ConexionBaseDatos conexion, PlanSql? plan,
        List<Dictionary<string, object?>>? filasAgregado, string? columnaGrupo, string resumen)
    {
        var encabezado = $"Datos obtenidos de la tabla '{origen}' (base de datos '{conexion.BaseDatos}')";
        // Eco auditable del filtro aplicado (extraído del SQL propio generado, no de
        // palabras de la pregunta): el consumidor ve QUÉ condición se aplicó (#9062
        // mostraba un desglose sin decir que filtraba por precio).
        var filtroEco = plan?.TieneFiltro == true ? ExtraerWhereParaMostrar(plan.Sql) : null;
        var conFiltro = string.IsNullOrWhiteSpace(filtroEco) ? string.Empty : $" Filtro aplicado: {filtroEco}.";

        if (filasAgregado == null || filasAgregado.Count == 0)
            return $"{encabezado}:{conFiltro}\n{resumen}";

        if (columnaGrupo == null)
        {
            var fila = filasAgregado[0];
            var total = LeerColumna(fila, "Total") ?? "?";
            var unidades = LeerColumna(fila, "Unidades");
            var valor = LeerColumna(fila, "ValorTotal");
            var extra = new List<string>();
            if (!string.IsNullOrWhiteSpace(unidades)) extra.Add($"{unidades} unidades");
            if (!string.IsNullOrWhiteSpace(valor)) extra.Add($"{plan?.EtiquetaMetrica} {valor}");
            var sufijo = extra.Count > 0 ? " (" + string.Join(", ", extra) + ")" : "";
            return $"{encabezado}. {(plan?.TieneFiltro == true ? "Total filtrado" : "Total")}: {total}{sufijo}.{conFiltro}\n{resumen}";
        }

        // Desglose por la dimensión comparada. Una línea por grupo, cifras de SQL.
        var lineas = new List<string>();
        foreach (var fila in filasAgregado.Take(20))
        {
            var grupo = LeerColumna(fila, "Grupo") ?? "?";
            var partes = new List<string>();
            var total = LeerColumna(fila, "Total");
            if (!string.IsNullOrWhiteSpace(total)) partes.Add($"{total} registros");
            var unidades = LeerColumna(fila, "Unidades");
            if (!string.IsNullOrWhiteSpace(unidades)) partes.Add($"{unidades} unidades");
            var valor = LeerColumna(fila, "ValorTotal");
            // Etiqueta real de la métrica: "importe total" solo si la tabla tiene
            // cantidad y precio. Con una sola columna decimal NO se presenta un número
            // sin significado como si fuera dinero.
            if (!string.IsNullOrWhiteSpace(valor)) partes.Add($"{plan?.EtiquetaMetrica} {valor}");
            lineas.Add($"- {columnaGrupo} = {grupo}: {string.Join(", ", partes)}");
        }
        if (filasAgregado.Count > 20)
            lineas.Add($"… ({filasAgregado.Count - 20} grupo(s) más, {filasAgregado.Count} en total).");

        return $"{encabezado}. Desglose por {columnaGrupo} (cifras calculadas en SQL, no recalcular).{conFiltro}\n" +
               string.Join("\n", lineas);
    }

    /// <summary>
    /// Extrae la cláusula WHERE del SQL propio generado para mostrarla como eco
    /// auditable (corta GROUP BY / ORDER BY). Estructural sobre texto controlado.
    /// </summary>
    internal static string? ExtraerWhereParaMostrar(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return null;
        var idx = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var resto = sql[idx..].TrimEnd().TrimEnd(';').Trim();
        foreach (var corte in new[] { " GROUP BY ", " ORDER BY " })
        {
            var i = resto.IndexOf(corte, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) resto = resto[..i];
        }
        return string.IsNullOrWhiteSpace(resto) ? null : resto.Trim();
    }

    private static (bool Autorizado, string? Motivo) ValidarAutorizacion(string sql, ConexionBaseDatos conexion)
    {
        // Validación ESTRUCTURAL (tokenizador + gramática), no lista de palabras
        // prohibidas: el filtro por substring rechazaba consultas legítimas que tuvieran
        // una columna "LastUpdate" o "IsDeleted", y la gramática no depende del dominio.
        var seguridad = AnalizadorSql.ValidarSoloLectura(sql);
        if (!seguridad.Seguro) return (false, seguridad.Motivo);

        var autorizadas = new HashSet<string>(
            conexion.TablasAutorizadas.Select(t => t.NombreTabla.ToLowerInvariant())
                .Concat(conexion.VistasAutorizadas.Select(v => v.NombreVista.ToLowerInvariant())));

        foreach (var objeto in AnalizadorSql.ObjetosReferenciados(sql))
        {
            if (string.IsNullOrWhiteSpace(objeto)) continue;
            if (!autorizadas.Contains(objeto.Trim('[', ']', '"')))
                return (false, $"el objeto '{objeto}' no está autorizado para consultas.");
        }

        return (true, null);
    }

    private static List<string> ExtraerTablas(string sql) => AnalizadorSql.ObjetosReferenciados(sql);

    /// <summary>
    /// Objetos del SQL para nombrar el origen del resultado. Delega en el analizador
    /// estructural (tokenización) en lugar de buscar la subcadena "FROM": así un literal
    /// de texto o una columna llamada "from" no se confunden con una cláusula.
    /// </summary>
    /// <summary>Plan SQL: consulta principal + conteo acompañante opcional (mismo WHERE).</summary>
    private sealed record PlanSql(
        string Sql, string? ConteoSql, bool TieneFiltro, string? ColumnaGrupo = null,
        string EtiquetaMetrica = "importe total");

    /// <summary>
    /// Perfil métrico de una tabla, derivado SOLO de metadatos (tipos y claves del
    /// catálogo), nunca de nombres: la columna de cantidad (entera, no clave) y la de
    /// precio unitario (decimal). Sin la primera, la "medida" es la suma de una columna
    /// decimal, que no es necesariamente un importe (en `ventas` es PrecioUnitario y su
    /// suma no es la venta).
    /// </summary>
    internal sealed record PerfilMetrica(string? Cantidad, string? Medida)
    {
        /// <summary>Expresión de importe: Σ(cantidad × precio) si hay ambas columnas.</summary>
        public bool HayImporte => !string.IsNullOrWhiteSpace(Cantidad) && !string.IsNullOrWhiteSpace(Medida);

        /// <summary>Expresión SQL agregable, o null si la tabla no tiene nada que sumar.</summary>
        public string? ExpresionImporte
        {
            get
            {
                if (HayImporte) return $"SUM([{Cantidad}] * [{Medida}])";
                if (!string.IsNullOrWhiteSpace(Medida)) return $"SUM([{Medida}])";
                return null;
            }
        }

        /// <summary>Etiqueta honesta de la métrica: un importe solo si hay cantidad×precio.</summary>
        public string Etiqueta => HayImporte ? "importe total" : "suma de la columna decimal";

        public string? ExpresionCantidad
            => string.IsNullOrWhiteSpace(Cantidad) ? null : $"SUM([{Cantidad}])";
    }

    /// <summary>
    /// Selección de conexión: sugerencia del Planner primero (validada), luego
    /// valor observado, luego similitud con el catálogo, luego LLM. Sin vocabulario fijo.
    /// </summary>
    private async Task<ConexionBaseDatos?> SeleccionarConexionAsync(
        string pregunta, List<ConexionBaseDatos> conexiones, CancellationToken ct, string? tablaSugerida = null)
    {
        if (conexiones.Count == 1) return conexiones[0];

        // La tabla sugerida manda si alguna conexión la tiene autorizada: el plan ya
        // resolvió a qué base pertenece (evita que la similitud elija otra base por
        // el resto de la frase en preguntas mixtas).
        if (!string.IsNullOrWhiteSpace(tablaSugerida))
        {
            var duena = conexiones.FirstOrDefault(c =>
                c.TablasAutorizadas.Any(t => t.NombreTabla.Equals(tablaSugerida, StringComparison.OrdinalIgnoreCase))
                || c.VistasAutorizadas.Any(v => v.NombreVista.Equals(tablaSugerida, StringComparison.OrdinalIgnoreCase)));
            if (duena != null)
            {
                _logger.LogInformation("SqlQueryTool: conexión '{Conexion}' elegida por tabla sugerida '{Tabla}'.",
                    duena.Nombre, tablaSugerida);
                return duena;
            }
            _logger.LogWarning("SqlQueryTool: tabla sugerida '{Tabla}' no autorizada en ninguna conexión; se ignora.",
                tablaSugerida);
        }

        // Señal de valor observado, antes que la similitud (plan #9115): si la
        // pregunta contiene un valor REAL de alguna tabla ("productos de marca Dell"
        // → ControlActivosTest.Activos.Marca='Dell'), esa conexión es la que
        // responde. Sin esto la similitud elegía VentasTest por el resto de la
        // frase y la consulta salía de la base equivocada. Los valores salen de un
        // DISTINCT real: no hay vocabulario fijo.
        if (_embeddingProvider != null)
        {
            foreach (var c in conexiones)
            {
                try
                {
                    var mapa = await ObtenerValoresMapaAsync(c, ct);
                    if (mapa.Count == 0) continue;
                    var exactos = new List<ValorDetectado>();
                    var plurales = new List<ValorDetectado>();
                    FiltroSemantico.Detectar(pregunta, mapa, exactos, plurales);
                    var columnas = FiltroSemantico.ElegirPorColumna(exactos, plurales)
                        .SelectMany(kv => kv.Value)
                        .Select(v => v.Clave.Contains('.') ? v.Clave[..v.Clave.IndexOf('.')] : v.Clave)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (columnas.Count == 0) continue;
                    _logger.LogInformation("SqlQueryTool: conexión '{Conexion}' elegida por valor observado (tabla(s) {Tablas}).",
                        c.Nombre, string.Join(",", columnas));
                    return c;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SqlQueryTool: no se pudo evaluar la conexión '{Conexion}' por valores observados.", c.Nombre);
                }
            }
        }

        // Vía rápida por SIGNIFICADO: se compara la pregunta con el catálogo de cada
        // conexión (tablas + columnas), no con los nombres de tabla por substring.
        if (_embeddingProvider != null)
        {
            try
            {
                var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);
                var mejor = (Conexion: (ConexionBaseDatos?)null, Sim: double.NegativeInfinity);
                foreach (var c in conexiones)
                {
                    var catalogo = await CatalogoConexionAsync(c, ct);
                    var sim = SeleccionHerramientaSemantica.Coseno(embPregunta, await EmbeddingCacheadoAsync(catalogo, ct));
                    if (sim > mejor.Sim) mejor = (c, sim);
                }
                if (mejor.Conexion is not null)
                {
                    _logger.LogInformation("SqlQueryTool: conexión '{Conexion}' elegida con similitud {Sim:F3}.",
                        mejor.Conexion.Nombre, mejor.Sim);
                    return mejor.Conexion;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SqlQueryTool: selección de conexión por similitud falló; se usa la primera activa.");
            }
        }

        if (conexiones.Count == 1) return conexiones[0];

        // Sin embeddings: desempate por LLM con timeout corto (el del paso es 30s).
        try
        {
            if (_ollama != null)
            {
                var catalogo = string.Join("; ", conexiones.Select(c =>
                    $"{c.Nombre}: [{string.Join(", ", c.TablasAutorizadas.Select(t => t.NombreTabla))}]"));
                var historial = new List<Mensaje>
                {
                    new Mensaje
                    {
                        Rol = RolMensaje.User,
                        Contenido = "Dadas estas conexiones y sus tablas autorizadas: [" + catalogo + "]. " +
                            "¿Cuál responde mejor por su SIGNIFICADO a la solicitud? Responde SOLO este JSON, sin explicaciones: " +
                            "{\"nombre\":\"...\"}. Solicitud: " + pregunta
                    }
                };
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                var respuesta = await _ollama.SendMessageAsync(historial, null, null, 0.0, 200, cts.Token);
                if (!string.IsNullOrWhiteSpace(respuesta))
                {
                    var inicio = respuesta.IndexOf('{');
                    var fin = respuesta.LastIndexOf('}');
                    if (inicio >= 0 && fin > inicio)
                    {
                        var json = respuesta[inicio..(fin + 1)];
                        var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (doc != null && doc.TryGetValue("nombre", out var nombre) && !string.IsNullOrWhiteSpace(nombre))
                        {
                            var match = conexiones.FirstOrDefault(c =>
                                c.Nombre.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (match != null) return match;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SqlQueryTool: desempate de conexión por LLM falló; usando primera.");
        }

        return conexiones.First();
    }

    /// <summary>
    /// Texto que describe el contenido de una conexión: sus tablas y las columnas de
    /// cada una. Es lo que se compara con la pregunta para decidir a qué base preguntar.
    /// Viene del catálogo, así que una base nueva se describe sola.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, string Texto)> _cacheCatalogoConexion
        = new(StringComparer.OrdinalIgnoreCase);

    private async Task<string> CatalogoConexionAsync(ConexionBaseDatos conexion, CancellationToken ct)
    {
        var tablas = conexion.TablasAutorizadas.Select(t => t.NombreTabla)
            .Concat(conexion.VistasAutorizadas.Select(v => v.NombreVista))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tablas.Count == 0) return conexion.Nombre;

        var clave = $"cat{conexion.IdConexion}:{conexion.BaseDatos}";
        if (_cacheCatalogoConexion.TryGetValue(clave, out var hit) && hit.Expira > DateTime.UtcNow)
            return hit.Texto;

        var columnas = await ObtenerColumnasPorTablaAsync(conexion, tablas, ct);
        var partes = new List<string> { conexion.Nombre };
        foreach (var t in tablas)
        {
            partes.Add(columnas.TryGetValue(t, out var cols) && cols.Count > 0
                ? t + " " + string.Join(" ", cols)
                : t);
        }
        var texto = string.Join("; ", partes);
        _cacheCatalogoConexion[clave] = (DateTime.UtcNow.AddMinutes(5), texto);
        return texto;
    }

    /// <summary>
    /// Generación de SQL: vía rápida primero (esquema + valores observados, sin LLM,
    /// completa en segundos dentro del timeout del paso). El LLM solo se usa si la
    /// vía rápida no encuentra ningún filtro.
    /// </summary>
    private async Task<PlanSql?> GenerarSqlSemanticoAsync(
        string pregunta, ConexionBaseDatos conexion, string modo, CancellationToken ct, string? tablaSugerida = null)
    {
        var tabla = await ElegirTablaAsync(conexion, pregunta, ct, tablaSugerida);
        if (string.IsNullOrWhiteSpace(tabla)) return null; // sin tablas autorizadas
        // Esquema + valores + perfil métrico en paralelo (consultas pequeñas de lectura).
        var tareaEsquema = DescribirEsquemaAsync(conexion, ct);
        var tareaMapa = ObtenerValoresMapaAsync(conexion, ct);
        var tareaPerfil = ObtenerPerfilMetricaAsync(conexion, tabla, ct);
        await Task.WhenAll(tareaEsquema, tareaMapa, tareaPerfil);
        var esquema = await tareaEsquema;
        var mapa = await tareaMapa;
        var perfil = await tareaPerfil;
        var valores = FormatearValores(mapa);

        // Vía rápida: filtros desde valores observados (palabra completa o plural).
        var rapido = FallbackEstructurado(pregunta, conexion, tabla, mapa, modo, perfil);

        // Comparación numérica determinística + semántica (plan #7060, sin keywords):
        // la pregunta trae dígitos estructurales y la tabla columnas numéricas por
        // metadatos (Medida/Cantidad). Se construye WHERE numérico combinado con los
        // filtros de texto (AND), sin listas de vocabulario:
        //   columna = metadatos (única disponible) o similitud semántica si hay varias;
        //   operador = símbolos en texto (>,<,=) o similitud/LLM genérico, nunca
        //   substring de dominio. No depende de que el LLM grande responda a tiempo.
        var filtroNumerico = await ConstruirFiltroNumericoAsync(pregunta, conexion, tabla, perfil, ct);
        if (filtroNumerico != null)
        {
            var filtrosTexto = ExtraerFiltrosPorValores(pregunta, mapa, tabla);
            var partes = filtrosTexto.Select(f => f.Condicion).ToList();
            partes.Add(filtroNumerico.Condicion);
            var whereCombinado = " WHERE " + string.Join(" AND ", partes);
            var columnaGrupoCombinado = filtrosTexto.Count > 0 ? filtrosTexto[0].Columna : null;
            if (modo == "ANALISIS")
                return new PlanSql(AggregateSql(tabla, whereCombinado, perfil, columnaGrupoCombinado), null, true, columnaGrupoCombinado, perfil.Etiqueta);
            return new PlanSql(
                $"SELECT TOP 10 * FROM [{tabla}]{whereCombinado};",
                AggregateSql(tabla, whereCombinado, perfil, columnaGrupoCombinado), true, columnaGrupoCombinado, perfil.Etiqueta);
        }

        // Comparación numérica vía LLM grande (respaldo para BETWEEN u operadores
        // complejos). Solo si la vía rápida encontró texto y hay dígitos + numéricas.
        // El camino determinístico de arriba ya cubrió el caso común (#7060).
        if (rapido != null && _ollama != null && !string.IsNullOrWhiteSpace(pregunta)
            && TieneDigitos(pregunta) && (perfil.Medida != null || perfil.Cantidad != null))
        {
            try
            {
                var sqlLlmNumerico = await GenerarSqlConLLMAsync(pregunta, conexion, esquema, valores, modo, perfil, ct);
                if (!string.IsNullOrWhiteSpace(sqlLlmNumerico) && ValidarAutorizacion(sqlLlmNumerico, conexion).Autorizado)
                    return new PlanSql(sqlLlmNumerico, null,
                        sqlLlmNumerico.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SqlQueryTool: generación LLM numérica falló; usando vía rápida.");
            }
        }
        if (rapido != null) return rapido;

        // Sin filtros observables: intentar LLM (puede exceder el timeout del paso;
        // ante cualquier fallo se devuelve fila sin filtrar en lugar de error).
        if (_ollama != null && !string.IsNullOrWhiteSpace(pregunta))
        {
            try
            {
                var sqlLlm = await GenerarSqlConLLMAsync(pregunta, conexion, esquema, valores, modo, perfil, ct);
                if (!string.IsNullOrWhiteSpace(sqlLlm) && ValidarAutorizacion(sqlLlm, conexion).Autorizado)
                    return new PlanSql(sqlLlm, null,
                        sqlLlm.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SqlQueryTool: generación LLM falló; usando consulta sin filtrar.");
            }
        }

        // Sin filtro: se agrega sobre toda la tabla. Sin dimensión conocida no se
        // desglosa (no hay columna que agrupar sin inventar una).
        return modo == "ANALISIS"
            ? new PlanSql(AggregateSql(tabla, null, perfil, null), null, false, null, perfil.Etiqueta)
            : new PlanSql($"SELECT TOP 10 * FROM [{tabla}];", AggregateSql(tabla, null, perfil, null), false, null, perfil.Etiqueta);
    }

    /// <summary>
    /// Perfil métrico de la tabla por METADATOS (tipos + claves), sin listas de nombres:
    ///   Medida   = 1ª decimal/numeric/money/float/real por orden ordinal.
    ///   Cantidad = 1ª entera (int/bigint/smallint/tinyint) por orden ordinal que NO sea
    ///              clave primaria ni foránea (los Id no se suman como cantidad).
    /// Con ambas, el importe es Σ(cantidad × precio), que es lo que el usuario entiende
    /// por "monto total"; con una sola, la suma es de una columna decimal y se etiqueta
    /// como tal para no presentar un número sin significado como si fuera dinero.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, PerfilMetrica Perfil)> _cachePerfil
        = new(StringComparer.OrdinalIgnoreCase);

    private async Task<PerfilMetrica> ObtenerPerfilMetricaAsync(ConexionBaseDatos conexion, string tabla, CancellationToken ct)
    {
        var clave = $"perf{conexion.IdConexion}:{tabla}";
        if (_cachePerfil.TryGetValue(clave, out var cached) && cached.Expira > DateTime.UtcNow)
            return cached.Perfil;
        var vacio = new PerfilMetrica(null, null);
        try
        {
            var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
            // Una sola consulta: numéricas, enteras y el conjunto de claves de la tabla.
            const string sql = """
                SELECT c.COLUMN_NAME AS Col, c.DATA_TYPE AS Tipo,
                       CASE WHEN EXISTS (
                              SELECT 1 FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
                              WHERE k.TABLE_NAME = c.TABLE_NAME AND k.COLUMN_NAME = c.COLUMN_NAME
                                AND k.CONSTRAINT_NAME IN (
                                      SELECT ku.CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                      JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                                        ON ku.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                                       AND ku.TABLE_NAME = tc.TABLE_NAME
                                      WHERE tc.TABLE_NAME = c.TABLE_NAME
                                        AND tc.CONSTRAINT_TYPE IN ('PRIMARY KEY','FOREIGN KEY'))
                            ) THEN 1 ELSE 0 END AS EsClave
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = @t
                ORDER BY c.ORDINAL_POSITION
                """;
            var filas = (await _executor.ExecuteReadOnlyAsync(
                cadena, sql, new Dictionary<string, object?> { ["t"] = tabla }, 100, ct)).ToList();

            PerfilMetrica? perfil = null;
            string? medida = null, cantidad = null;
            foreach (var fila in filas)
            {
                var col = LeerColumna(fila, "Col");
                var tipo = (LeerColumna(fila, "Tipo") ?? "").ToLowerInvariant();
                var esClave = LeerColumna(fila, "EsClave") == "1";
                if (string.IsNullOrWhiteSpace(col)) continue;

                if (medida is null && EsNumerica(tipo)) medida = col;
                else if (cantidad is null && EsEntera(tipo) && !esClave) cantidad = col;

                if (medida is not null && cantidad is not null) break;
            }
            perfil = new PerfilMetrica(cantidad, medida);

            var resultado = perfil;
            _cachePerfil[clave] = (DateTime.UtcNow.AddMinutes(30), resultado);
            return resultado;
        }
        catch { return vacio; }
    }

    private static bool EsNumerica(string tipo)
        => tipo is "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real";

    private static bool EsEntera(string tipo)
        => tipo is "int" or "bigint" or "smallint" or "tinyint";

    /// <summary>¿La pregunta menciona cifras? Estructural (dígitos), sin vocabulario.</summary>
    private static bool TieneDigitos(string texto)
    {
        foreach (var c in texto)
            if (char.IsDigit(c)) return true;
        return false;
    }

    private static string? Limpiar(string? col) => string.IsNullOrWhiteSpace(col) ? null : col;

    /// <summary>
    /// Agregación determinista. Si hay columna de grupo (la misma que se filtró), desglosa
    /// por ella: es la dimensión que el usuario está comparando y evita que el LLM tenga
    /// que sumar a mano. Devuelve una fila por grupo con Total, Unidades e Importe.
    /// </summary>
    private static string AggregateSql(string tabla, string? where, PerfilMetrica perfil, string? agruparPor)
    {
        var tabSafe = "[" + tabla.Replace("]", "") + "]";
        var partes = new List<string> { "COUNT(*) AS Total" };
        var unidades = perfil.ExpresionCantidad;
        if (unidades != null) partes.Add($"{unidades} AS Unidades");
        var importe = perfil.ExpresionImporte;
        if (importe != null) partes.Add($"{importe} AS ValorTotal");

        var grupo = Limpiar(agruparPor);
        if (grupo == null)
            return $"SELECT {string.Join(", ", partes)} FROM {tabSafe}{where};";

        var colSafe = "[" + grupo.Replace("]", "") + "]";
        return $"SELECT {colSafe} AS Grupo, {string.Join(", ", partes)} " +
               $"FROM {tabSafe}{where} GROUP BY {colSafe} ORDER BY {colSafe};";
    }

    private async Task<string?> GenerarSqlConLLMAsync(
        string pregunta, ConexionBaseDatos conexion, string esquema, string valores, string modo, PerfilMetrica perfil, CancellationToken ct)
    {
        var tablas = string.Join(", ", conexion.TablasAutorizadas.Select(t => t.NombreTabla)
            .Concat(conexion.VistasAutorizadas.Select(v => v.NombreVista)).Distinct(StringComparer.OrdinalIgnoreCase));
        var rol = modo == "CONSULTA"
            ? "Devuelve filas filtradas (SELECT TOP 20 * ... WHERE ...) o COUNT(*) filtrado si piden una cantidad."
            : modo == "ANALISIS"
                ? "Devuelve una agregación coherente con el filtro de la pregunta (COUNT, con WHERE). Solo agrupa (GROUP BY) si piden desglose explícito."
                : "Elige entre filas filtradas o COUNT filtrado según el significado de la pregunta.";
        // Métrica real de la tabla. Sin esto el modelo suma la columna decimal suelta
        // (PrecioUnitario) y presents un número que no es el importe.
        var metricas = perfil switch
        {
            { HayImporte: true } => $"unidades: SUM([{perfil.Cantidad}]); importe: {perfil.ExpresionImporte} " +
                                    $"(Σ cantidad × [{perfil.Medida}]). ÚSALOS para totales y montos.",
            { ExpresionImporte: not null } => $"importe: {perfil.ExpresionImporte} " +
                                    "(es la SUMA de una columna decimal, NO un monto de venta: no lo presentes como dinero).",
            _ => "la tabla no tiene columnas numéricas sumables: solo COUNT(*)."
        };
        var historial = new List<Mensaje>
        {
            new Mensaje
            {
                Rol = RolMensaje.User,
                Contenido = "Genera UNA consulta SQL Server de solo lectura para responder por SIGNIFICADO. " +
                    "Responde SOLO con el SQL, sin explicaciones ni markdown. " +
                    "Reglas: solo SELECT; solo tablas autorizadas [" + tablas + "]; " +
                    "esquema: " + esquema + ". " +
                    "Valores reales observados (usa SOLO estos literales en WHERE, con LIKE): " + valores + ". " +
                    "Métricas de la tabla: " + metricas + " " +
                    "Nunca agrupes por una columna distinta al filtro (ej. no agrupes por Estado si filtran por Categoria), " +
                    "salvo desglose explícito. Rol del paso: " + rol + " " +
                    "Pregunta: " + pregunta
            }
        };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // El sub-presupuesto del LLM debe CABER dentro del presupuesto de la herramienta
        // (ToolOrchestrator: TiempoMaximoEjecucionMs, 120s por defecto) dejando tiempo
        // para ejecutar el SQL. En CPU el reasoning tarda minutos: con 15s las
        // comparaciones numéricas ("precio supera los 1000", plan #7059) caían al
        // fallback de texto antes de que Ollama respondiera. 60s le da margen real;
        // si igual falla, se cae al SQL determinista como antes.
        cts.CancelAfter(TimeSpan.FromSeconds(60));
        var respuesta = await _ollama!.SendMessageAsync(historial, null, null, 0.0, 500, cts.Token);
        if (string.IsNullOrWhiteSpace(respuesta)) return null;

        // El modelo reasoning puede devolver <think>...</think> antes del SQL.
        var finThink = respuesta.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        var util = finThink >= 0 ? respuesta[(finThink + 8)..] : respuesta;
        var idxSelect = util.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        if (idxSelect < 0) return null;
        var sql = util[idxSelect..].Trim().Trim('`').Trim();
        // Corta trailing explicaciones tras el primer ';' + texto.
        var idxEnd = sql.IndexOf(';');
        if (idxEnd >= 0) sql = sql[..(idxEnd + 1)];
        return sql;
    }

    /// <summary>
    /// Esquema real vía INFORMATION_SCHEMA (tablas autorizadas). Sin keywords.
    /// Con caché de 5 min por conexión: las ramas paralelas comparten el mismo esquema
    /// y antes cada una repetía hasta 5 consultas de columnas.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, string Esquema)> _cacheEsquema
        = new(StringComparer.OrdinalIgnoreCase);

    private async Task<string> DescribirEsquemaAsync(ConexionBaseDatos conexion, CancellationToken ct)
    {
        var claveCache = $"esc{conexion.IdConexion}:{string.Join(",", conexion.TablasAutorizadas.Select(t => t.NombreTabla).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))}";
        if (_cacheEsquema.TryGetValue(claveCache, out var hit) && hit.Expira > DateTime.UtcNow && !string.IsNullOrWhiteSpace(hit.Esquema))
            return hit.Esquema;

        var partes = new List<string>();
        try
        {
            var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
            foreach (var t in conexion.TablasAutorizadas.Select(x => x.NombreTabla).Distinct(StringComparer.OrdinalIgnoreCase).Take(5))
            {
                try
                {
                    var filas = (await _executor.ExecuteReadOnlyAsync(
                        cadena,
                        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION",
                        new Dictionary<string, object?> { ["t"] = t }, 50, ct)).ToList();
                    var cols = filas.Select(f => f.Values.FirstOrDefault()?.ToString() ?? "").Where(s => s.Length > 0);
                    partes.Add($"{t}({string.Join(",", cols)})");
                }
                catch { partes.Add(t); }
            }
        }
        catch { return string.Join(", ", conexion.TablasAutorizadas.Select(t => t.NombreTabla)); }
        var esquema = partes.Count > 0 ? string.Join("; ", partes) : "(sin esquema)";
        _cacheEsquema[claveCache] = (DateTime.UtcNow.AddMinutes(5), esquema);
        return esquema;
    }

    /// <summary>
    /// Valores distintos observados en columnas de texto (para grounding del WHERE).
    /// Hasta 3 tablas x 10 columnas x 30 valores, con caché estática de 5 min por
    /// conexión (los DISTINCT no se repiten en cada paso del plan).
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, Dictionary<string, List<string>> Mapa)> _cacheValores
        = new(StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, List<string>>> ObtenerValoresMapaAsync(ConexionBaseDatos conexion, CancellationToken ct)
    {
        var clave = $"cx{conexion.IdConexion}:{string.Join(",", conexion.TablasAutorizadas.Select(t => t.NombreTabla).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))}";
        if (_cacheValores.TryGetValue(clave, out var cached) && cached.Expira > DateTime.UtcNow && cached.Mapa.Count > 0)
            return cached.Mapa;

        var mapa = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
            var tablas = conexion.TablasAutorizadas.Select(x => x.NombreTabla)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();

            // Columnas de texto por tabla EN PARALELO (antes: 3 aperturas secuenciales).
            var tareasColumnas = tablas.Select(async t =>
            {
                try
                {
                    var colsFilas = (await _executor.ExecuteReadOnlyAsync(
                        cadena,
                        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND DATA_TYPE IN ('char','nchar','varchar','nvarchar','text','ntext') ORDER BY ORDINAL_POSITION",
                        new Dictionary<string, object?> { ["t"] = t }, 20, ct)).ToList();
                    return (Tabla: t, Columnas: colsFilas.Select(f => f.Values.FirstOrDefault()?.ToString() ?? "")
                        .Where(s => s.Length > 0).Take(10).ToList());
                }
                catch { return (Tabla: t, Columnas: new List<string>()); }
            }).ToList();

            var infoColumnas = await Task.WhenAll(tareasColumnas);

            // DISTINCT por columna EN PARALELO: eran hasta 30 viajes secuenciales y
            // agotaban el timeout de la herramienta (la rama moría cancelada).
            var tareasValores = infoColumnas
                .SelectMany(info => info.Columnas.Select(async col =>
                {
                    try
                    {
                        var colSafe = "[" + col.Replace("]", "") + "]";
                        var tabSafe = "[" + info.Tabla.Replace("]", "") + "]";
                        var filas = (await _executor.ExecuteReadOnlyAsync(
                            cadena, $"SELECT DISTINCT TOP 30 {colSafe} AS V FROM {tabSafe} WHERE {colSafe} IS NOT NULL",
                            null, 30, ct)).ToList();
                        var vals = filas.Select(f => f.TryGetValue("V", out var v) || f.TryGetValue("v", out v) ? v?.ToString() : null)
                            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();
                        return (Clave: $"{info.Tabla}.{col}", Valores: vals);
                    }
                    catch { return (Clave: "", Valores: new List<string>()); }
                }))
                .ToList();

            foreach (var (claveCol, vals) in await Task.WhenAll(tareasValores))
            {
                if (vals.Count > 0 && !string.IsNullOrEmpty(claveCol)) mapa[claveCol] = vals;
            }
        }
        catch { /* sin valores: el LLM trabaja solo con esquema */ }
        if (mapa.Count > 0)
            _cacheValores[clave] = (DateTime.UtcNow.AddMinutes(5), mapa);
        return mapa;
    }

    /// <summary>Formatea el mapa de valores observados para el prompt del LLM.</summary>
    private static string FormatearValores(Dictionary<string, List<string>> mapa)
    {
        if (mapa.Count == 0) return "(sin valores observados)";
        return string.Join("; ", mapa.Select(kv => $"{kv.Key}=[{string.Join("|", kv.Value.Take(15))}]"));
    }

    /// <summary>
    /// Elige la tabla por SIMILITUD entre la pregunta y el nombre de la tabla MÁS sus
    /// columnas (todo del catálogo).
    ///
    /// Antes comparaba por substring: `pregunta.Contains(nombreTabla)`. Con otra base de
    /// datos eso falla —preguntar por "los pacientes" no encuentra la tabla `admisiones`
    /// porque su nombre no aparece— y además da falsos positivos triviales.
    ///
    /// Sin embeddings (Ollama caído) no se elige por vocabulario: se devuelven todas las
    /// tablas candidatas para que la decisión la tome el LLM con el esquema completo.
    /// </summary>
    private async Task<string> ElegirTablaAsync(ConexionBaseDatos conexion, string pregunta, CancellationToken ct, string? tablaSugerida = null)
    {
        var tablas = conexion.TablasAutorizadas.Select(t => t.NombreTabla)
            .Concat(conexion.VistasAutorizadas.Select(v => v.NombreVista))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Sin tablas autorizadas no hay nada que consultar: la validación de
        // autorización rechazará cualquier SQL. Antes devolvía "Activos" hardcodeado.
        if (tablas.Count == 0) return string.Empty;
        // Sugerencia del Planner validada: si está autorizada en ESTA conexión se
        // usa sin re-adivinar (cada paso SQL de un plan multi-tabla trae la suya).
        if (!string.IsNullOrWhiteSpace(tablaSugerida))
        {
            var autorizada = tablas.FirstOrDefault(t => t.Equals(tablaSugerida, StringComparison.OrdinalIgnoreCase));
            if (autorizada != null)
            {
                _logger.LogInformation("SqlQueryTool: tabla '{Tabla}' aceptada por sugerencia del plan.", autorizada);
                return autorizada;
            }
        }
        if (tablas.Count == 1) return tablas[0];
        if (_embeddingProvider == null || string.IsNullOrWhiteSpace(pregunta)) return tablas[0];

        try
        {
            // Señal de valor observado, antes que la similitud (plan #9110): si la
            // pregunta contiene un valor REAL de una tabla ("productos de marca
            // Dell" → Activos.Marca='Dell'), esa tabla es el sujeto de la
            // consulta. Es la misma señal que usa el PlanBuilder para elegirla, así
            // ambos coinciden; la similitud sola se iba a Ventas porque la pregunta
            // ALSO menciona vacations/documento. Sin vocabulario: los valores salen
            // de un DISTINCT real de la base.
            var mapaValores = await ObtenerValoresMapaAsync(conexion, ct);
            if (mapaValores.Count > 0)
            {
                var exactos = new List<ValorDetectado>();
                var plurales = new List<ValorDetectado>();
                FiltroSemantico.Detectar(pregunta, mapaValores, exactos, plurales);
                // La clave completa "Tabla.Columna" viaja en cada valor detectado: el nombre de
                // columna solo no dice de qué tabla es (y dos tablas pueden
                // compartir columna).
                var porValor = FiltroSemantico.ElegirPorColumna(exactos, plurales)
                    .SelectMany(kv => kv.Value)
                    .Select(v => v.Clave.Contains('.') ? v.Clave[..v.Clave.IndexOf('.')] : string.Empty)
                    .Where(t => !string.IsNullOrWhiteSpace(t)
                        && tablas.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (porValor.Count > 0)
                {
                    var tablaPorValor = porValor[0];
                    _logger.LogInformation("SqlQueryTool: tabla '{Tabla}' elegida por valor observado en la pregunta.", tablaPorValor);
                    return tablaPorValor;
                }
            }

            // Descripción de cada tabla = su nombre + el de sus columnas: el significado
            // de la tabla incluye qué contiene, no solo cómo se llama.
            var columnasPorTabla = await ObtenerColumnasPorTablaAsync(conexion, tablas, ct);
            var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);

            var mejor = tablas[0];
            var mejorSim = double.NegativeInfinity;
            foreach (var t in tablas)
            {
                var descripcion = columnasPorTabla.TryGetValue(t, out var cols) && cols.Count > 0
                    ? t + " " + string.Join(" ", cols)
                    : t;
                var embTabla = await EmbeddingCacheadoAsync(descripcion, ct);
                var sim = SeleccionHerramientaSemantica.Coseno(embPregunta, embTabla);
                if (sim > mejorSim) { mejorSim = sim; mejor = t; }
            }

            _logger.LogInformation("SqlQueryTool: tabla '{Tabla}' elegida con similitud {Sim:F3}.", mejor, mejorSim);
            return mejor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SqlQueryTool: no se pudo elegir tabla por similitud; se usa la primera autorizada.");
            return tablas[0];
        }
    }

    /// <summary>Columnas por tabla, con caché corta (el esquema no cambia seguido).</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, Dictionary<string, List<string>> Cols)> _cacheColumnasPorTabla
        = new(StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, List<string>>> ObtenerColumnasPorTablaAsync(
        ConexionBaseDatos conexion, List<string> tablas, CancellationToken ct)
    {
        var clave = $"colt{conexion.IdConexion}:{string.Join(",", tablas.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))}";
        if (_cacheColumnasPorTabla.TryGetValue(clave, out var hit) && hit.Expira > DateTime.UtcNow)
            return hit.Cols;

        var resultado = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
            var tareas = tablas.Select(async t =>
            {
                try
                {
                    var filas = (await _executor.ExecuteReadOnlyAsync(
                        cadena,
                        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION",
                        new Dictionary<string, object?> { ["t"] = t }, 60, ct)).ToList();
                    return (Tabla: t, Cols: filas.Select(f => f.Values.FirstOrDefault()?.ToString() ?? "")
                        .Where(s => s.Length > 0).ToList());
                }
                catch { return (Tabla: t, Cols: new List<string>()); }
            }).ToList();
            foreach (var (tabla, cols) in await Task.WhenAll(tareas))
                if (cols.Count > 0) resultado[tabla] = cols;
        }
        catch { }

        _cacheColumnasPorTabla[clave] = (DateTime.UtcNow.AddMinutes(5), resultado);
        return resultado;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, float[]> _cacheEmbeddingTexto
        = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim _lockEmbeddingTexto = new(1, 1);

    private async Task<float[]> EmbeddingCacheadoAsync(string texto, CancellationToken ct)
    {
        if (_cacheEmbeddingTexto.TryGetValue(texto, out var e)) return e;
        await _lockEmbeddingTexto.WaitAsync(ct);
        try
        {
            if (_cacheEmbeddingTexto.TryGetValue(texto, out e)) return e;
            e = await _embeddingProvider!.GenerateEmbeddingAsync(texto).WaitAsync(ct);
            _cacheEmbeddingTexto[texto] = e;
            return e;
        }
        finally { _lockEmbeddingTexto.Release(); }
    }

    /// <summary>
    /// Vía rápida sin LLM: construye WHERE solo con valores OBSERVADOS en BD.
    /// Devuelve null si no hay ningún filtro (el llamador intentará el LLM).
    /// El SELECT depende del modo estructural: ANALISIS → COUNT; otro → filas +
    /// conteo acompañante con el mismo WHERE.
    /// </summary>
    private static PlanSql? FallbackEstructurado(
        string pregunta, ConexionBaseDatos conexion, string tabla,
        Dictionary<string, List<string>> mapaValores, string modo, PerfilMetrica perfil)
    {
        var nombreTabla = tabla;
        var filtros = ExtraerFiltrosPorValores(pregunta, mapaValores, nombreTabla);
        if (filtros.Count == 0) return null;

        var where = " WHERE " + string.Join(" AND ", filtros.Select(f => f.Condicion));
        // Se desglosa por la MISMA columna que se filtró: es la dimensión que el usuario
        // está comparando ("Ropa vs Hogar" → WHERE Categoría, GROUP BY Categoría).
        // El LLM no tiene que sumar a mano y el desglose no depende de su interpretación.
        var columnaGrupo = filtros[0].Columna;

        if (modo == "ANALISIS")
            return new PlanSql(AggregateSql(nombreTabla, where, perfil, columnaGrupo), null, true, columnaGrupo, perfil.Etiqueta);
        return new PlanSql(
            $"SELECT TOP 10 * FROM [{nombreTabla}]{where};",
            AggregateSql(nombreTabla, where, perfil, columnaGrupo), true, columnaGrupo, perfil.Etiqueta);
    }

    /// <summary>Una condición WHERE con la columna que la produce (para agrupar por ella).</summary>
    private sealed record FiltroColumna(string Columna, string Condicion);

    /// <summary>
    /// Filtros desde valores reales observados (mapa Tabla.Col → valores).
    /// Delega la detección en FiltroSemantico (compartido con PlanBuilder) y
    /// construye las condiciones (= para literal completo, LIKE para parcial).
    /// Si un literal solo menciona el NOMBRE de la tabla ("cuántos activos..." →
    /// Estado='ACTIVO' en la tabla Activos, plan #9062), no es un filtro: es el
    /// sujeto de la consulta y se descarta. Criterio por esquema (nombre real de
    /// la tabla + morfología genérica), sin listas de vocabulario.
    /// </summary>
    private static List<FiltroColumna> ExtraerFiltrosPorValores(
        string pregunta, Dictionary<string, List<string>> mapaValores, string? tabla = null)
    {
        var resultado = new List<FiltroColumna>();
        try
        {
            var exactos = new List<ValorDetectado>();
            var plurales = new List<ValorDetectado>();
            FiltroSemantico.Detectar(pregunta, mapaValores, exactos, plurales);
            foreach (var (col, valores) in FiltroSemantico.ElegirPorColumna(exactos, plurales))
            {
                var utiles = valores.Where(v => !EsMencionDeTabla(v.Literal, tabla)).ToList();
                if (utiles.Count == 0) continue;
                var colSafe = "[" + col.Replace("]", "") + "]";
                string Cond(ValorDetectado x)
                {
                    var litSafe = x.Literal.Replace("'", "''");
                    return x.EsCompleto ? $"{colSafe} = '{litSafe}'" : $"{colSafe} LIKE '%{litSafe}%'";
                }
                resultado.Add(new FiltroColumna(col,
                    utiles.Count == 1 ? Cond(utiles[0]) : "(" + string.Join(" OR ", utiles.Select(Cond)) + ")"));
            }
        }
        catch { /* sin filtros */ }
        return resultado;
    }

    /// <summary>
    /// ¿El literal solo nombra la tabla consultada? Se compara por morfología
    /// genérica (igual o forma plural/género, en ambas direcciones) contra el
    /// nombre real de la tabla y sus piezas (PascalCase/snake). Sin vocabulario.
    /// </summary>
    internal static bool EsMencionDeTabla(string literal, string? tabla)
    {
        if (string.IsNullOrWhiteSpace(literal) || string.IsNullOrWhiteSpace(tabla)) return false;
        var litNorm = FiltroSemantico.Normalizar(literal);
        if (litNorm.Length < 4) return false;
        var piezas = System.Text.RegularExpressions.Regex.Replace(tabla, @"(?<=[a-z0-9])(?=[A-Z])|[_-]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(FiltroSemantico.Normalizar)
            .Where(w => w.Length >= 4)
            .ToList();
        if (piezas.Count == 0) return false;
        var tablaFull = string.Concat(piezas);
        static bool Forma(string a, string b) => a.Equals(b, StringComparison.Ordinal)
            || FiltroSemantico.EsFormaDe(a, b) || FiltroSemantico.EsFormaDe(b, a);
        if (Forma(litNorm, tablaFull)) return true;
        return piezas.Any(p => Forma(litNorm, p));
    }

    /// <summary>
    /// Filtro numérico (plan #7060, cero vocabulario de dominio).
    /// Umbral = dígitos estructurales (regex, sin palabras). Columna = metadatos:
    /// si hay una sola numérica se usa directo; si hay varias, similitud semántica
    /// (embeddings) o micro-LLM genérico. Operador = símbolos (&gt;,&lt;,=) o
    /// semántica genérica (mayor/menor/igual como conceptos, no substrings).
    /// Null si no hay dígitos o la tabla no tiene numéricas.
    /// </summary>
    private async Task<FiltroColumna?> ConstruirFiltroNumericoAsync(
        string pregunta, ConexionBaseDatos conexion, string tabla, PerfilMetrica perfil, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pregunta)) return null;
            if (perfil.Medida == null && perfil.Cantidad == null) return null;
            var umbrales = ExtraerUmbrales(pregunta);
            if (umbrales.Count == 0) return null;
            // Un año suelto ("Metas para 2027") no es un umbral de cantidad
            // (plan #9152: terminó en CostoUnitario > 2027 y vació la consulta).
            // Sin símbolo de comparación explícito no se filtra por él; se prueba
            // con el siguiente número si lo hay ("metas 2027 y precio mayor a
            // 1000" usa 1000). Estructural (4 dígitos en rango de año), sin
            // vocabulario. Con símbolo ("precio > 2020") sí se respeta.
            var umbral = ElegirUmbral(umbrales, pregunta);
            if (umbral == null) return null;
            var columna = await ElegirColumnaNumericaAsync(pregunta, conexion, tabla, perfil, ct);
            if (string.IsNullOrWhiteSpace(columna)) return null;
            var operador = await ResolverOperadorAsync(pregunta, ct);
            var colSafe = "[" + columna.Replace("]", "") + "]";
            var valorSql = umbral.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new FiltroColumna(columna, $"{colSafe} {operador} {valorSql}");
        }
        catch { return null; }
    }

    /// <summary>Umbrales numéricos por estructura (dígitos), sin vocabulario.</summary>
    internal static List<decimal> ExtraerUmbrales(string pregunta)
    {
        var lista = new List<decimal>();
        if (string.IsNullOrWhiteSpace(pregunta)) return lista;
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(pregunta, @"\d[\d\s.,]*\d|\d"))
        {
            var crudo = m.Value.Replace(" ", string.Empty);
            // Formato es-PE: '.' miles y ',' decimal, o invariante. Se normaliza
            // por estructura (posición de separadores), no por palabras.
            string normalizado;
            if (crudo.Contains('.') && crudo.Contains(','))
                normalizado = crudo.Replace(".", string.Empty).Replace(',', '.');
            else if (crudo.Contains(',') && !crudo.Contains('.'))
                normalizado = crudo.Contains(",00") || crudo.EndsWith(",0")
                    ? crudo.Replace(",", string.Empty)
                    : crudo.Replace(',', '.');
            else
                normalizado = crudo.Replace(",", string.Empty);
            if (decimal.TryParse(normalizado,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
                lista.Add(v);
        }
        return lista.Distinct().Take(2).ToList();
    }

    /// <summary>
    /// ¿El número parece un año (4 dígitos en rango de año)? Los años referencian
    /// puntos en el tiempo, no cotas de cantidad: sin símbolo de comparación no
    /// pueden ser umbral ("Metas para 2027" ≠ "cantidad > 2027"). Estructural
    /// (dígitos y rango), sin vocabulario.
    /// </summary>
    internal static bool EsAnio(decimal valor)
        => valor == Math.Truncate(valor) && valor >= 1900 && valor <= 2100;

    /// <summary>
    /// Elige el umbral a filtrar: el primero que no sea un año suelto sin símbolo
    /// de comparación. Null si todos son años sin símbolo (no hay cota de cantidad).
    /// </summary>
    internal static decimal? ElegirUmbral(List<decimal> umbrales, string pregunta)
    {
        if (umbrales.Count == 0) return null;
        var tieneSimbolo = DetectarOperadorPorSimbolos(pregunta) != null;
        foreach (var u in umbrales)
            if (!EsAnio(u) || tieneSimbolo)
                return u;
        return null;
    }

    /// <summary>Operador por símbolos estructurales (&gt;, &lt;, =, &gt;=, &lt;=).</summary>
    internal static string? DetectarOperadorPorSimbolos(string pregunta)
    {
        if (string.IsNullOrEmpty(pregunta)) return null;
        if (pregunta.Contains(">=")) return ">=";
        if (pregunta.Contains("<=")) return "<=";
        if (pregunta.Contains(">")) return ">";
        if (pregunta.Contains("<")) return "<";
        return null;
    }

    /// <summary>
    /// Resuelve el operador sin NINGUNA lista de palabras: símbolos estructurales
    /// (&gt;, &lt;, =) → micro-LLM semántico (JSON) → "&gt;" como último recurso.
    /// La versión anterior usaba prototipos de embeddings hardcodeados ("mayor que",
    /// "supera el valor", ...): era vocabulario fijo en C# y resolvió "=" para
    /// "supera los 1000" (plan #9061). Eliminados.
    /// </summary>
    private async Task<string> ResolverOperadorAsync(string pregunta, CancellationToken ct)
    {
        var porSimbolo = DetectarOperadorPorSimbolos(pregunta);
        if (porSimbolo != null) return porSimbolo;

        // Caché corta por pregunta normalizada: los pasos CONSULTA + ANALISIS de un
        // mismo plan comparten texto y resolverían el operador dos veces en Ollama
        // (CPU, NUM_PARALLEL=1). Clave estructural, sin palabras.
        var clave = System.Text.RegularExpressions.Regex.Replace(
            (pregunta ?? string.Empty).ToLowerInvariant().Trim(), @"\s+", " ");
        if (_cacheOperador.TryGetValue(clave, out var hit) && hit.Expira > DateTime.UtcNow)
            return hit.Operador;

        // Micro-LLM genérico: solo pide el operador, sin nombres de dominio.
        if (_ollama != null)
        {
            try
            {
                var historial = new List<Mensaje>
                {
                    new Mensaje
                    {
                        Rol = RolMensaje.User,
                        Contenido = "La solicitud menciona una cantidad umbral. ¿La comparación es mayor (>), menor (<) o igual (=)? " +
                            "Responde SOLO este JSON: {\"operador\":\">\"}. Solicitud: " + pregunta
                    }
                };
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(45));
                var respuesta = await _ollama.SendMessageAsync(historial, null, null, 0.0, 100, cts.Token);
                if (!string.IsNullOrWhiteSpace(respuesta))
                {
                    var inicio = respuesta.IndexOf('{');
                    var fin = respuesta.LastIndexOf('}');
                    if (inicio >= 0 && fin > inicio)
                    {
                        var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                            respuesta[inicio..(fin + 1)],
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (doc != null && doc.TryGetValue("operador", out var op)
                            && op is ">" or "<" or ">=" or "<=" or "=")
                        { _cacheOperador[clave] = (DateTime.UtcNow.AddMinutes(10), op); return op; }
                    }
                    // El modelo reasoning a veces devuelve SQL en vez de JSON: se
                    // acepta el operador estructural que contenga (sin leer palabras).
                    var simbolo = DetectarOperadorPorSimbolos(respuesta);
                    if (simbolo != null) { _cacheOperador[clave] = (DateTime.UtcNow.AddMinutes(10), simbolo); return simbolo; }
                }
            }
            catch { /* último recurso */ }
        }

        // Último recurso documentado: umbrales de conteo ("cuántos superan X")
        // son mayoritariamente cotas inferiores. Se registra para auditoría.
        _logger.LogWarning("SqlQueryTool: operador no resuelto semánticamente; usando '>' por defecto.");
        return ">";
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, string Operador)> _cacheOperador
        = new(StringComparer.Ordinal);

    /// <summary>
    /// Columna numérica objetivo por metadatos; con varias, similitud semántica
    /// (pregunta vs nombre de columna) o micro-LLM. Sin listas de nombres.
    /// </summary>
    private async Task<string?> ElegirColumnaNumericaAsync(
        string pregunta, ConexionBaseDatos conexion, string tabla, PerfilMetrica perfil, CancellationToken ct)
    {
        var candidatas = new List<string>();
        if (!string.IsNullOrWhiteSpace(perfil.Medida)) candidatas.Add(perfil.Medida!);
        if (!string.IsNullOrWhiteSpace(perfil.Cantidad) && !candidatas.Contains(perfil.Cantidad!))
            candidatas.Add(perfil.Cantidad!);
        if (candidatas.Count == 0) return null;
        if (candidatas.Count == 1) return candidatas[0];

        if (_embeddingProvider != null)
        {
            try
            {
                var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ct);
                var mejor = candidatas[0];
                var mejorSim = double.NegativeInfinity;
                foreach (var c in candidatas)
                {
                    var sim = SeleccionHerramientaSemantica.Coseno(embPregunta,
                        await EmbeddingCacheadoAsync(tabla + " " + c, ct));
                    if (sim > mejorSim) { mejorSim = sim; mejor = c; }
                }
                return mejor;
            }
            catch { /* cae al micro-LLM */ }
        }

        if (_ollama != null)
        {
            try
            {
                var historial = new List<Mensaje>
                {
                    new Mensaje
                    {
                        Rol = RolMensaje.User,
                        Contenido = "Dadas las columnas numéricas [" + string.Join(", ", candidatas) +
                            "] de la tabla " + tabla + ". ¿Cuál mide la cantidad umbral de la solicitud? " +
                            "Responde SOLO este JSON: {\"columna\":\"...\"}. Solicitud: " + pregunta
                    }
                };
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                var respuesta = await _ollama.SendMessageAsync(historial, null, null, 0.0, 100, cts.Token);
                if (!string.IsNullOrWhiteSpace(respuesta))
                {
                    var inicio = respuesta.IndexOf('{');
                    var fin = respuesta.LastIndexOf('}');
                    if (inicio >= 0 && fin > inicio)
                    {
                        var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                            respuesta[inicio..(fin + 1)],
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (doc != null && doc.TryGetValue("columna", out var col)
                            && candidatas.Any(c => c.Equals(col.Trim(), StringComparison.OrdinalIgnoreCase)))
                            return candidatas.First(c => c.Equals(col.Trim(), StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            catch { /* respaldo */ }
        }

        return candidatas[0];
    }

    /// <summary>Lee una columna por nombre insensible a caso.</summary>
    private static string? LeerColumna(Dictionary<string, object?> fila, string columna)
    {
        foreach (var kv in fila)
            if (kv.Key.Equals(columna, StringComparison.OrdinalIgnoreCase))
                return kv.Value?.ToString();
        return null;
    }

    private static string GenerarResumen(List<Dictionary<string, object?>> datos, List<string> columnas)
    {
        // Tope de tamaño: el resultado se concatena en el contexto de pasos
        // posteriores y la ventana del modelo local es limitada (Ollama num_ctx).
        const int maxChars = 2500;
        if (datos.Count == 0) return ContratoResultado.MarcarSinDatos("la consulta no devolvió filas");
        var sb = new System.Text.StringBuilder();
        var mostradas = 0;
        foreach (var fila in datos.Take(10))
        {
            var linea = string.Join(" | ", columnas.Select(c => $"{c}: {Formatear(fila.TryGetValue(c, out var v) ? v : null)}"));
            if (sb.Length + linea.Length > maxChars) break;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(linea);
            mostradas++;
        }
        if (mostradas < datos.Count)
            sb.Append($"\n… ({datos.Count - mostradas} fila(s) más, {datos.Count} en total en esta muestra).");
        return sb.ToString();
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
