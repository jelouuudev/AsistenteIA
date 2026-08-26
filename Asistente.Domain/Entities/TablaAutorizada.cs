using System;

namespace Asistente.Domain.Entities;

public class TablaAutorizada
{
    public int IdTabla { get; set; }
    public int IdConexion { get; set; }
    public string NombreTabla { get; set; } = string.Empty;
    public string Esquema { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activa { get; set; } = true;

    // Navigation properties
    public ConexionBaseDatos? Conexion { get; set; }
}
