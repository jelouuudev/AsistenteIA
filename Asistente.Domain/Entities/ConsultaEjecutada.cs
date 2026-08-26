using System;

namespace Asistente.Domain.Entities;

public class ConsultaEjecutada
{
    public int IdConsulta { get; set; }
    public int IdUsuario { get; set; }
    public int IdConexion { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    public string PreguntaUsuario { get; set; } = string.Empty;
    public string OperacionEjecutada { get; set; } = string.Empty;
    public string ConsultaGenerada { get; set; } = string.Empty;
    public long TiempoEjecucion { get; set; }
    public int CantidadRegistros { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Resultado { get; set; }

    // Navigation properties
    public Usuario? Usuario { get; set; }
    public ConexionBaseDatos? Conexion { get; set; }
}
