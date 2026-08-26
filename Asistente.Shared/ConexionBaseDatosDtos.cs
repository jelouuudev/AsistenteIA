using System;
using System.Collections.Generic;

namespace Asistente.Shared;

public class ConexionBaseDatosDto
{
    public int IdConexion { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Servidor { get; set; } = string.Empty;
    public string BaseDatos { get; set; } = string.Empty;
    public string UsuarioConexion { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public DateTime FechaRegistro { get; set; }
    public int TotalTablasAutorizadas { get; set; }
    public int TotalVistasAutorizadas { get; set; }
    public int TotalConsultasEjecutadas { get; set; }
}

public class CrearConexionBaseDatosRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Servidor { get; set; } = string.Empty;
    public string BaseDatos { get; set; } = string.Empty;
    public string? UsuarioConexion { get; set; }
    public string? Contrasena { get; set; }
    public bool AutenticacionWindows { get; set; } = true;
}

public class ActualizarConexionBaseDatosRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Servidor { get; set; } = string.Empty;
    public string BaseDatos { get; set; } = string.Empty;
    public string? UsuarioConexion { get; set; }
    public string? Contrasena { get; set; }
    public bool AutenticacionWindows { get; set; } = true;
    public bool Activa { get; set; } = true;
}

public class ProbarConexionRequest
{
    public string Servidor { get; set; } = string.Empty;
    public string BaseDatos { get; set; } = string.Empty;
    public string? UsuarioConexion { get; set; }
    public string? Contrasena { get; set; }
    public bool AutenticacionWindows { get; set; } = true;
}

public class ConexionPruebaResultadoDto
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
