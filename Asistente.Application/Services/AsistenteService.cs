using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

/// <summary>
/// Agent Manager (ETAPA 16): administra la configuración de los agentes
/// (crear, editar, activar/desactivar, versionar, publicar, probar, duplicar).
/// </summary>
public class AsistenteService
{
    private readonly IAsistenteRepository _asistenteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AsistenteService(IAsistenteRepository asistenteRepository, IUnitOfWork unitOfWork)
    {
        _asistenteRepository = asistenteRepository;
        _unitOfWork = unitOfWork;
    }

    // ---- Consulta ----
    public virtual async Task<AsistenteDto?> ObtenerPorIdAsync(int id)
    {
        var a = await _asistenteRepository.GetByIdAsync(id);
        return a == null ? null : MapToDto(a);
    }

    public virtual async Task<IEnumerable<AsistenteDto>> ObtenerTodosAsync()
        => (await _asistenteRepository.GetAllAsync()).Select(MapToDto);

    public async Task<IEnumerable<AsistenteDto>> ObtenerAutorizadosParaUsuarioAsync(int idUsuario, IEnumerable<int> roles)
        => (await _asistenteRepository.GetAutorizadosParaUsuarioAsync(idUsuario, roles)).Select(MapToDto);

    // ---- Crear ----
    public async Task<AsistenteDto> CrearAsistenteAsync(CrearAsistenteRequest r)
    {
        var a = new Domain.Entities.Asistente
        {
            Codigo = r.Codigo,
            Nombre = r.Nombre,
            Descripcion = r.Descripcion,
            Objetivo = r.Objetivo,
            PromptSistema = r.PromptSistema,
            ModeloIA = r.ModeloIA,
            Temperatura = r.Temperatura,
            MaxTokens = r.MaxTokens,
            TimeoutSegundos = r.TimeoutSegundos,
            Idioma = r.Idioma,
            LongitudMaximaRespuesta = r.LongitudMaximaRespuesta,
            NivelFormalidad = r.NivelFormalidad,
            FormatoRespuesta = r.FormatoRespuesta,
            Restricciones = r.Restricciones,
            MensajeBienvenida = r.MensajeBienvenida,
            Estado = EstadoAgente.Activo,
            Version = 1,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        // 1) Guardar primero el agente para obtener su IdAsistente (generado por la BD).
        await _asistenteRepository.AddAsync(a);
        await _unitOfWork.SaveChangesAsync();

        // 2) Ahora que conocemos el Id, crear las asignaciones con la FK resuelta.
        AplicarAsignaciones(a, r.Fuentes, r.Herramientas, r.Workflows, r.Roles, r.Usuarios);

        // Crear versión inicial
        a.Versiones.Add(new AgenteVersion
        {
            Version = 1,
            PromptSistema = a.PromptSistema,
            ModeloIA = a.ModeloIA,
            Temperatura = a.Temperatura,
            MaxTokens = a.MaxTokens,
            Estado = EstadoAgente.Activo,
            FechaCreacion = DateTime.UtcNow,
            Configuracion = SerializarConfig(a)
        });
        _asistenteRepository.Update(a);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(a);
    }

    // ---- Actualizar ----
    public async Task<AsistenteDto> ActualizarAsistenteAsync(int id, ActualizarAsistenteRequest r)
    {
        var a = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(id))
            ?? throw new KeyNotFoundException("Agente no encontrado.");

        a.Codigo = r.Codigo;
        a.Nombre = r.Nombre;
        a.Descripcion = r.Descripcion;
        a.Objetivo = r.Objetivo;
        a.PromptSistema = r.PromptSistema;
        a.ModeloIA = r.ModeloIA;
        a.Estado = r.Estado;
        a.Activo = r.Activo;
        a.Temperatura = r.Temperatura;
        a.MaxTokens = r.MaxTokens;
        a.TimeoutSegundos = r.TimeoutSegundos;
        a.Idioma = r.Idioma;
        a.LongitudMaximaRespuesta = r.LongitudMaximaRespuesta;
        a.NivelFormalidad = r.NivelFormalidad;
        a.FormatoRespuesta = r.FormatoRespuesta;
        a.Restricciones = r.Restricciones;
        a.MensajeBienvenida = r.MensajeBienvenida;
        a.FechaModificacion = DateTime.UtcNow;

        // Las asignaciones directas a usuarios NO se gestionan en el form de editar
        // asistente (solo Fuentes/Herramientas/Workflows/Roles): se conservan para que
        // guardar no las borre en silencio. Se administran en Seguridad → AsignarAsistentes.
        var usuariosActuales = a.UsuariosAsistentes?.Where(x => x.Activo).Select(x => x.IdUsuario).ToList()
            ?? new List<int>();
        var usuarios = (r.Usuarios != null && r.Usuarios.Any()) ? r.Usuarios : usuariosActuales;

        // Eliminar las asignaciones actuales (de BD y del change tracker) para evitar
        // el conflicto de tracking al reinsertarlas (Regla: una sola instancia por clave).
        await _asistenteRepository.EliminarAsignacionesAsync(a.IdAsistente);
        // Romper la referencia a las colecciones hijas trackeadas (ya removidas arriba).
        a.AsistentesFuentes = null!;
        a.AsistentesHerramientas = null!;
        a.AgentesWorkflows = null!;
        a.AgentesRoles = null!;
        a.UsuariosAsistentes = null!;

        // Sincronizar asignaciones (recrear activas con IdAsistente resuelto)
        SincronizarAsignaciones(a, r.Fuentes, r.Herramientas, r.Workflows, r.Roles, usuarios);

        _asistenteRepository.Update(a);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(a);
    }

    // ---- Activar / Desactivar (Regla 7) ----
    public async Task ActivarAsistenteAsync(int id)
    {
        var a = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(id))
            ?? throw new KeyNotFoundException("Agente no encontrado.");
        a.Activo = true;
        _asistenteRepository.Update(a);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DesactivarAsistenteAsync(int id)
    {
        var a = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(id))
            ?? throw new KeyNotFoundException("Agente no encontrado.");
        a.Activo = false;
        _asistenteRepository.Update(a);
        await _unitOfWork.SaveChangesAsync();
    }

    // ---- Ciclo de publicación (Actividad 10) ----
    public async Task CambiarEstadoAsync(int id, EstadoAgente estado, string? usuario = null)
    {
        var a = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(id))
            ?? throw new KeyNotFoundException("Agente no encontrado.");
        a.Estado = estado;
        a.Activo = (estado == EstadoAgente.Activo);
        a.FechaModificacion = DateTime.UtcNow;
        _asistenteRepository.Update(a);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ActivarAsync(int id, string? usuario = null)
        => await CambiarEstadoAsync(id, EstadoAgente.Activo, usuario);

    public async Task DesactivarAsync(int id, string? usuario = null)
        => await CambiarEstadoAsync(id, EstadoAgente.Inactivo, usuario);

    // ---- Versionamiento (Actividad 8/17) ----
    public async Task<AgenteVersionDto> CrearVersionAsync(int id, string? usuario = null)
    {
        var a = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(id))
            ?? throw new KeyNotFoundException("Agente no encontrado.");
        a.Version += 1;
        var v = new AgenteVersion
        {
            Version = a.Version,
            PromptSistema = a.PromptSistema,
            ModeloIA = a.ModeloIA,
            Temperatura = a.Temperatura,
            MaxTokens = a.MaxTokens,
            Estado = a.Estado,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = usuario,
            Configuracion = SerializarConfig(a)
        };
        a.Versiones.Add(v);
        _asistenteRepository.Update(a);
        await _unitOfWork.SaveChangesAsync();
        return new AgenteVersionDto
        {
            IdAgenteVersion = v.IdAgenteVersion,
            IdAsistente = a.IdAsistente,
            Version = v.Version,
            PromptSistema = v.PromptSistema,
            ModeloIA = v.ModeloIA,
            Estado = v.Estado,
            FechaCreacion = v.FechaCreacion,
            UsuarioCreacion = v.UsuarioCreacion
        };
    }

    public async Task<AgenteVersionDto> RestaurarVersionAsync(int idAgente, int idVersion, string? usuario = null)
    {
        var a = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(idAgente))
            ?? throw new KeyNotFoundException("Agente no encontrado.");
        var version = a.Versiones.FirstOrDefault(v => v.IdAgenteVersion == idVersion)
            ?? throw new KeyNotFoundException("Versión no encontrada.");

        a.PromptSistema = version.PromptSistema;
        a.ModeloIA = version.ModeloIA;
        a.Temperatura = version.Temperatura;
        a.MaxTokens = version.MaxTokens;
        a.Estado = version.Estado;
        a.Version += 1;
        a.FechaModificacion = DateTime.UtcNow;

        var nuevaVersion = new AgenteVersion
        {
            Version = a.Version,
            PromptSistema = a.PromptSistema,
            ModeloIA = a.ModeloIA,
            Temperatura = a.Temperatura,
            MaxTokens = a.MaxTokens,
            Estado = a.Estado,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = usuario,
            Configuracion = version.Configuracion
        };
        a.Versiones.Add(nuevaVersion);
        _asistenteRepository.Update(a);
        await _unitOfWork.SaveChangesAsync();

        return new AgenteVersionDto
        {
            IdAgenteVersion = nuevaVersion.IdAgenteVersion,
            IdAsistente = a.IdAsistente,
            Version = nuevaVersion.Version,
            PromptSistema = nuevaVersion.PromptSistema,
            ModeloIA = nuevaVersion.ModeloIA,
            Estado = nuevaVersion.Estado,
            FechaCreacion = nuevaVersion.FechaCreacion,
            UsuarioCreacion = nuevaVersion.UsuarioCreacion
        };
    }

    public async Task<IEnumerable<AgenteVersionDto>> ObtenerVersionesAsync(int id)
    {
        var a = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(id))
            ?? throw new KeyNotFoundException("Agente no encontrado.");
        return a.Versiones.OrderByDescending(v => v.Version).Select(v => new AgenteVersionDto
        {
            IdAgenteVersion = v.IdAgenteVersion,
            IdAsistente = v.IdAsistente,
            Version = v.Version,
            ModeloIA = v.ModeloIA,
            Estado = v.Estado,
            FechaCreacion = v.FechaCreacion,
            UsuarioCreacion = v.UsuarioCreacion
        });
    }

    // ---- Duplicar (Actividad 1) ----
    public async Task<AsistenteDto> DuplicarAsync(int id)
    {
        var orig = (Domain.Entities.Asistente?)(await _asistenteRepository.GetByIdAsync(id))
            ?? throw new KeyNotFoundException("Agente no encontrado.");
        var copia = new Domain.Entities.Asistente
        {
            Codigo = orig.Codigo + "_copia",
            Nombre = orig.Nombre + " (copia)",
            Descripcion = orig.Descripcion,
            Objetivo = orig.Objetivo,
            PromptSistema = orig.PromptSistema,
            ModeloIA = orig.ModeloIA,
            Temperatura = orig.Temperatura,
            MaxTokens = orig.MaxTokens,
            TimeoutSegundos = orig.TimeoutSegundos,
            Idioma = orig.Idioma,
            LongitudMaximaRespuesta = orig.LongitudMaximaRespuesta,
            NivelFormalidad = orig.NivelFormalidad,
            FormatoRespuesta = orig.FormatoRespuesta,
            Restricciones = orig.Restricciones,
            MensajeBienvenida = orig.MensajeBienvenida,
            Estado = EstadoAgente.Activo,
            Version = 1,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        // Guardar primero para obtener el Id, luego aplicar asignaciones con la FK resuelta.
        await _asistenteRepository.AddAsync(copia);
        await _unitOfWork.SaveChangesAsync();
        AplicarAsignaciones(copia,
            orig.AsistentesFuentes.Where(x => x.Activo).Select(x => x.IdFuente).ToList(),
            orig.AsistentesHerramientas.Where(x => x.Activa).Select(x => x.IdHerramienta).ToList(),
            orig.AgentesWorkflows.Where(x => x.Activo).Select(x => x.IdWorkflow).ToList(),
            orig.AgentesRoles.Where(x => x.Activo).Select(x => x.IdRol).ToList(),
            orig.UsuariosAsistentes.Where(x => x.Activo).Select(x => x.IdUsuario).ToList());
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(copia);
    }

    // ---- Helpers ----
    private static void AplicarAsignaciones(Domain.Entities.Asistente a, List<int> fuentes, List<int> herramientas,
        List<int> workflows, List<int> roles, List<int> usuarios)
    {
        var id = a.IdAsistente;
        a.AsistentesFuentes = fuentes.Select(f => new AsistenteFuente { IdAsistente = id, IdFuente = f, Activo = true }).ToList();
        a.AsistentesHerramientas = herramientas.Select(h => new AsistenteHerramienta { IdAsistente = id, IdHerramienta = h, Activa = true }).ToList();
        a.AgentesWorkflows = workflows.Select(w => new AgenteWorkflow { IdAsistente = id, IdWorkflow = w, Activo = true }).ToList();
        a.AgentesRoles = roles.Select(r => new AgenteRol { IdAsistente = id, IdRol = r, Activo = true }).ToList();
        a.UsuariosAsistentes = usuarios.Select(u => new UsuarioAsistente { IdAsistente = id, IdUsuario = u, Activo = true }).ToList();
    }

    private static void SincronizarAsignaciones(Domain.Entities.Asistente a, List<int> fuentes, List<int> herramientas,
        List<int> workflows, List<int> roles, List<int> usuarios)
    {
        var id = a.IdAsistente;
        // Inicializar las colecciones si fueron puestas en null al editar.
        a.AsistentesFuentes ??= new List<AsistenteFuente>();
        a.AsistentesHerramientas ??= new List<AsistenteHerramienta>();
        a.AgentesWorkflows ??= new List<AgenteWorkflow>();
        a.AgentesRoles ??= new List<AgenteRol>();
        a.UsuariosAsistentes ??= new List<UsuarioAsistente>();

        // Limpiar las colecciones existentes (los hijos tracked se marcan para borrado)
        // y luego agregar los nuevos. Esto evita el conflicto de tracking al reasignar la lista.
        a.AsistentesFuentes.Clear();
        a.AsistentesHerramientas.Clear();
        a.AgentesWorkflows.Clear();
        a.AgentesRoles.Clear();
        a.UsuariosAsistentes.Clear();

        foreach (var f in fuentes) a.AsistentesFuentes.Add(new AsistenteFuente { IdAsistente = id, IdFuente = f, Activo = true });
        foreach (var h in herramientas) a.AsistentesHerramientas.Add(new AsistenteHerramienta { IdAsistente = id, IdHerramienta = h, Activa = true });
        foreach (var w in workflows) a.AgentesWorkflows.Add(new AgenteWorkflow { IdAsistente = id, IdWorkflow = w, Activo = true });
        foreach (var r in roles) a.AgentesRoles.Add(new AgenteRol { IdAsistente = id, IdRol = r, Activo = true });
        foreach (var u in usuarios) a.UsuariosAsistentes.Add(new UsuarioAsistente { IdAsistente = id, IdUsuario = u, Activo = true });
    }

    private static string SerializarConfig(Domain.Entities.Asistente a)
    {
        var cfg = new ConfiguracionAgente
        {
            Fuentes = a.AsistentesFuentes.Where(x => x.Activo).Select(x => x.IdFuente).ToList(),
            Herramientas = a.AsistentesHerramientas.Where(x => x.Activa).Select(x => x.IdHerramienta).ToList(),
            Workflows = a.AgentesWorkflows.Where(x => x.Activo).Select(x => x.IdWorkflow).ToList(),
            Roles = a.AgentesRoles.Where(x => x.Activo).Select(x => x.IdRol).ToList(),
            Usuarios = a.UsuariosAsistentes.Where(x => x.Activo).Select(x => x.IdUsuario).ToList(),
            Restricciones = a.Restricciones,
            FormatoRespuesta = a.FormatoRespuesta
        };
        return System.Text.Json.JsonSerializer.Serialize(cfg);
    }

    private static AsistenteDto MapToDto(Domain.Entities.Asistente a) => new()
    {
        IdAsistente = a.IdAsistente,
        Codigo = a.Codigo,
        Nombre = a.Nombre,
        Descripcion = a.Descripcion,
        Objetivo = a.Objetivo,
        PromptSistema = a.PromptSistema,
        ModeloIA = a.ModeloIA,
        Estado = a.Estado,
        Version = a.Version,
        Activo = a.Activo,
        FechaCreacion = a.FechaCreacion,
        FechaModificacion = a.FechaModificacion,
        Idioma = a.Idioma,
        LongitudMaximaRespuesta = a.LongitudMaximaRespuesta,
        NivelFormalidad = a.NivelFormalidad,
        FormatoRespuesta = a.FormatoRespuesta,
        Restricciones = a.Restricciones,
        MensajeBienvenida = a.MensajeBienvenida,
        Temperatura = a.Temperatura,
        MaxTokens = a.MaxTokens,
        TimeoutSegundos = a.TimeoutSegundos,
        Fuentes = a.AsistentesFuentes.Where(x => x.Activo).Select(x => x.IdFuente).ToList(),
        Herramientas = a.AsistentesHerramientas.Where(x => x.Activa).Select(x => x.IdHerramienta).ToList(),
        Workflows = a.AgentesWorkflows.Where(x => x.Activo).Select(x => x.IdWorkflow).ToList(),
        Roles = a.AgentesRoles.Where(x => x.Activo).Select(x => x.IdRol).ToList(),
        Usuarios = a.UsuariosAsistentes.Where(x => x.Activo).Select(x => x.IdUsuario).ToList()
    };
}
