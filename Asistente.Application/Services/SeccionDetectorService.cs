using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Asistente.Application.Services;

public static class SeccionDetectorService
{
    private static readonly Dictionary<string, List<string>> SeccionesPatrones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Roles y Responsabilidades"] = new()
        {
            "roles y responsabilidades",
            "roles y responsabilidad",
            "responsabilidades del",
            "rol de rrhh",
            "buddy asignado",
            "manager directo"
        },
        ["Checklist Rapido del Manager"] = new()
        {
            "checklist rapido del manager",
            "checklist rapido del gestor",
            "checklist del manager",
            "checklist del gestor",
            "checklist rapido",
            "checklist del primer dia",
            "checklist del primer día"
        },
        ["Proceso de Incorporacion"] = new()
        {
            "proceso de incorporacion",
            "proceso de incorporación",
            "pre-ingreso",
            "primer dia",
            "primer día",
            "primera semana",
            "primeros 30 dias",
            "primeros 30 días"
        },
        ["Accesos y Herramientas"] = new()
        {
            "accesos y herramientas",
            "herramientas obligatorias",
            "niveles de acceso"
        },
        ["Capacitacion Obligatoria"] = new()
        {
            "capacitacion obligatoria",
            "capacitación obligatoria"
        },
        ["Evaluacion del Periodo de Prueba"] = new()
        {
            "evaluacion del periodo",
            "evaluación del periodo",
            "periodo de prueba",
            "período de prueba"
        },
        ["Flujo de Procesamiento Documental"] = new()
        {
            "flujo de procesamiento",
            "flujo documental",
            "procesamiento documental",
            "cómo se procesan",
            "como se procesan",
            "pasos del procesamiento",
            "pasos de procesamiento",
            "paso a paso.*procesamiento",
            "proceso de procesar",
            "etapas del procesamiento"
        },
        ["Instalación y Configuración"] = new()
        {
            "instalación y configuración",
            "instalacion y configuracion",
            "instalación",
            "instalacion",
            "cómo instalar",
            "como instalar",
            "pasos de instalación",
            "pasos de instalacion",
            "paso a paso.*instal",
            "setup",
            "iniciar la aplicación",
            "iniciar la aplicacion"
        },
        ["Requisitos del Sistema"] = new()
        {
            "requisitos del sistema",
            "requisitos de hardware",
            "requisitos de software",
            "requisitos del cliente"
        },
        ["Arquitectura de la Plataforma"] = new()
        {
            "arquitectura de la plataforma",
            "arquitectura del sistema",
            "arquitectura de la aplicacion",
            "arquitectura de la aplicación",
            "arquitectura"
        },
        ["Uso Diario del Desarrollador"] = new()
        {
            "uso diario del desarrollador",
            "uso diario de desarrollador",
            "uso diario",
            "crear un repositorio",
            "configurar un pipeline",
            "realizar un despliegue",
            "gestion de ramas",
            "gestión de ramas"
        },
        ["Resolución de Problemas"] = new()
        {
            "resolución de problemas",
            "resolucion de problemas",
            "errores comunes",
            "troubleshooting",
            "diagnóstico",
            "diagnostico"
        },
        ["Seguridad"] = new()
        {
            "seguridad y autenticación",
            "seguridad y autenticacion",
            @"\bseguridad\b",
            "cifrado de datos",
            "auditoria y cumplimiento",
            "auditoría y cumplimiento"
        },
        ["Costos y Licenciamiento"] = new()
        {
            "costos y licenciamiento",
            "licenciamiento",
            "planes de licenciamiento",
            "plan starter",
            "plan enterprise"
        },
        ["Glosario de Términos"] = new()
        {
            "glosario de terminos",
            "glosario de términos",
            "glosario"
        },
        ["Soporte y Contacto"] = new()
        {
            "soporte y contacto",
            "soporte tecnico",
            "soporte técnico"
        },
        ["Productividad y Seguimiento"] = new()
        {
            "productividad y seguimiento",
            "productividad en teletrabajo",
            "seguimiento teletrabajo",
            "indicadores de productividad",
            "revision de desempeno",
            "revisión de desempeño",
            "plan de mejora"
        },
        ["Bienestar y Ergonomia"] = new()
        {
            "bienestar y ergonomia",
            "bienestar y ergonomía",
            "bienestar y la ergonomia",
            "bienestar y la ergonomía",
            "bienestar",
            "ergonomia",
            "ergonomía",
            "silla ergonomica",
            "silla ergonómica",
            "jornada de bienestar",
            "evaluacion postural",
            "evaluación postural"
        },
        ["Gestion de Incidencias"] = new()
        {
            "gestion de incidencias",
            "gestión de incidencias",
            "incidencias teletrabajo",
            "problema tecnico teletrabajo",
            "problema técnico teletrabajo"
        },
        ["Estructura y Componentes"] = new()
        {
            "estructura del sistema",
            "componentes principales",
            "capas del sistema",
            "módulos",
            "modulos",
            "diagrama de arquitectura",
            "tecnologías usadas",
            "tecnologias usadas"
        },
        ["Configuración de Ollama"] = new()
        {
            "ollama",
            "modelo.*ia",
            "modelo de ia",
            "deepseek",
            "llama",
            "configurar ollama",
            "instalar ollama"
        },
        ["Base de Datos"] = new()
        {
            "base de datos",
            "sql server",
            "migraciones",
            "entity framework",
            "tablas",
            "schema"
        },
        ["API y Endpoints"] = new()
        {
            "api",
            "endpoints",
            "rutas",
            "controllers",
            "controladores",
            "métodos http",
            "metodos http"
        },
        ["Seguridad y Autenticación"] = new()
        {
            "autenticación",
            "autenticacion",
            "authorization",
            "jwt",
            "tokens",
            "login",
            "registro"
        },
        ["Embeddings y Búsqueda Vectorial"] = new()
        {
            "embeddings",
            "búsqueda vectorial",
            "busqueda vectorial",
            "chromadb",
            "vector store",
            "similitud"
        },
        ["Procedimiento ante Incidentes de Seguridad"] = new()
        {
            "procedimiento ante incidentes",
            "incidente de seguridad",
            "incidentes de seguridad",
            "pasos del procedimiento",
            "qué hacer.*incidente",
            "que hacer.*incidente",
            "protocolo de incidentes",
            "respuesta a incidentes",
            "manejo de incidentes"
        },
        ["Política de Contraseñas"] = new()
        {
            "política de contraseñas",
            "politica de contrasenas",
            "política de contrasenas",
            "contraseñas",
            "contrasenas",
            "requisitos.*contraseña",
            "requisitos.*contrasena",
            "cambiar contraseña",
            "cambiar contrasena",
            "gestor de contraseñas",
            "gestor de contrasenas"
        },
        ["Clasificación de la Información"] = new()
        {
            "clasificación de la información",
            "clasificacion de la informacion",
            "clasificación de la informacion",
            "clasificacion de la información",
            "clasificar información",
            "clasificar informacion",
            "categorías.*información",
            "categorias.*informacion",
            "confidencial",
            "interna",
            "pública",
            "publica"
        },
        ["Control de Acceso"] = new()
        {
            "control de acceso",
            "acceso a sistemas",
            "autenticación multifactor",
            "autenticacion multifactor",
            "2fa",
            "menor privilegio",
            "revisión de accesos",
            "revision de accesos"
        },
        ["Copia de Seguridad"] = new()
        {
            "copia de seguridad",
            "copias de seguridad",
            "backup",
            "respaldo",
            "restauración",
            "restauracion"
        },
        ["Uso Aceptable de Recursos"] = new()
        {
            "uso aceptable",
            "recursos de ti",
            "recursos de TI",
            "software no autorizado",
            "dispositivos personales"
        },
        ["Sanciones por Incumplimiento"] = new()
        {
            "sanciones",
            "incumplimiento",
            "amonestación",
            "amonestacion",
            "suspensión temporal",
            "suspension temporal",
            "terminación del contrato",
            "terminacion del contrato"
        },
        ["Información de Contacto"] = new()
        {
            "información de contacto",
            "informacion de contacto",
            "datos de contacto",
            "dato de contacto",
            "contacto",
            "correo.*seguridad",
            "teléfono.*seguridad",
            "telefono.*seguridad",
            @"ext\.\s*5555",
            "emergencia.*seguridad"
        },
        ["Monitoreo y Auditoría"] = new()
        {
            "monitoreo",
            "auditoría",
            "auditoria",
            "panel de monitoreo",
            "panel de control",
            "logs",
            "serilog",
            "registro de acciones",
            "bitácora",
            "bitacora",
            "tasa de éxito",
            "tasa de exito",
            "chunks generados"
        },
        ["Despliegue y CI/CD"] = new()
        {
            "despliegue y ci/cd",
            "ci/cd",
            "cicd",
            "integracion continua",
            "integración continua",
            "despliegue continuo",
            "github actions",
            "deploy",
            "deployment"
        },
        ["Estándares de Código"] = new()
        {
            "estandares de codigo",
            "estándares de código",
            "convenciones de nomenclatura",
            "reglas de formato"
        },
        ["Control de Versiones con Git"] = new()
        {
            "control de versiones",
            "estrategia de ramas",
            "conventional commits"
        },
        ["Revisión de Código"] = new()
        {
            "revision de codigo",
            "revisión de código",
            "code review",
            "criterios de aprobación",
            "criterios de aprobacion"
        },
        ["Pruebas de Software"] = new()
        {
            "pruebas de software",
            "pruebas unitarias",
            "niveles de prueba"
        },
        ["Documentación Técnica"] = new()
        {
            "documentacion tecnica",
            "documentación técnica"
        },
        ["Gestión de Dependencias"] = new()
        {
            "gestion de dependencias",
            "gestión de dependencias",
            "politica de dependencias",
            "política de dependencias"
        },
        ["Metodología Ágil - Scrum"] = new()
        {
            "metodologia agil",
            "metodología ágil",
            "scrum",
            "sprint planning",
            "daily standup"
        },
        ["Preguntas Frecuentes"] = new()
        {
            "preguntas frecuentes",
            "faq"
        },
        // Nota: "Productividad y Seguimiento" ya está definida arriba con patrones más específicos.
        // No duplicar con patrones genéricos ("productividad"/"seguimiento") que roban otras secciones.
    };

    /// <summary>
    /// Prefijos conversacionales a quitar para obtener el tema/sección pedida.
    /// </summary>
    private static readonly string[] PrefijosConversacionales =
    {
        @"^ahora\s+",
        @"^seg[uú]n\s+(el\s+)?manual(\s+de|\s+del)?\s+\w+[,:\s]+",
        @"^seg[uú]n\s+(el\s+)?manual(\s+del\s+producto)?[,:\s]+",
        @"^seg[uú]n\s+(el\s+)?documento[,:\s]+",
        @"^seg[uú]n\s+(la\s+)?gu[ií]a(\s+de\s+\w+)?[,:\s]+",
        @"^de\s+acuerdo\s+(al|con\s+el)\s+manual(\s+\w+)?[,:\s]+",
        @"^en\s+(el\s+)?manual(\s+\w+)?[,:\s]+",
        @"^(h[aá]blame|cu[eé]ntame|dime|expl[ií]came|descr[ií]beme)\s+(sobre|de|del|acerca\s+de|los|las|el|la)\s+",
        @"^(h[aá]blame|cu[eé]ntame|dime|expl[ií]came)\s+",
        @"^(quiero\s+saber|necesito\s+saber|me\s+interes[aá])\s+(sobre|de|del)?\s*",
        @"^(qu[eé]\s+dice|qu[eé]\s+establece)\s+(el\s+manual\s+sobre|sobre|de|del)\s+",
        @"^(informaci[oó]n\s+sobre|datos\s+de|acerca\s+de)\s+",
        @"^del\s+",
        @"^de\s+(la|el|los|las)\s+",
        @"^sobre\s+(la|el|los|las)?\s*",
        @"^la\s+secci[oó]n\s+(de\s+)?"
    };

    public static string? DetectarSeccion(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return null;

        var consultaLower = consulta.ToLowerInvariant();

        // Preferir patrones más específicos (más largos) para evitar mapeos genéricos.
        string? mejorSeccion = null;
        int mejorLongitud = -1;

        foreach (var seccion in SeccionesPatrones)
        {
            foreach (var patron in seccion.Value)
            {
                if (Regex.IsMatch(consultaLower, patron, RegexOptions.IgnoreCase) && patron.Length > mejorLongitud)
                {
                    mejorLongitud = patron.Length;
                    mejorSeccion = seccion.Key;
                }
            }
        }

        if (mejorSeccion != null)
            return mejorSeccion;

        // Fallback: usar el tema limpio de la consulta como título de sección.
        var titulo = ExtraerTituloTema(consulta);
        return string.IsNullOrWhiteSpace(titulo) || titulo.Length < 8 ? null : titulo;
    }

    /// <summary>
    /// Extrae el tema/título pedido quitando muletillas ("háblame sobre", "según el manual"...).
    /// </summary>
    public static string? ExtraerTituloTema(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return null;

        var texto = consulta.Trim();
        foreach (var prefijo in PrefijosConversacionales)
            texto = Regex.Replace(texto, prefijo, "", RegexOptions.IgnoreCase).Trim();

        texto = Regex.Replace(texto, @"[¿?¡!]+", "").Trim();
        texto = Regex.Replace(texto, @"\s{2,}", " ").Trim();

        // Quitar artículos iniciales residuales
        texto = Regex.Replace(texto, @"^(el|la|los|las|un|una)\s+", "", RegexOptions.IgnoreCase).Trim();

        if (texto.Length < 8)
            return null;

        // Evitar devolver la consulta completa si no se limpió nada útil
        if (texto.Equals(consulta.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !Regex.IsMatch(consulta, @"h[aá]blame|cu[eé]ntame|manual|secci[oó]n|sobre", RegexOptions.IgnoreCase))
            return null;

        return texto;
    }

    /// <summary>
    /// True cuando el usuario pide contenido de un documento/manual de forma literal.
    /// </summary>
    public static bool EsConsultaDocumentalLiteral(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return false;

        if (EsPreguntaDeLista(consulta))
            return true;

        if (DetectarSeccion(consulta) != null)
            return true;

        var c = consulta.ToLowerInvariant();
        return Regex.IsMatch(c, @"manual(\s+del\s+producto)?") ||
               Regex.IsMatch(c, @"h[aá]blame\s+(sobre|de|del)") ||
               Regex.IsMatch(c, @"cu[eé]ntame\s+(sobre|de|del)") ||
               Regex.IsMatch(c, @"qu[eé]\s+dice\s+(el\s+)?(manual|documento)") ||
               Regex.IsMatch(c, @"secci[oó]n\s+");
    }

    /// <summary>
    /// Palabras clave para preferir un documento concreto nombrado en la consulta.
    /// </summary>
    public static string? ExtraerPreferenciaDocumento(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return null;

        var c = SectionExtractorHelper.QuitarAcentos(consulta).ToLowerInvariant();

        // Específicos primero (más discriminativos).
        if (Regex.IsMatch(c, @"onboarding|incorporacion"))
            return "onboarding";
        if (Regex.IsMatch(c, @"cloudsync|manual\s+del\s+producto|manual\s+producto"))
            return "cloudsync";
        if (Regex.IsMatch(c, @"gu[ií]?a\s+(de\s+)?desarrollo|gu[ií]?a\s+(de\s+)?software|guia\s+desarrollo"))
            return "guia";
        if (Regex.IsMatch(c, @"pol[ií]?tica.*(seguridad|teletrabajo)|teletrabajo"))
            return "politica";

        // "según el manual onboarding" / "manual X" → token X
        var m = Regex.Match(c, @"\bmanual(?:\s+de|\s+del)?\s+([a-z0-9_]{3,})");
        if (m.Success)
        {
            var token = m.Groups[1].Value;
            if (token is not ("el" or "la" or "los" or "las" or "producto" or "del" or "de"))
                return token;
        }

        if (Regex.IsMatch(c, @"\bmanual\b"))
            return "manual";

        return null;
    }

    public static bool EsPreguntaDeLista(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return false;

        var consultaLower = consulta.ToLowerInvariant();

        var patronesLista = new[]
        {
            @"cuáles son los pasos",
            @"cuales son los pasos",
            @"qué pasos",
            @"que pasos",
            @"lista de",
            @"enumerar",
            @"mencionar.*pasos",
            @"paso [0-9]",
            @"paso 1",
            @"paso 2",
            @"paso 3",
            @"cuáles son las etapas",
            @"cuales son las etapas",
            @"cuáles son las fases",
            @"cuales son las fases",
            @"procedimiento ante",
            @"procedimiento de",
            @"instrucciones de",
            @"instrucciones para",
            @"cuál es la política",
            @"cual es la politica",
            @"cuáles son las reglas",
            @"cuales son las reglas",
            @"qué normas",
            @"que normas",
            @"preguntas frecuentes"
        };

        return patronesLista.Any(patron => Regex.IsMatch(consultaLower, patron));
    }
}
