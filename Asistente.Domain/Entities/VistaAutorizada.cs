using System;

namespace Asistente.Domain.Entities;

public class VistaAutorizada
{
    public int IdVista { get; set; }
    public int IdConexion { get; set; }
    public string NombreVista { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activa { get; set; } = true;

    // Navigation properties
    public ConexionBaseDatos? Conexion { get; set; }
}
