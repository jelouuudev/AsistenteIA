using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Asistente.Application.Services;

public static class SectionExtractorHelper
{
    internal static string QuitarAcentos(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char c in normalizado)
        {
            var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria != System.Globalization.UnicodeCategory.NonSpacingMark &&
                categoria != System.Globalization.UnicodeCategory.ModifierLetter)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>
    /// Marcas / términos técnicos que NO deben partirse por reglas CamelCase.
    /// </summary>
    private static readonly string[] MarcasProtegidas =
    {
        "GitHub", "GitLab", "Bitbucket", "CloudSync", "TechCorp", "SonarQube",
        "Workday", "OpenAI", "ChatGPT", "DevOps", "NoSQL", "PostgreSQL", "MySQL",
        "MongoDB", "JavaScript", "TypeScript", "GraphQL", "OAuth", "ActiveDirectory",
        "PowerBI", "VSCode", "AppStore", "PlayStore", "YouTube", "LinkedIn",
        "WhatsApp", "OneDrive", "SharePoint", "DataDog", "NewRelic", "CloudFlare",
        "Cloudflare", "Kubernetes", "OpenShift", "BitLocker", "IntelliJ", "WebAPI",
        "RESTAPI", "gRPC", "OpenAPI", "SwaggerUI", "Postman", "DeepSeek", "ChatBot",
        "Apache", "Grafana", "Kafka"
    };

    /// <summary>
    /// Pares pegados frecuentes en extracción PDF (minúsculas).
    /// </summary>
    private static readonly (string Mal, string Bien)[] CorreccionesPegadas =
    {
        ("completarel", "completar el"),
        ("parallamadas", "para llamadas"),
        ("enteletrabajo", "en teletrabajo"),
        ("equipodebe", "equipo debe"),
        ("ratelimiting", "rate limiting"),
        ("paralelizastages", "paraleliza stages"),
        ("dashboardvia", "dashboard via"),
        ("kubernetesvia", "Kubernetes via"),
        ("blue-greendeployments", "blue-green deployments"),
        ("canaryreleases", "canary releases"),
        ("errorrates", "error rates"),
        ("pullrequest", "pull request"),
        ("correo electronico", "correo electronico"),
        ("webhooks personalizados", "webhooks personalizados"),
    };

    /// <summary>
    /// Correcciones de mayúsculas/minúsculas rotas por PdfPig font encoding.
    /// Mapea la versión corrupta → versión correcta.
    /// </summary>
    private static readonly Dictionary<string, string> CorreccionesMayusculasPdfPig = new(StringComparer.OrdinalIgnoreCase)
    {
        // Secciones de la guía de desarrollo
        ["EStAndaRdEs"] = "Estándares",
        ["cOdIgO"] = "Código",
        ["convEniCiones"] = "Convenciones",
        ["nOMbramientO"] = "Nomenclatura",
        ["ForMaTo"] = "Formato",
        ["RevisiÓN"] = "Revisión",
        ["Estructura"] = "Estructura",
        ["dE"] = "de",
        ["Archivos"] = "Archivos",
        ["Servicios"] = "Servicios",
        ["Repositorios"] = "Repositorios",
        ["Controlador"] = "Controlador",
        ["dEl"] = "del",
        ["lAs"] = "las",
        ["lOs"] = "los",
        ["ReglAs"] = "Reglas",
        ["dEfInIcIón"] = "Definición",
        ["ApRobAdA"] = "Aprobada",
        ["ApRobAdO"] = "Aprobado",
        ["Herramientas"] = "Herramientas",
        ["pArA"] = "para",
        ["cOn"] = "con",
        ["dEl"] = "del",
        ["lA"] = "la",
        ["y"] = "y",
        ["sOn"] = "son",
        ["pOr"] = "por",
        ["sE"] = "se",
        ["nO"] = "no",
        ["al"] = "al",
        ["su"] = "su",
        ["más"] = "más",
        ["mAs"] = "más",

        // Frases completas comunes que aparecen corruptas
        ["EStAndaRdEs dE cOdIgO"] = "Estándares de Código",
        ["convEniCiones dE nOMbramientO"] = "Convenciones de Nomenclatura",
        ["Estructura dE Archivos"] = "Estructura de Archivos",
        ["Proceso dE RevisiÓN"] = "Proceso de Revisión",
        ["Criterios dE ApRobAción"] = "Criterios de Aprobación",
        ["cOnvEnCionEs"] = "Convenciones",
        ["Contenido"] = "Contenido",
        ["IntRodUcción"] = "Introducción",
        ["IntRoduccion"] = "Introducción",
        ["PrUEbAs"] = "Pruebas",
        ["SeguridAd"] = "Seguridad",
        ["Versión"] = "Versión",
        ["Version"] = "Versión",
        ["Autenticación"] = "Autenticación",
        ["Configuración"] = "Configuración",
        ["Configuracion"] = "Configuración",
        ["DocumenTAción"] = "Documentación",
        ["DocumenTacion"] = "Documentación",
        ["El"] = "El",
        ["La"] = "La",
        ["Los"] = "Los",
        ["Las"] = "Las",
    };

    /// <summary>
    /// Limpia texto extraído de PDF: rompe palabras pegadas, añade saltos de línea en encabezados, etc.
    /// </summary>
    public static string LimpiarTextoPdf(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto;

        texto = texto.Trim();

        // Unir saltos artificiales dentro de palabra ANTES de proteger marcas:
        // "Git\nHub" / "Cloud\nSync" / "v\n1.0"
        texto = Regex.Replace(texto, @"([A-Za-zÁÉÍÓÚáéíóúñÑ]{2,})\r?\n([A-ZÁÉÍÓÚÑ][a-záéíóúñ]{1,6})\b", "$1$2");
        texto = Regex.Replace(texto, @"\bv\r?\n(\d)", "v$1", RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"([A-Za-z])\r?\n(\d)", "$1$2");
        // Punto huérfano al inicio de línea: "diaria\n. Canal" → "diaria. Canal"
        texto = Regex.Replace(texto, @"([a-záéíóúñÁÉÍÓÚÑ])\r?\n\.\s+", "$1. ");

        // Proteger marcas con límite de palabra (evita Kubernetesvia → token+via sin espacio)
        var protegidos = new List<string>();
        foreach (var marca in MarcasProtegidas.OrderByDescending(m => m.Length))
        {
            if (texto.IndexOf(marca, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var token = $"\uE000{protegidos.Count}\uE001";
            texto = Regex.Replace(texto, $@"\b{Regex.Escape(marca)}\b", token, RegexOptions.IgnoreCase);
            protegidos.Add(marca);
        }

        // Marca pegada a continuación en minúsculas: Kubernetesvia / dashboardvia
        // (si no hubo \b match, intentar prefijo de marca + resto minúscula)
        foreach (var marca in MarcasProtegidas.OrderByDescending(m => m.Length))
        {
            texto = Regex.Replace(
                texto,
                $@"\b({Regex.Escape(marca)})([a-záéíóúñ]{{2,}})\b",
                m =>
                {
                    var token = $"\uE000{protegidos.Count}\uE001";
                    protegidos.Add(m.Groups[1].Value);
                    return token + " " + m.Groups[2].Value;
                },
                RegexOptions.IgnoreCase);
        }

        // Proteger versiones (v1.0), también pegadas: v1.0Para
        texto = Regex.Replace(texto, @"\b(v\d+(?:\.\d+)*)(?![0-9.])", m =>
        {
            var token = $"\uE000{protegidos.Count}\uE001";
            protegidos.Add(m.Value);
            return token;
        }, RegexOptions.IgnoreCase);

        // Separar punto final pegado a número de sección: "containerizadas.10. Soporte"
        texto = Regex.Replace(
            texto,
            @"(?<!\d)([.])(\d{1,2}\.\s+[A-ZÁÉÍÓÚÑ])",
            "$1\n$2");

        // Token de versión/marca pegado a texto capital: "«v1.0»Para" → salto
        texto = Regex.Replace(
            texto,
            @"(\uE000\d+\uE001)([A-ZÁÉÍÓÚÑ])",
            "$1\n$2");

        // "Responsabilidades3.1 Recursos" → separar título de subsección
        texto = Regex.Replace(
            texto,
            @"([a-záéíóúñÁÉÍÓÚÑ])(\d{1,2}\.\d+\s*[A-ZÁÉÍÓÚÑ])",
            "$1\n\n$2");

        // Palabras españolas pegadas: "DirectoEl manager", "NuevoDebe", "ErgonomiaTechCorp" (tras restaurar)
        texto = Regex.Replace(
            texto,
            @"([a-záéíóúñ])(El|La|Los|Las|Debe|Para|Durante|Tambien|También|Este|Esta)\b",
            "$1 $2");

        // CamelCase largo: "TerminosDefiniciones" → espacio (no newline)
        texto = Regex.Replace(
            texto,
            @"([a-záéíóúñ]{3,})([A-ZÁÉÍÓÚÑ][a-záéíóúñ]{4,})\b",
            "$1 $2");

        // Subsecciones: 3.1 Recursos Humanos
        texto = Regex.Replace(
            texto,
            @"(?<![a-zA-Z\d])(\d{1,2}\.\d+)\.?\s+([A-ZÁÉÍÓÚÑ][^\n]{2,70}?)(?=\n|\d{1,2}\.\d+|[.]\s|\z)",
            "\n\n**$1** $2\n");

        // Secciones principales: 3. Roles y Responsabilidades
        texto = Regex.Replace(
            texto,
            @"(?<![a-zA-Z\d\.])(\d{1,2})\.\s+([A-ZÁÉÍÓÚÑ][^\n]{3,80}?)(?=\n|\d{1,2}\.\d+|\z)",
            "\n\n**$1.** $2\n");

        // ---- Viñetas ----
        // "texto.-Microsoft" / "rrhh.-Confluence" / "diaria.-Slack"
        texto = Regex.Replace(
            texto,
            @"\.\s*[-•]\s*(?=[A-ZÁÉÍÓÚÑ#])",
            ".\n- ");

        // "Obligatorias-Slack" / "son-Microsoft"
        texto = Regex.Replace(
            texto,
            @"([a-záéíóúñÁÉÍÓÚÑ0-9])\s*[-•]\s*(?=[A-ZÁÉÍÓÚÑ#])",
            "$1\n- ");

        texto = Regex.Replace(
            texto,
            @"(?<=[:\s]|son)[-•]\s*(?=[A-ZÁÉÍÓÚÑ#])",
            "\n- ");

        // Varias viñetas en la misma línea: "...pipelines. - Confluence:" ya cubierto;
        // también "- Item1.-Item2" tras primer pase
        texto = Regex.Replace(
            texto,
            @"(?<=\n)\s*[-•]\s+",
            "\n- ",
            RegexOptions.Multiline);

        // Oraciones pegadas: "dia.Este" → "dia. Este"
        texto = Regex.Replace(
            texto,
            @"\.([A-ZÁÉÍÓÚÑ])",
            ". $1");

        // Correcciones explícitas de palabras pegadas por extracción PDF
        foreach (var (mal, bien) in CorreccionesPegadas)
            texto = Regex.Replace(texto, $@"\b{Regex.Escape(mal)}\b", bien, RegexOptions.IgnoreCase);

        // Partículas frecuentes pegadas SOLO con verbos/sustantivos largos conocidos del corpus
        texto = Regex.Replace(texto,
            @"\b(completar|garantizar|solicitar|devolver|abrir|crear|configurar|desplegar|ejecutar|implementar|gestionar|comunican|integran|expone|recopila|soporta|paraleliza)(el|la|los|las|en|con|por|para|via|vía)\b",
            "$1 $2",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto,
            @"\b(para)(llamadas|problemas|ambientes|despliegues)\b",
            "$1 $2",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto,
            @"\b(en)(teletrabajo|produccion|producción|desarrollo|staging)\b",
            "$1 $2",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto,
            @"\b(equipo)(debe|asignado)\b",
            "$1 $2",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto,
            @"\b(dashboard|kubernetes|servidor|cluster)(via|vía|en|de|con)\b",
            "$1 $2",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto,
            @"\b(rate)(limiting)\b",
            "$1 $2",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto,
            @"\b(paraleliza)(stages)\b",
            "$1 $2",
            RegexOptions.IgnoreCase);

        // Hashtags pegados: "en#soporte" → "en #soporte"
        texto = Regex.Replace(texto, @"([a-záéíóúñÁÉÍÓÚÑ])(#[\w-]+)", "$1 $2");

        // Quitar encabezados/pies de página del manual que se cuelan en el cuerpo
        texto = Regex.Replace(
            texto,
            @"(?m)^(?:Manual de Onboarding|Manual (?:del )?Producto|Politica de Teletrabajo|Política de Teletrabajo|Guia de Desarrollo|Guía de Desarrollo)\s*v?\d*(?:\.\d+)*\s*$",
            "",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(
            texto,
            @"\bManual de Onboarding\s+v?\d+(?:\.\d+)*\s*",
            "",
            RegexOptions.IgnoreCase);

        // Título de sección pegado al cuerpo tras formateo markdown:
        // "**9.** Bienestar y ErgonomiaTechCorp se..." ya se cubre con CamelCase;
        // "Bienestar y Ergonomia TechCorp se" — si TechCorp queda solo al inicio de párrafo tras título, ok.

        // Restaurar marcas y versiones protegidas
        for (int i = 0; i < protegidos.Count; i++)
            texto = texto.Replace($"\uE000{i}\uE001", protegidos[i]);

        // Tras restaurar: marca pegada a capital (ErgonomiaTechCorp si TechCorp no estaba tokenizado a tiempo)
        foreach (var marca in MarcasProtegidas.OrderByDescending(m => m.Length))
        {
            texto = Regex.Replace(
                texto,
                $@"([a-záéíóúñ])({Regex.Escape(marca)})\b",
                "$1 $2",
                RegexOptions.IgnoreCase);
            texto = Regex.Replace(
                texto,
                $@"\b({Regex.Escape(marca)})([a-záéíóúñ]{{2,}})\b",
                "$1 $2",
                RegexOptions.IgnoreCase);
        }

        // Limpiar espacios raros y demasiados saltos
        texto = Regex.Replace(texto, @"[ \t]+\n", "\n");
        texto = Regex.Replace(texto, @"[ \t]{2,}", " ");
        texto = Regex.Replace(texto, @"\n{3,}", "\n\n");
        // Compactar viñetas: quitar líneas vacías entre items de lista
        texto = Regex.Replace(texto, @"(\n- [^\n]+)\n{2,}(?=- )", "$1\n");

        return texto.Trim();
    }

    /// <summary>
    /// Alias para compatibilidad hacia atrás.
    /// </summary>
    public static string FormatearTextoPdf(string texto) => LimpiarTextoPdf(texto);

    /// <summary>
    /// Separa viñetas que quedaron pegadas en una sola línea y normaliza el formato de lista.
    /// </summary>
    public static string FormatearListaLimpia(string texto, string? seccion = null)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto;

        texto = LimpiarTextoPdf(texto);

        // Asegurar saltos antes de cada ítem tipo "- Algo" en medio de línea
        texto = Regex.Replace(texto, @"(?<=\S)\s+(?=-\s+[A-ZÁÉÍÓÚÑ#])", "\n");
        texto = Regex.Replace(texto, @"\.\s*-\s+(?=[A-ZÁÉÍÓÚÑ#])", ".\n- ");

        var lineas = texto.Split('\n');
        var resultado = new List<string>();

        if (!string.IsNullOrWhiteSpace(seccion))
        {
            resultado.Add($"**{seccion}**");
            resultado.Add("");
        }

        var seccionNorm = string.IsNullOrWhiteSpace(seccion)
            ? ""
            : NormalizarTituloParaBusqueda(seccion);

        foreach (var linea in lineas)
        {
            var l = linea.Trim();
            if (string.IsNullOrWhiteSpace(l))
            {
                if (resultado.Count > 0 && resultado[^1] != "")
                    resultado.Add("");
                continue;
            }

            // Normalizar encabezado numerado: **5.1** Título → **5.1 Título**
            var mHeader = Regex.Match(l, @"^\*{0,2}(\d{1,2}(?:\.\d+)?)\.?\*{0,2}\s+(.+)$");
            if (mHeader.Success)
            {
                var num = mHeader.Groups[1].Value;
                var tit = mHeader.Groups[2].Value.Trim().Trim('*').Trim();
                if (tit.Length > 0)
                {
                    resultado.Add($"**{num}** {tit}");
                    resultado.Add("");
                }
                continue;
            }

            // Encabezado de página colado: "… TechCorp Manual de Onboarding v1.0"
            l = Regex.Replace(l,
                @"\s*(?:TechCorp\s+)?Manual de Onboarding\s+v?\d+(?:\.\d+)*\s*",
                " ",
                RegexOptions.IgnoreCase).Trim();
            l = Regex.Replace(l,
                @"\bManual de Onboarding\s+v?\d+(?:\.\d+)*\b",
                "",
                RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(l)) continue;

            // Saltar / recortar línea que repite el título de la sección
            if (!string.IsNullOrEmpty(seccionNorm) && seccion != null)
            {
                var lNorm = NormalizarTituloParaBusqueda(l);
                if (lNorm == seccionNorm || (lNorm.Length > 0 && seccionNorm.Contains(lNorm) && lNorm.Length >= seccionNorm.Length - 2))
                    continue;

                // "Bienestar y Ergonomia TechCorp se compromete..."
                if (lNorm.StartsWith(seccionNorm, StringComparison.Ordinal))
                {
                    var words = seccionNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var pattern = @"^\s*" + string.Join(@"\s+", words.Select(w => Regex.Escape(w))) +
                                  @"(?:\s+TechCorp)?\s*";
                    var resto = Regex.Replace(QuitarAcentos(l), pattern, "", RegexOptions.IgnoreCase).Trim();
                    // Mapear resto sobre el string original aproximando por longitud
                    if (resto.Length > 20 && resto.Length < l.Length)
                    {
                        // Buscar el inicio del cuerpo en el original (primera palabra del resto)
                        var primera = resto.Split(' ', 2)[0];
                        var idx = l.IndexOf(primera, StringComparison.OrdinalIgnoreCase);
                        if (idx > 0)
                            l = l[idx..].Trim();
                        else
                            l = resto;
                        l = Regex.Replace(l, @"^TechCorp\s+", "", RegexOptions.IgnoreCase);
                    }
                }
            }

            // Línea con varias viñetas implícitas separadas por ".-"
            if (Regex.IsMatch(l, @"\.\s*-\s*[A-ZÁÉÍÓÚÑ#]"))
            {
                var partes = Regex.Split(l, @"\.\s*-\s*(?=[A-ZÁÉÍÓÚÑ#])");
                for (int i = 0; i < partes.Length; i++)
                {
                    var p = partes[i].Trim().TrimStart('-', '•', '*', ' ').Trim();
                    if (p.Length == 0) continue;
                    if (!p.EndsWith('.') && i < partes.Length - 1)
                        p += ".";
                    resultado.Add($"- {p}");
                }
                continue;
            }

            var matchViñeta = Regex.Match(l, @"^([-•*]\s+|\d+\.\s+)(.+)$");
            if (matchViñeta.Success)
            {
                var contenido = matchViñeta.Groups[2].Value.Trim();
                // Sub-viñetas pegadas dentro del ítem
                if (Regex.IsMatch(contenido, @"\.\s*-\s*[A-ZÁÉÍÓÚÑ#]"))
                {
                    var partes = Regex.Split(contenido, @"\.\s*-\s*(?=[A-ZÁÉÍÓÚÑ#])");
                    for (int i = 0; i < partes.Length; i++)
                    {
                        var p = partes[i].Trim().TrimStart('-', '•', '*', ' ').Trim();
                        if (p.Length == 0) continue;
                        if (!p.EndsWith('.') && i < partes.Length - 1)
                            p += ".";
                        resultado.Add($"- {p}");
                    }
                }
                else
                {
                    resultado.Add($"- {contenido}");
                }
            }
            else if (Regex.IsMatch(l, @"^[A-ZÁÉÍÓÚÑ#].{5,80}:\s"))
            {
                // Ítem de lista sin guion: "Slack: comunicacion..."
                resultado.Add($"- {l}");
            }
            else
            {
                resultado.Add(l);
            }
        }

        var final = string.Join("\n", resultado);
        final = Regex.Replace(final, @"\n{3,}", "\n\n");
        final = Regex.Replace(final, @"(\n- [^\n]+)\n{2,}(?=- )", "$1\n");

        // Corrección de mayúsculas/minúsculas rotas por PdfPig font encoding
        final = CorregirMayusculasPdfPig(final);

        return final.Trim();
    }

    /// <summary>
    /// Corrige palabras con mayúsculas/minúsculas rotas por PdfPig font encoding
    /// (ej: "convEniCiones dE nOMbramientO" → "Convenciones de Nomenclatura").
    /// Usa diccionario de palabras/frases documentadas + fallback heurístico suave.
    /// </summary>
    private static string CorregirMayusculasPdfPig(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto;

        // 1. Frases completas (mayor longitud primero para evitar reemplazos parciales)
        foreach (var kv in CorreccionesMayusculasPdfPig
            .OrderByDescending(kv => kv.Key.Length))
        {
            if (kv.Key.Contains(' '))
                texto = Regex.Replace(texto, Regex.Escape(kv.Key), kv.Value, RegexOptions.IgnoreCase);
        }

        // 2. Palabras individuales del diccionario
        foreach (var kv in CorreccionesMayusculasPdfPig
            .Where(kv => !kv.Key.Contains(' '))
            .OrderByDescending(kv => kv.Key.Length))
        {
            texto = Regex.Replace(texto, $@"\b{Regex.Escape(kv.Key)}\b", kv.Value, RegexOptions.IgnoreCase);
        }

        // 3. Fallback heurístico limitado: solo palabras largas (>= 6) con >=3 mayúsculas 
        // en posiciones no-iniciales y que CONTENGAN letras con acento potencial
        texto = Regex.Replace(texto, @"\b([A-Za-zÁÉÍÓÚáéíóúñÑ]{6,})\b", m =>
        {
            var palabra = m.Value;
            if (Regex.IsMatch(palabra, @"[\d_]")) return palabra;
            if (palabra == palabra.ToUpperInvariant() || palabra == palabra.ToLowerInvariant())
                return palabra;

            int mayusculas = palabra.Count(char.IsUpper);
            if (mayusculas < 3) return palabra; // pocas mayúsculas → ok

            bool hayMayusculaInterna = palabra.Skip(1).Any(char.IsUpper);
            if (!hayMayusculaInterna) return palabra;

            // Patrón de PdfPig: mayúsculas interiores dispersas vs camelCase (mayúsculas en frontera de palabra)
            // Si las mayúsculas no forman camelCase limpio, probable error de encoding.
            // camelCase: después de cada mayúscula viene minúscula o mayúscula-fin.
            // PdfPig error: mayúsculas en posiciones arbitrarias intercaladas con minúsculas.
            bool pareceCamelCase = true;
            for (int i = 0; i < palabra.Length - 1; i++)
            {
                if (char.IsUpper(palabra[i]) && char.IsUpper(palabra[i + 1]))
                {
                    pareceCamelCase = false; // mayúscula seguida de mayúscula → no es camelCase limpio
                    break;
                }
                // Para camelCase real: secuencias como "getUserById" → u→U, r→B, d→I son transiciones lc→uc
                // Para PdfPig: "convEniCiones" → v→E (minúscula→mayúscula) se ve como camelCase...
                // Pero "nOMbramientO" → O→M (mayúscula→mayúscula) → NO es camelCase → detectado
            }
            if (pareceCamelCase) return palabra; // parece identificador válido

            // Convertir a título: primera mayúscula, resto minúsculas
            var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
            var corregida = cultura.TextInfo.ToTitleCase(palabra.ToLowerInvariant());

            return corregida;
        });

        return texto;
    }

    internal static bool Contiene(string texto, string busqueda)
    {
        if (string.IsNullOrEmpty(texto) || string.IsNullOrEmpty(busqueda)) return false;
        var textoNorm = QuitarAcentos(texto).Normalize(System.Text.NormalizationForm.FormC);
        var busquedaNorm = QuitarAcentos(busqueda).Normalize(System.Text.NormalizationForm.FormC);
        if (textoNorm.Contains(busquedaNorm, StringComparison.OrdinalIgnoreCase))
            return true;
        var textoSlash = textoNorm.Replace("/", "");
        var busquedaSlash = busquedaNorm.Replace("/", "");
        if (busquedaSlash != busquedaNorm && textoSlash.Contains(busquedaSlash, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    internal static List<string> ExtraerKeywords(string consulta)
    {
        var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cuales", "cuál", "cual", "son", "los", "las", "de", "del", "la", "el", "un", "una",
            "qué", "que", "dame", "hablame", "háblame", "cuentame", "cuéntame",
            "sobre", "informacion", "información",
            "el", "la", "lo", "los", "las", "un", "una", "unos", "unas",
            "y", "o", "de", "del", "en", "con", "por", "para", "a", "al",
            "se", "como", "cómo", "es", "son", "esta", "está",
            "hay", "tiene", "tienen", "puede", "pueden", "debe", "deben",
            "no", "si", "sí", "pero", "mas", "más", "menos", "todo", "toda",
            "todos", "todas", "otros", "otras", "otro", "otra",
            "ese", "esa", "esos", "esas", "este", "esta", "estos", "estas",
            "mi", "tu", "su", "mis", "tus", "sus", "me", "te", "le", "nos",
            "dime", "ahora", "algunas", "algunos", "del", "las", "los"
        };

        var consultaLimpia = QuitarAcentos(consulta.ToLowerInvariant());

        var abreviaciones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ci/cd"] = "cicd",
            ["ci / cd"] = "cicd",
            ["c.i./c.d."] = "cicd",
            ["api"] = "api",
            ["sql"] = "sql",
            ["pdf"] = "pdf",
            ["jwt"] = "jwt",
            ["2fa"] = "2fa",
        };

        foreach (var kv in abreviaciones)
        {
            if (consultaLimpia.Contains(kv.Key))
                consultaLimpia = consultaLimpia.Replace(kv.Key, kv.Value);
        }

        var palabras = Regex.Split(consultaLimpia, @"[^a-z0-9]+")
            .Where(p => p.Length >= 3 && !stopwords.Contains(p))
            .Distinct()
            .ToList();

        return palabras;
    }

    internal static int ContarKeywordsEnTexto(string texto, string consulta)
    {
        var keywords = ExtraerKeywords(consulta);
        return keywords.Count(k => Contiene(texto, k));
    }

    internal static string? ExtraerSeccionPorKeywords(string textoCompleto, string consulta, string? tituloHint = null)
    {
        if (string.IsNullOrWhiteSpace(textoCompleto) || string.IsNullOrWhiteSpace(consulta))
            return null;

        var keywords = ExtraerKeywords(consulta);
        if (keywords.Count == 0 && string.IsNullOrWhiteSpace(tituloHint))
            return null;

        // Reparar texto: insertar saltos de línea después de encabezados de sección pegados al contenido
        textoCompleto = RepararSaltosSecciones(textoCompleto);

        if (!string.IsNullOrWhiteSpace(tituloHint))
        {
            int idxHintRapido = BuscarTituloEnTexto(textoCompleto, tituloHint);
            if (idxHintRapido >= 0)
            {
                int ventana = Math.Min(10, idxHintRapido);
                var pref = textoCompleto.Substring(idxHintRapido - ventana, ventana);
                var numMatch = Regex.Match(pref, @"(\d{1,2})\.\s*$");
                bool esEncabezadoSeccion = numMatch.Success;
                if (esEncabezadoSeccion)
                {
                    int finRapido = BuscarFinSeccionNumerada(textoCompleto, idxHintRapido);
                    finRapido = Math.Min(finRapido, textoCompleto.Length);
                    int inicioRecorte = idxHintRapido - ventana + numMatch.Index;
                    var recorte = textoCompleto.Substring(inicioRecorte, finRapido - inicioRecorte).Trim();
                    if (recorte.Length >= 50)
                        return recorte;
                }
            }
        }

        var candidatos = RecolectarIniciosDeSeccion(textoCompleto);

        // Si conocemos el título, forzar un candidato en la primera coincidencia del hint.
        if (!string.IsNullOrWhiteSpace(tituloHint))
        {
            int idxHint = BuscarTituloEnTexto(textoCompleto, tituloHint);
            if (idxHint >= 0 && !candidatos.Any(c => Math.Abs(c.Posicion - idxHint) <= 8))
            {
                // Solo agregar el hint como candidato si está en un encabezado de sección numerado
                // (p.ej. "6. Capacitacion Obligatoria"), no en texto del cuerpo (p.ej. portada).
                bool esEncabezado = idxHint >= 3 &&
                    Regex.IsMatch(textoCompleto.Substring(Math.Max(0, idxHint - 4), Math.Min(4, idxHint)), @"\d{1,2}\.\s*$");
                if (esEncabezado)
                {
                    candidatos.Add((idxHint, tituloHint));
                    candidatos = candidatos.OrderBy(c => c.Posicion).ToList();
                }
            }
        }

        // Descartar candidatos que son subtítulos internos (p.ej. "Colaborador Nuevo" dentro de 3.4)
        // cuando ya tenemos un hint de sección principal.
        if (!string.IsNullOrWhiteSpace(tituloHint) && candidatos.Count > 1)
        {
            int idxPrincipal = BuscarTituloEnTexto(textoCompleto, tituloHint);
            if (idxPrincipal >= 0)
            {
                int finPrincipal = BuscarFinSeccionNumerada(textoCompleto, idxPrincipal);
                candidatos = candidatos
                    .Where(c => (c.Posicion >= idxPrincipal - 5 && c.Posicion <= idxPrincipal + 5) || c.Posicion >= finPrincipal - 2)
                    .ToList();
                bool esEncabezado = idxPrincipal >= 3 &&
                    Regex.IsMatch(textoCompleto.Substring(Math.Max(0, idxPrincipal - 4), Math.Min(4, idxPrincipal)), @"\d{1,2}\.\s*$");
                if (esEncabezado && !candidatos.Any(c => Math.Abs(c.Posicion - idxPrincipal) <= 8))
                    candidatos.Add((idxPrincipal, tituloHint));
                candidatos = candidatos.OrderBy(c => c.Posicion).ToList();
            }
        }

        if (candidatos.Count == 0)
            return null;

        int mejorInicio = -1;
        int mejorFin = -1;
        int mejorPuntaje = -1;

        for (int i = 0; i < candidatos.Count; i++)
        {
            int inicio = candidatos[i].Posicion;
            // Fin = siguiente candidato, o siguiente sección numerada principal, o tope duro.
            int fin = i + 1 < candidatos.Count ? candidatos[i + 1].Posicion : textoCompleto.Length;
            fin = Math.Min(fin, BuscarFinSeccionNumerada(textoCompleto, inicio));
            fin = Math.Min(fin, inicio + 3500); // tope duro: nunca devolver media novela
            if (fin <= inicio) continue;

            int longitud = Math.Min(fin - inicio, textoCompleto.Length - inicio);
            string seccion = textoCompleto.Substring(inicio, longitud);
            string headerLine = candidatos[i].Titulo;

            int puntaje = CalcularPuntajeHeader(headerLine, seccion, keywords, tituloHint);

            if (puntaje > mejorPuntaje)
            {
                mejorPuntaje = puntaje;
                mejorInicio = inicio;
                mejorFin = fin;
            }
        }

        int umbralMinimo = keywords.Count >= 2 ? 5 : 2;
        if (!string.IsNullOrWhiteSpace(tituloHint))
            umbralMinimo = Math.Max(umbralMinimo, 5);

        if (mejorPuntaje < umbralMinimo || mejorInicio < 0)
            return null;

        int longitudFinal = Math.Min(mejorFin - mejorInicio, textoCompleto.Length - mejorInicio);
        double ratioSeccion = (double)longitudFinal / Math.Max(textoCompleto.Length, 1);

        // Nunca aceptar una "sección" que sea la mayor parte del documento.
        // Si conocemos el título (tituloHint), permitimos hasta 0.70 ya que el usuario
        // pidió explícitamente esa sección.
        double umbralRatio = string.IsNullOrWhiteSpace(tituloHint) ? 0.40 : 0.70;
        if (ratioSeccion > umbralRatio || longitudFinal > 3500)
            return null;

        var textoSeccion = textoCompleto.Substring(mejorInicio, longitudFinal).Trim();
        return string.IsNullOrWhiteSpace(textoSeccion) || textoSeccion.Length < 50 ? null : textoSeccion;
    }

    /// <summary>
    /// Busca el inicio de la siguiente sección principal (N. Titulo) después de <paramref name="inicio"/>.
    /// Ignora subsecciones N.M.
    /// </summary>
    private static int BuscarFinSeccionNumerada(string texto, int inicio)
    {
        int numActual = 0;
        var ventanaInicio = texto.Substring(Math.Max(0, inicio - 4), Math.Min(44, texto.Length - Math.Max(0, inicio - 4)));
        var matchActual = Regex.Match(ventanaInicio, @"(\d{1,2})\.\s+[A-ZÁÉÍÓÚÑ]");
        if (!matchActual.Success)
            matchActual = Regex.Match(texto.Substring(inicio, Math.Min(40, texto.Length - inicio)), @"^(\d{1,2})\.\s+");
        if (matchActual.Success)
            int.TryParse(matchActual.Groups[1].Value, out numActual);

        // Acepta "4. Titulo" tras espacio/newline O pegado tras un punto: "dia.4. Titulo"
        var nextHeader = new Regex(
            @"(?:(?<![a-zA-Z\d])|(?<=\.))(\d{1,2})\.\s+[A-ZÁÉÍÓÚÑ]",
            RegexOptions.None);

        foreach (Match m in nextHeader.Matches(texto))
        {
            if (m.Index <= inicio + 5) continue;

            // Ignorar subsecciones N.M (el dígito va justo después de otro "N.")
            if (m.Index >= 2 && texto[m.Index - 1] == '.' && char.IsDigit(texto[m.Index - 2]))
                continue;

            if (!int.TryParse(m.Groups[1].Value, out int n))
                continue;

            if (numActual > 0 && n <= numActual)
                continue;

            return m.Index;
        }

        return texto.Length;
    }

    private static int CalcularPuntajeHeader(string headerLine, string seccion, List<string> keywords, string? tituloHint)
    {
        int puntaje = 0;

        foreach (var k in keywords)
        {
            if (Contiene(headerLine, k))
                puntaje += 5;
            else if (Contiene(seccion, k))
                puntaje += 1;
        }

        if (!string.IsNullOrWhiteSpace(tituloHint))
        {
            var hintKeywords = ExtraerKeywords(tituloHint);
            int hintHits = hintKeywords.Count(k => Contiene(headerLine, k));
            if (hintHits > 0)
                puntaje += hintHits * 4;

            if (Contiene(headerLine, tituloHint) || Contiene(tituloHint, headerLine))
                puntaje += 10;
        }

        return puntaje;
    }

    private static List<(int Posicion, string Titulo)> RecolectarIniciosDeSeccion(string textoCompleto)
    {
        var resultados = new SortedDictionary<int, string>();

        var headerRegex = new Regex(
            @"(?<![a-zA-Z\d\.])(\d{1,2})\.\s+([A-ZÁÉÍÓÚÑ][A-Za-záéíóúñÁÉÍÓÚÑ\s]{3,80}?)(?=(?:\s{2,}|\n|\r|\d{1,2}\.\d+|[A-ZÁÉÍÓÚÑ][a-záéíóúñ]{3,}[a-z]|\z))",
            RegexOptions.None);

        var subHeaderRegex = new Regex(
            @"(?<![a-zA-Z\d])\d{1,2}\.\d+\.?\s+[A-ZÁÉÍÓÚÑ]",
            RegexOptions.None);

        var subHeaderPositions = new HashSet<int>();
        foreach (Match sh in subHeaderRegex.Matches(textoCompleto))
            subHeaderPositions.Add(sh.Index);

        foreach (Match h in headerRegex.Matches(textoCompleto))
        {
            bool esSubHeader = subHeaderPositions.Any(shPos => Math.Abs(h.Index - shPos) <= 6);
            if (esSubHeader) continue;

            var titulo = $"{h.Groups[1].Value}. {h.Groups[2].Value.Trim()}";
            resultados[h.Index] = titulo;
        }

        // Títulos sin número, incluso pegados al contenido (p.ej. "Arquitectura de la PlataformaCloudSync")
        // o pegados a una subsección (p.ej. "Uso Diario del Desarrollador5.1 Crear...")
        var jammedTitleRegex = new Regex(
            @"(?<![a-záéíóúñ])((?:[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)(?:\s+(?:de|del|la|el|los|las|y|e|en|a|con|para|por|[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)){1,8})(?=(?:[A-ZÁÉÍÓÚÑ][a-záéíóúñ]{3,}|\d{1,2}\.\d+))",
            RegexOptions.None);

        foreach (Match m in jammedTitleRegex.Matches(textoCompleto))
        {
            var titulo = m.Groups[1].Value.Trim();
            if (titulo.Length < 10 || titulo.Length > 80) continue;
            if (resultados.Keys.Any(pos => Math.Abs(pos - m.Index) <= 10)) continue;

            // No tratar como sección principal si viene justo después de un subencabezado "5.4 ".
            var prefijo = textoCompleto.Substring(Math.Max(0, m.Index - 8), Math.Min(8, m.Index));
            if (Regex.IsMatch(prefijo, @"\d{1,2}\.\d+\.?\s*$"))
                continue;

            resultados[m.Index] = titulo;
        }

        // Títulos en líneas propias
        var tituloSinNumeroRegex = new Regex(
            @"(?:^|(?<=\n))\s{0,2}([A-ZÁÉÍÓÚÑ][a-záéíóúñ]+(?:\s+(?:de|del|la|el|los|las|y|e|en|a|con|para|por|[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)){1,8})\s*(?:\n|$)",
            RegexOptions.Multiline);

        foreach (Match titulo in tituloSinNumeroRegex.Matches(textoCompleto))
        {
            var tituloTexto = titulo.Groups[1].Value.Trim();
            if (tituloTexto.Length < 10 || tituloTexto.Length > 80) continue;
            if (resultados.Keys.Any(pos => Math.Abs(pos - titulo.Index) <= 10)) continue;
            resultados[titulo.Index] = tituloTexto;
        }

        return resultados.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    /// <summary>
    /// Normaliza un título quitando artículos y conectores para matching flexible
    /// ("bienestar y la ergonomia" ≈ "Bienestar y Ergonomia").
    /// </summary>
    public static string NormalizarTituloParaBusqueda(string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return string.Empty;
        var t = QuitarAcentos(titulo.Trim()).ToLowerInvariant();
        t = Regex.Replace(t, @"\b(el|la|los|las|un|una|de|del|y|e|en|a|al|con|por|para)\b", " ");
        t = Regex.Replace(t, @"[^a-z0-9]+", " ");
        t = Regex.Replace(t, @"\s{2,}", " ").Trim();
        return t;
    }

    private static int BuscarTituloEnTexto(string texto, string titulo)
    {
        if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(titulo))
            return -1;

        // Coincidencia exacta (ignorando acentos)
        var textoNorm = QuitarAcentos(texto);
        var tituloNorm = QuitarAcentos(titulo.Trim());
        int idx = textoNorm.IndexOf(tituloNorm, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return idx;

        // Coincidencia flexible sin artículos: "bienestar y la ergonomia" → "bienestar ergonomia"
        var tituloFlex = NormalizarTituloParaBusqueda(titulo);
        var tituloFlexWords = tituloFlex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tituloFlexWords.Length >= 2)
        {
            var lower = textoNorm.ToLowerInvariant();
            // Buscar todas las palabras clave del título en una ventana corta (encabezado)
            int searchFrom = 0;
            while (searchFrom < lower.Length)
            {
                int first = lower.IndexOf(tituloFlexWords[0], searchFrom, StringComparison.Ordinal);
                if (first < 0) break;

                int cursor = first + tituloFlexWords[0].Length;
                bool allNear = true;
                for (int wi = 1; wi < tituloFlexWords.Length; wi++)
                {
                    int p = lower.IndexOf(tituloFlexWords[wi], cursor, StringComparison.Ordinal);
                    if (p < 0 || p - first > Math.Max(titulo.Length + 20, 80))
                    {
                        allNear = false;
                        break;
                    }
                    cursor = p + tituloFlexWords[wi].Length;
                }
                if (allNear)
                    return first;

                searchFrom = first + 1;
            }
        }

        // Coincidencia por keywords del título en orden cercano
        var kws = ExtraerKeywords(titulo);
        if (kws.Count < 2)
            return -1;

        var lower2 = textoNorm.ToLowerInvariant();
        int firstKw = -1;
        int lastKw = -1;
        foreach (var k in kws)
        {
            int p = lower2.IndexOf(k.ToLowerInvariant(), StringComparison.Ordinal);
            if (p < 0) return -1;
            if (firstKw < 0 || p < firstKw) firstKw = p;
            if (p > lastKw) lastKw = p;
        }

        // Las keywords del título deben estar cerca (mismo encabezado).
        if (firstKw >= 0 && lastKw - firstKw <= Math.Max(titulo.Length + 10, 60))
            return firstKw;

        return -1;
    }

    private static string RepararSaltosSecciones(string texto)
    {
if (string.IsNullOrWhiteSpace(texto))
            return texto;

        // Insertar salto de línea después de encabezados de sección numerados (ej: "6. Capacitacion ObligatoriaSeguridad")
        var regex = new Regex(@"(\d{1,2}\.\s+[A-ZÁÉÍÓÚÑ][A-Za-záéíóúñÁÉÍÓÚÑ\s]{3,80})([A-ZÁÉÍÓÚÑ])", RegexOptions.None);
        return regex.Replace(texto, "$1\n$2");
    }
}
