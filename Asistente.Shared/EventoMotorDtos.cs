using System.Collections.Generic;

namespace Asistente.Shared;

// ---- Evento Empresarial ----
public class EventoEmpresarialDto
{
    public int IdEvento { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = "Sistema";
    public bool Activo { get; set; }
    public System.DateTime FechaCreacion { get; set; }
    public int CantidadReglas { get; set; }
}

public class CrearEventoEmpresarialRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = "Sistema";
    public bool Activo { get; set; } = true;
    public int UsuarioCreacion { get; set; } = 1;
}

public class ActualizarEventoEmpresarialRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = "Sistema";
    public bool Activo { get; set; }
}

// ---- Regla de Evento ----
public class ReglaEventoDto
{
    public int IdRegla { get; set; }
    public int IdEvento { get; set; }
    public string NombreEvento { get; set; } = string.Empty;
    public int IdWorkflow { get; set; }
    public string NombreWorkflow { get; set; } = string.Empty;
    public string? Condicion { get; set; }
    public int Prioridad { get; set; }
    public bool Activa { get; set; }
}

public class CrearReglaEventoRequest
{
    public int IdEvento { get; set; }
    public int IdWorkflow { get; set; }
    public string? Condicion { get; set; }
    public int Prioridad { get; set; } = 1;
    public bool Activa { get; set; } = true;
}

public class ActualizarReglaEventoRequest
{
    public int IdWorkflow { get; set; }
    public string? Condicion { get; set; }
    public int Prioridad { get; set; } = 1;
    public bool Activa { get; set; }
}

// ---- Evento Procesado ----
public class EventoProcesadoDto
{
    public int IdEventoProcesado { get; set; }
    public int IdEvento { get; set; }
    public string CodigoEvento { get; set; } = string.Empty;
    public string NombreEvento { get; set; } = string.Empty;
    public System.DateTime FechaHora { get; set; }
    public string Estado { get; set; } = "Pendiente";
    public string? Resultado { get; set; }
    /// <summary>Datos JSON con los que se disparó el evento (lo que el evento fue realmente).</summary>
    public string? ContextoDisparo { get; set; }
    public long TiempoProcesamiento { get; set; }
    public int? IdRegla { get; set; }
    public int? IdWorkflow { get; set; }
    public string? NombreWorkflow { get; set; }
}

// ---- Disparador de Evento (origen configurable 100% desde UI) ----
public class DisparadorEventoDto
{
    public int IdDisparador { get; set; }
    public int IdEvento { get; set; }
    public string NombreEvento { get; set; } = string.Empty;
    public string CodigoEvento { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public System.DateTime? UltimaEjecucion { get; set; }
    public System.DateTime? ProximaEjecucion { get; set; }
}

public class CrearDisparadorEventoRequest
{
    public int IdEvento { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public bool Activo { get; set; } = true;
}

public class ActualizarDisparadorEventoRequest
{
    public string Tipo { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public bool Activo { get; set; }
}

// ---- Tarea Programada ----
public class TareaProgramadaDto
{
    public int IdTarea { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ExpresionCron { get; set; } = string.Empty;
    public int IdWorkflow { get; set; }
    public string NombreWorkflow { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public System.DateTime? UltimaEjecucion { get; set; }
    public System.DateTime? ProximaEjecucion { get; set; }
}

public class CrearTareaProgramadaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string ExpresionCron { get; set; } = string.Empty;
    public int IdWorkflow { get; set; }
    public bool Activa { get; set; } = true;
    public int UsuarioCreacion { get; set; } = 1;
}

public class ActualizarTareaProgramadaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string ExpresionCron { get; set; } = string.Empty;
    public int IdWorkflow { get; set; }
    public bool Activa { get; set; }
}

// ---- Configuración del Motor de Eventos ----
public class ConfiguracionEventoMotorDto
{
    public int ReintentosMaximos { get; set; }
    public int IntervaloReintentoMs { get; set; }
    public int TiempoMaximoEventoMs { get; set; }
    public int EventosSimultaneosMax { get; set; }
    public int FrecuenciaProcesadorMs { get; set; }
}

// ---- Solicitud para disparar un evento manualmente (caso de prueba / webhook interno) ----
public class DispararEventoRequest
{
    public string CodigoEvento { get; set; } = string.Empty;
    public string? ContextoJson { get; set; }
}

// ---- Panel de Monitoreo ----
public class MonitoreoEventosDto
{
    public int TotalEventos { get; set; }
    public int EventosActivos { get; set; }
    public int TotalReglas { get; set; }
    public int ReglasActivas { get; set; }
    public int TotalEventosProcesados { get; set; }
    public int EventosExitosos { get; set; }
    public int EventosConError { get; set; }
    public int EventosReintentando { get; set; }
    public int TotalTareas { get; set; }
    public int TareasActivas { get; set; }
    public List<EventoProcesadoDto> UltimosEventosProcesados { get; set; } = new();
    public List<ReglaEventoDto> ReglasActivasDetalle { get; set; } = new();
    public List<TareaProgramadaDto> TareasDetalle { get; set; } = new();
}
