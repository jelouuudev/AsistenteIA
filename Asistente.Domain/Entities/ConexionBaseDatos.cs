using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

public class ConexionBaseDatos
{
    public int IdConexion { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Servidor { get; set; } = string.Empty;
    public string BaseDatos { get; set; } = string.Empty;
    public string? UsuarioConexion { get; set; }
    public string CadenaConexionCifrada { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<TablaAutorizada> TablasAutorizadas { get; set; } = new List<TablaAutorizada>();
    public ICollection<VistaAutorizada> VistasAutorizadas { get; set; } = new List<VistaAutorizada>();
    public ICollection<ConsultaEjecutada> ConsultasEjecutadas { get; set; } = new List<ConsultaEjecutada>();
}
