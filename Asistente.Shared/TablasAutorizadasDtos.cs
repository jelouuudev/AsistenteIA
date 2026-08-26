using System.Collections.Generic;

namespace Asistente.Shared;

public class TablaAutorizadaDto
{
    public int IdTabla { get; set; }
    public int IdConexion { get; set; }
    public string NombreTabla { get; set; } = string.Empty;
    public string Esquema { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activa { get; set; }
}

public class VistaAutorizadaDto
{
    public int IdVista { get; set; }
    public int IdConexion { get; set; }
    public string NombreVista { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activa { get; set; }
}

public class TablaAutorizadaRequest
{
    public string NombreTabla { get; set; } = string.Empty;
    public string Esquema { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class VistaAutorizadaRequest
{
    public string NombreVista { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class EsquemaObjetoDto
{
    public string Esquema { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public List<ColumnaEsquemaDto> Columnas { get; set; } = new();
}

public class ColumnaEsquemaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string TipoDato { get; set; } = string.Empty;
    public int? Longitud { get; set; }
    public bool Anulable { get; set; }
    public bool EsClavePrimaria { get; set; }
    public bool EsClaveForanea { get; set; }
}

public class RelacionEsquemaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string TablaOrigen { get; set; } = string.Empty;
    public string ColumnaOrigen { get; set; } = string.Empty;
    public string TablaDestino { get; set; } = string.Empty;
    public string ColumnaDestino { get; set; } = string.Empty;
}

public class EsquemaBaseDatosDto
{
    public string BaseDatos { get; set; } = string.Empty;
    public List<EsquemaObjetoDto> Tablas { get; set; } = new();
    public List<EsquemaObjetoDto> Vistas { get; set; } = new();
    public List<RelacionEsquemaDto> Relaciones { get; set; } = new();
}
