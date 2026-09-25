namespace Asistente.Shared;

public class HerramientaDto
{
    public int IdHerramienta { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public bool RequierePermiso { get; set; }
    public DateTime FechaRegistro { get; set; }
    public int TotalEjecuciones { get; set; }
    public double TiempoPromedioMs { get; set; }
    public int Errores { get; set; }
    public DateTime? UltimaEjecucion { get; set; }
    public bool AsociadaAlAsistente { get; set; }
}

public class CrearHerramientaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Utilidad";
    public bool Activa { get; set; } = true;
    public bool RequierePermiso { get; set; } = true;
}

public class ActualizarHerramientaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public bool RequierePermiso { get; set; }
}

public class EjecucionHerramientaDto
{
    public int IdEjecucion { get; set; }
    public int IdHerramienta { get; set; }
    public string CodigoHerramienta { get; set; } = string.Empty;
    public string NombreHerramienta { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public string Parametros { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public long TiempoEjecucion { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ConfiguracionOrchestratorDto
{
    public bool Habilitado { get; set; }
    public int Prioridad { get; set; }
    public int TiempoMaximoEjecucionMs { get; set; }
    public int MaxEjecucionesSimultaneas { get; set; }
    public bool RequiereAutorizacion { get; set; }
    public int MaxTiempoTotalMs { get; set; } = 120000;
}

public class HerramientaUsoChatDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public long TiempoMs { get; set; }
    public string? Mensaje { get; set; }
}
