using System;
using System.Collections.Generic;

namespace Asistente.Shared;

public class FuenteConocimientoDto
{
    public int IdFuente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public int Prioridad { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int UsuarioCreacion { get; set; }
    public int TotalDocumentos { get; set; }
    public int TotalAsistentes { get; set; }
    public int TotalChunks { get; set; }
    public int TotalVectores { get; set; }
    public DateTime? FechaUltimaIndexacion { get; set; }
}

public class CrearFuenteConocimientoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = "Manual";
    public int Prioridad { get; set; } = 5;
}

public class ActualizarFuenteConocimientoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = "Manual";
    public bool Activo { get; set; } = true;
    public int Prioridad { get; set; } = 5;
}

public class AsistenteFuenteDto
{
    public int IdAsistente { get; set; }
    public int IdFuente { get; set; }
    public string? NombreAsistente { get; set; }
    public string? NombreFuente { get; set; }
    public bool Activo { get; set; }
    public int Prioridad { get; set; }
}

public class AsignarFuenteAAsistenteRequest
{
    public int IdAsistente { get; set; }
    public int IdFuente { get; set; }
    public int Prioridad { get; set; } = 5;
}

public class DocumentoFuenteDto
{
    public int IdDocumento { get; set; }
    public int IdFuente { get; set; }
    public string? NombreDocumento { get; set; }
    public string? NombreFuente { get; set; }
    public bool Activo { get; set; }
}

public class AsignarDocumentoAFuenteRequest
{
    public int IdDocumento { get; set; }
    public int IdFuente { get; set; }
}

public class DashboardFuentesDto
{
    public int TotalFuentes { get; set; }
    public int TotalFuentesActivas { get; set; }
    public int TotalDocumentosAsociados { get; set; }
    public int TotalAsistentesConFuentes { get; set; }
    public int TotalChunks { get; set; }
    public int TotalVectores { get; set; }
    public DateTime? FechaUltimaIndexacionGlobal { get; set; }
    public List<FuenteConocimientoDto> Fuentes { get; set; } = new();
}

public class ReferenciaDocumentalDto
{
    public string NombreDocumento { get; set; } = string.Empty;
    public string NombreFuente { get; set; } = string.Empty;
    public string? VersionDocumento { get; set; }
    public int? PaginaInicial { get; set; }
    public int? PaginaFinal { get; set; }
    public string? FragmentoUtilizado { get; set; }
    public float PuntajeSimilitud { get; set; }
}
