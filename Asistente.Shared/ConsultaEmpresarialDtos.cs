using System;
using System.Collections.Generic;

namespace Asistente.Shared;

public class ConsultaPlantillaDto
{
    public int IdPlantilla { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? IdConexion { get; set; }
    public string? NombreConexion { get; set; }
    public string ConsultaSql { get; set; } = string.Empty;
    public string? Parametros { get; set; }
    public bool Activa { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class CrearConsultaPlantillaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? IdConexion { get; set; }
    public string ConsultaSql { get; set; } = string.Empty;
    public string? Parametros { get; set; }
}

public class ActualizarConsultaPlantillaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? IdConexion { get; set; }
    public string ConsultaSql { get; set; } = string.Empty;
    public string? Parametros { get; set; }
    public bool Activa { get; set; } = true;
}

public class ConsultaEjecutadaDto
{
    public int IdConsulta { get; set; }
    public int IdUsuario { get; set; }
    public string? NombreUsuario { get; set; }
    public int IdConexion { get; set; }
    public string? NombreConexion { get; set; }
    public DateTime FechaHora { get; set; }
    public string PreguntaUsuario { get; set; } = string.Empty;
    public string OperacionEjecutada { get; set; } = string.Empty;
    public string ConsultaGenerada { get; set; } = string.Empty;
    public long TiempoEjecucion { get; set; }
    public int CantidadRegistros { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Resultado { get; set; }
}

public class EjecutarConsultaRequest
{
    public int IdConexion { get; set; }
    public int? IdPlantilla { get; set; }
    public string? ConsultaSql { get; set; }
    public Dictionary<string, object?>? Parametros { get; set; }
    public string? Pregunta { get; set; }
}

public class EjecutarConsultaResponse
{
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
    public string? Estado { get; set; }
    public long TiempoEjecucionMs { get; set; }
    public int CantidadRegistros { get; set; }
    public List<string> Columnas { get; set; } = new();
    public List<Dictionary<string, object?>> Registros { get; set; } = new();
    public string? ResumenDatos { get; set; }
}

public class DashboardConsultasDto
{
    public int TotalConsultas { get; set; }
    public int TotalCompletadas { get; set; }
    public int TotalErrores { get; set; }
    public int TotalBloqueadas { get; set; }
    public double PromedioTiempoMs { get; set; }
    public int TotalRegistros { get; set; }
    public int TotalConexionesActivas { get; set; }
    public int TotalTablasAutorizadas { get; set; }
    public int TotalVistasAutorizadas { get; set; }
    public int TotalPlantillas { get; set; }
    public List<ConsultaEjecutadaDto> UltimasConsultas { get; set; } = new();
}
