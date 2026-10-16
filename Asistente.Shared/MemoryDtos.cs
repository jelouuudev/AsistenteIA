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
    public int? IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string? Motivo { get; set; }
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
}
