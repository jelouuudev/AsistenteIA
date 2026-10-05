using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Tokenizador y validador ESTRUCTURAL de SQL Server de solo lectura.
///
/// Por qué no una lista de palabras prohibidas: el filtro por substring sobre el texto
/// plano daba dos fallos. (1) Falsos positivos: cualquier columna o tabla llamada
/// <c>LastUpdate</c> o <c>IsDeleted</c> contiene "update"/"delete" y la consulta se
/// rechazaba. (2) Falsos negativos: un comentario o un literal de texto podía esconder
/// algo, y la comprobación solo miraba que la consulta empezara por SELECT.
///
/// Aquí se analiza la ESTRUCTURA: se tokeniza respetando literales, identificadores
/// entre corchetes/comillas y comentarios; se exige UNA sola sentencia y que su palabra
/// inicial sea SELECT (o WITH cuya sentencia principal sea SELECT). Ningún verbo de
/// escritura puede aparecer, y se prohíbe SELECT ... INTO (que crea una tabla).
///
/// Nota: las palabras reservadas de SQL son gramática del lenguaje, no vocabulario de
/// negocio. No cambian al agregar bases de datos. Lo que sí era dependent del dominio —
/// y se eliminó— eran las listas de términos para decidir qué ejecutar.
/// </summary>
public static class AnalizadorSql
{
    public enum TipoToken { Identificador, PalabraClave, Literal, Numero, Operador, Puntuacion, Variable, Comentario }

    public sealed record Token(TipoToken Tipo, string Texto, bool Citado);

    public sealed record ResultadoValidacion(bool Seguro, string? Motivo)
    {
        public static readonly ResultadoValidacion Ok = new(true, null);
    }

    /// <summary>
    /// Divide el SQL en tokens. Los literales de cadena y los identificadores
    /// delimitados se entregan como un solo token, de modo que su contenido NUNCA se
    /// interpreta como gramática.
    /// </summary>
    public static List<Token> Tokenizar(string sql)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Comentario de línea: se descarta (y se puede marcar como sospechoso).
            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n') i++;
                continue;
            }
            // Comentario de bloque.
            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i = Math.Min(sql.Length, i + 2);
                continue;
            }

            // Literal de cadena: '' es el escape de una comilla interna.
            if (c == '\'')
            {
                var sb = new StringBuilder();
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                        i++; break;
                    }
                    sb.Append(sql[i]); i++;
                }
                tokens.Add(new Token(TipoToken.Literal, sb.ToString(), true));
                continue;
            }

            // Identificador entre corchetes: [Mi Tabla]
            if (c == '[')
            {
                var sb = new StringBuilder();
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == ']')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == ']') { sb.Append(']'); i += 2; continue; }
                        i++; break;
                    }
                    sb.Append(sql[i]); i++;
                }
                tokens.Add(new Token(TipoToken.Identificador, sb.ToString(), true));
                continue;
            }

            // Identificador entre comillas dobles: "Mi Tabla"
            if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '"') { i++; break; }
                    sb.Append(sql[i]); i++;
                }
                tokens.Add(new Token(TipoToken.Identificador, sb.ToString(), true));
                continue;
            }

            // Variable: @x, @@x, #tmp
            if (c == '@' || c == '#')
            {
                var sb = new StringBuilder();
                sb.Append(c); i++;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_' || sql[i] == '@')) { sb.Append(sql[i]); i++; }
                tokens.Add(new Token(TipoToken.Variable, sb.ToString(), false));
                continue;
            }

            // Número
            if (char.IsDigit(c) || (c == '.' && i + 1 < sql.Length && char.IsDigit(sql[i + 1])))
            {
                var sb = new StringBuilder();
                while (i < sql.Length && (char.IsDigit(sql[i]) || sql[i] == '.')) { sb.Append(sql[i]); i++; }
                tokens.Add(new Token(TipoToken.Numero, sb.ToString(), false));
                continue;
            }

            // Identificador o palabra clave
            if (char.IsLetter(c) || c == '_' || c == '@')
            {
                var sb = new StringBuilder();
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_')) { sb.Append(sql[i]); i++; }
                var texto = sb.ToString();
                tokens.Add(new Token(EsPalabraClave(texto) ? TipoToken.PalabraClave : TipoToken.Identificador, texto, false));
                continue;
            }

            // Operador / puntuación
            if (i + 1 < sql.Length && (c == '<' || c == '>' || c == '!' || c == '|' || c == '='))
            {
                tokens.Add(new Token(TipoToken.Operador, sql.Substring(i, 2), false));
                i += 2;
                continue;
            }
            tokens.Add(new Token("+-*/%<>=(),;.".IndexOf(c) >= 0 ? TipoToken.Operador : TipoToken.Puntuacion, c.ToString(), false));
            i++;
        }
        return tokens;
    }

    /// <summary>
    /// Valida que el SQL sea UNA sentencia de solo lectura.
    /// </summary>
    public static ResultadoValidacion ValidarSoloLectura(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new ResultadoValidacion(false, "consulta vacía.");

        var tokens = Tokenizar(sql);
        if (tokens.Count == 0)
            return new ResultadoValidacion(false, "no se encontró ninguna sentencia.");

        // 1) Separadores de sentencia: solo puede haber uno y al final.
        var separadores = tokens.Where(t => t.Texto == ";").ToList();
        if (separadores.Count > 1)
            return new ResultadoValidacion(false, "solo se permite una sentencia por consulta.");
        if (separadores.Count == 1)
        {
            var indice = tokens.IndexOf(separadores[0]);
            if (indice != tokens.Count - 1)
                return new ResultadoValidacion(false, "solo se permite una sentencia por consulta.");
        }

        // 2) La sentencia debe empezar por SELECT. Se admite WITH (CTE) siempre que
        //    la sentencia principal sea un SELECT.
        var primero = tokens[0].Texto.ToUpperInvariant();
        if (primero == "WITH")
        {
            var principal = tokens.FirstOrDefault(t =>
                t.Tipo is TipoToken.PalabraClave &&
                EsSelectOSelectPadre(t.Texto));
            if (principal is null)
                return new ResultadoValidacion(false, "una CTE debe terminar en una consulta SELECT.");
        }
        else if (primero != "SELECT")
        {
            return new ResultadoValidacion(false, "solo se permiten consultas SELECT de solo lectura.");
        }

        // 3) Ningún verbo de escritura ni construcción que escriba. Se revisan SOLO
        //    palabras clave: el contenido de literales e identificadores entre
        //    corchetes/comillas queda fuera por construcción.
        foreach (var t in tokens)
        {
            if (t.Tipo != TipoToken.PalabraClave) continue;
            if (VerbosDeEscritura.Contains(t.Texto.ToUpperInvariant()))
                return new ResultadoValidacion(false, "la consulta contiene operaciones no permitidas.");
        }

        // 4) SELECT ... INTO crea una tabla: es una escritura aunque parezca lectura.
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i].Tipo == TipoToken.PalabraClave
                && tokens[i].Texto.Equals("INTO", StringComparison.OrdinalIgnoreCase)
                && tokens.Take(i).Any(t => t.Texto.Equals("SELECT", StringComparison.OrdinalIgnoreCase)))
                return new ResultadoValidacion(false, "SELECT ... INTO no está permitido (crea una tabla).");
        }

        return ResultadoValidacion.Ok;
    }

    private static bool EsSelectOSelectPadre(string texto)
    {
        var t = texto.ToUpperInvariant();
        return t == "SELECT";
    }

    /// <summary>Verbos y construcciones de escritura de T-SQL. Gramática del lenguaje.</summary>
    private static readonly HashSet<string> VerbosDeEscritura = new(StringComparer.Ordinal)
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "UPSERT",
        "DROP", "ALTER", "CREATE", "TRUNCATE", "RENAME",
        "EXEC", "EXECUTE", "CALL", "GRANT", "DENY", "REVOKE",
        "BACKUP", "RESTORE", "BULK", "OPENROWSET", "OPENDATASOURCE", "OPENQUERY"
    };

    /// <summary>Palabras reservadas que no son identificadores (para tokenización correcta).</summary>
    private static readonly HashSet<string> PalabrasReservadas = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT","FROM","WHERE","JOIN","INNER","LEFT","RIGHT","FULL","CROSS","OUTER","ON",
        "GROUP","BY","ORDER","HAVING","UNION","ALL","DISTINCT","TOP","AS","AND","OR","NOT",
        "IN","EXISTS","BETWEEN","LIKE","IS","NULL","CASE","WHEN","THEN","ELSE","END",
        "INSERT","UPDATE","DELETE","MERGE","INTO","VALUES","SET","WITH","OVER","PARTITION",
        "ASC","DESC","OFFSET","FETCH","NEXT","ROWS","ROW","APPLY","PIVOT","UNPIVOT",
        "CREATE","ALTER","DROP","TRUNCATE","EXEC","EXECUTE","GRANT","DENY","REVOKE",
        "MERGE","OUT","INNER","CONVERT","CAST","COALESCE","ISNULL","COUNT","SUM","AVG",
        "MIN","MAX","GETDATE","SYSDATETIME","NEWID"
    };

    private static bool EsPalabraClave(string texto) => PalabrasReservadas.Contains(texto);

    /// <summary>
    /// Nombres de objeto que aparecen como destino de FROM/JOIN. Se calcula sobre los
    /// tokens, no buscando la subcadena "FROM": así una columna llamada "from" o un
    /// literal con la palabra no confunden, y se resuelven [esquema].[tabla].
    /// </summary>
    public static List<string> ObjetosReferenciados(string sql)
    {
        var tokens = Tokenizar(sql);
        var resultado = new List<string>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Tipo != TipoToken.PalabraClave) continue;
            var up = t.Texto.ToUpperInvariant();
            if (up != "FROM" && up != "JOIN") continue;

            var nombre = LeerNombreObjeto(tokens, i + 1);
            if (!string.IsNullOrEmpty(nombre)) resultado.Add(nombre);
        }
        return resultado.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Lee [esquema].[objeto] u objeto a partir de la posición indicada.</summary>
    private static string? LeerNombreObjeto(List<Token> tokens, int inicio)
    {
        // Salta el paréntesis de una tabla derivada: FROM (SELECT ...)
        if (inicio < tokens.Count && tokens[inicio].Texto == "(") return null;

        var partes = new List<string>();
        int i = inicio;
        while (i < tokens.Count && partes.Count < 3)
        {
            if (tokens[i].Texto == ".")
            {
                i++;
                continue;
            }
            if (tokens[i].Tipo is TipoToken.Identificador or TipoToken.PalabraClave)
            {
                partes.Add(tokens[i].Texto);
                i++;
                if (i < tokens.Count && tokens[i].Texto == ".") { i++; continue; }
                break;
            }
            break;
        }
        if (partes.Count == 0) return null;
        return partes[^1].Trim().ToLowerInvariant();
    }
}
