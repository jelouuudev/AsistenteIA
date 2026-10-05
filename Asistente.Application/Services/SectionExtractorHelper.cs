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

    // (Diccionario de correcciones PdfPig eliminado junto con su único consumidor.)

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

        // Subsecciones: 3.1 Recursos Humanos. El lookbehind excluye separadores
        // de versión y ruta (% - / . :): sin esto, la cola de "%PDF-1.6 A partir…"
        // se releía como subsección "1.6" y el texto quedaba "%PDF" + "**1.6**",
        // perdiendo el "-1.6" (plan #13226: la lista de cabeceras se cortaba en
        // %PDF-1.5). No hay vocabulario: es una clase de caracteres.
        texto = Regex.Replace(
            texto,
            @"(?<![a-zA-Z\d%\-/.:])(\d{1,2}\.\d+)\.?\s+([A-ZÁÉÍÓÚÑ][^\n]{2,70}?)(?=\n|\d{1,2}\.\d+|[.]\s|\z)",
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

    // (Se eliminó el aparato de secciones por keywords/títulos: el RAG ahora
    // recupera por comprensión semántica. Solo queda limpieza de texto PDF.)


    // (Utilidades de keywords/secciones eliminadas: el RAG recupera por comprensión.)

    // (Fin del aparato eliminado. Cierre de clase debajo.)
}
