using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

/// <summary>
/// Ámbito de memoria (ETAPA 21): Conversacional | Usuario | Agente |
/// Organizacion | Workflow. Catálogo vivo: las resoluciones se hacen contra
/// estos códigos, nunca contra listas fijas en código.
/// </summary>
public class MemoryScope
{
    public int IdScope { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<MemoryRecord> Memorias { get; set; } = new List<MemoryRecord>();
    public ICollection<MemoryRetentionPolicy> Politicas { get; set; } = new List<MemoryRetentionPolicy>();
}

/// <summary>Categoría de memoria (FormatoReporte, Definicion, Preferencia, Hecho...).</summary>
public class MemoryCategory
{
    public int IdCategory { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<MemoryRecord> Memorias { get; set; } = new List<MemoryRecord>();
}

/// <summary>
/// Registro de memoria institucional u operativa (ETAPA 21). El contenido actual
/// vive desnormalizado para lectura rápida; la historia completa está en
/// MemoryVersion (append-only). VectorId enlaza su embedding en ChromaDB.
/// </summary>
public class MemoryRecord
{
    public int IdMemory { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public int IdScope { get; set; }
    public int? IdPropietario { get; set; }
    public int? IdCategory { get; set; }
    public bool Activo { get; set; } = true;
    public int VersionActual { get; set; } = 1;
    /// <summary>Si exige permiso MEMORIA:{Codigo} para leer/escribir.</summary>
    public bool RequierePermiso { get; set; } = true;
    public Guid? VectorId { get; set; }
    public bool Indexada { get; set; } = false;
    public DateTime? FechaExpiracion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public MemoryScope? Scope { get; set; }
    public MemoryCategory? Category { get; set; }
    public ICollection<MemoryVersion> Versiones { get; set; } = new List<MemoryVersion>();
}

/// <summary>
/// Versión inmutable de una memoria. Crear genera la v1; cada actualización y
/// cada reversión agregan una versión nueva con el contenido vigente. Nunca se
/// reescribe historia: revertir = nueva versión copiando la elegida.
/// </summary>
public class MemoryVersion
{
    public int IdVersion { get; set; }
    public int IdMemory { get; set; }
    public int Numero { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public int? IdUsuario { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public MemoryRecord? Memory { get; set; }
}

/// <summary>
/// Auditoría de memoria: quién (usuario/agente), qué memoria (id + código, el
/// código sobrevive a purgas), operación, resultado y motivo si aplica.
/// </summary>
public class MemoryAccessLog
{
    public int IdLog { get; set; }
    public int? IdMemory { get; set; }
    public string Codigo { get; set; } = string.Empty;
    /// <summary>Tipo de memoria (ámbito: Conversacional, Usuario, Agente,
    /// Organizacion, Workflow). Se copia aquí y no se deduce por IdMemory porque
    /// al purgar una memoria IdMemory queda en null y solo sobrevive el código:
    /// el ámbito se perdería justo en el caso donde más importa la trazabilidad.</summary>
    public string? Ambito { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    /// <summary>Duración en milisegundos cuando la operación la tiene (búsqueda
    /// semántica, composición de contexto). Es lo que alimenta la métrica de
    /// tiempo de recuperación del panel.</summary>
    public int? DuracionMs { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Política de retención: días de vida (por ámbito o global si Ambito es null),
/// qué hacer al expirar (desactivar o eliminar) y si la purga conserva trazabilidad.
/// </summary>
public class MemoryRetentionPolicy
{
    public int IdPolicy { get; set; }
    public int? IdScope { get; set; }
    public int DiasRetencion { get; set; } = 365;
    public bool EliminarAlExpirar { get; set; } = false;
    public bool ConservarTrazabilidad { get; set; } = true;
    public bool Activo { get; set; } = true;

    public MemoryScope? Scope { get; set; }
}
