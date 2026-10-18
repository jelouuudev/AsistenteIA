using System;
using System.Collections.Generic;

namespace Asistente.Shared;

/// <summary>DTOs de Memoria Empresarial (ETAPA 21). Los secretos no aplican aquí;
/// lo sensible son los permisos: el contenido solo se entrega con autorización.</summary>
public class CrearMemoryRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string Ambito { get; set; } = "Organizacion";
    public int? IdPropietario { get; set; }
    public string? Categoria { get; set; }
    public bool RequierePermiso { get; set; } = true;
    public int? DiasRetencion { get; set; }
    public DateTime? FechaExpiracion { get; set; }
}

public class ActualizarMemoryRequest
{
    public string Contenido { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}

public class MemoryDto
{
    public int IdMemory { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string Ambito { get; set; } = string.Empty;
    public int? IdPropietario { get; set; }
    public string? Categoria { get; set; }
    public bool Activo { get; set; }
    public int VersionActual { get; set; }
    public bool RequierePermiso { get; set; }
    public bool Indexada { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

public class MemoryVersionDto
{
    public int Numero { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime Fecha { get; set; }
}

public class MemoryAccessLogDto
{
    public string Codigo { get; set; } = string.Empty;
    /// <summary>Tipo de memoria (ámbito). Sobrevive a la purga, cuando IdMemory no.</summary>
    public string? Ambito { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public int? DuracionMs { get; set; }
    public DateTime Fecha { get; set; }
}

public class MemorySearchHitDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Fragmento { get; set; } = string.Empty;
    public string Ambito { get; set; } = string.Empty;
    public double Puntaje { get; set; }
}

public class MemoryMetricasDto
{
    public long TotalActivas { get; set; }
    public System.Collections.Generic.Dictionary<string, long> PorAmbito { get; set; } = new();
    public long Versiones { get; set; }
    public long Accesos { get; set; }
    public long ExpiradasPendientes { get; set; }

    /// <summary>Memorias distintas devueltas por las búsquedas (observabilidad).</summary>
    public long MemoriasConsultadas { get; set; }

    /// <summary>Duración media de una búsqueda semántica, en milisegundos.</summary>
    public double? TiempoRecuperacionMs { get; set; }

    /// <summary>Accesos por agente. La clave 0 agrupa los accesos sin agente.</summary>
    public System.Collections.Generic.Dictionary<int, long> UsoPorAgente { get; set; } = new();

    /// <summary>Accesos por usuario. La clave 0 agrupa los accesos sin usuario.</summary>
    public System.Collections.Generic.Dictionary<int, long> UsoPorUsuario { get; set; } = new();

    /// <summary>Distribución de tipos de memoria leídos, por ámbito.</summary>
    public System.Collections.Generic.Dictionary<string, long> AccesosPorTipo { get; set; } = new();
}
