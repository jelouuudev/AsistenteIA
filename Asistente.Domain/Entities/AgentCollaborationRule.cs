using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Regla de colaboración entre agentes (ETAPA 17, Actividad 3).
/// Define qué agente origen puede (o no) invocar colaborativamente a qué
/// agente destino, con una prioridad para resolver conflictos.
/// Regla 3: Solo podrán colaborar agentes autorizados.
/// </summary>
public class AgentCollaborationRule
{
    public int IdRule { get; set; }
    public int AgenteOrigen { get; set; }
    public int AgenteDestino { get; set; }
    public bool Permitido { get; set; }
    public int Prioridad { get; set; } = 100;
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? UsuarioCreacion { get; set; }

    public Asistente? Origen { get; set; }
    public Asistente? Destino { get; set; }
}
