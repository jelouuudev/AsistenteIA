using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

public class ConsultaPlantilla
{
    public int IdPlantilla { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? IdConexion { get; set; }
    public string ConsultaSql { get; set; } = string.Empty;
    public string? Parametros { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ConexionBaseDatos? Conexion { get; set; }
}
