using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class PromptSistemaService
{
    private readonly IPromptSistemaRepository _promptRepository;
    private readonly IHistorialPromptRepository _historialRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PromptSistemaService(
        IPromptSistemaRepository promptRepository,
        IHistorialPromptRepository historialRepository,
        IUnitOfWork unitOfWork)
    {
        _promptRepository = promptRepository;
        _historialRepository = historialRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PromptSistemaDto?> ObtenerPorIdAsync(int id)
    {
        var prompt = await _promptRepository.GetByIdAsync(id);
        if (prompt == null) return null;
        return MapToDto(prompt);
    }

    public async Task<PromptSistemaDto?> ObtenerActivoPorAsistenteIdAsync(int asistenteId)
    {
        var prompt = await _promptRepository.GetActiveByAsistenteIdAsync(asistenteId);
        if (prompt == null) return null;
        return MapToDto(prompt);
    }

    public async Task<IEnumerable<PromptSistemaDto>> ObtenerPorAsistenteIdAsync(int asistenteId)
    {
        var prompts = await _promptRepository.GetByAsistenteIdAsync(asistenteId);
        return prompts.Select(MapToDto);
    }

    public async Task<IEnumerable<PromptSistemaDto>> ObtenerTodosAsync()
    {
        var prompts = await _promptRepository.GetAllAsync();
        return prompts.Select(MapToDto);
    }

    public async Task<PromptSistemaDto> CrearPromptAsync(CrearPromptRequest request)
    {
        var nextVersion = await _promptRepository.GetNextVersionAsync(request.IdAsistente);

        var prompt = new PromptSistema
        {
            IdAsistente = request.IdAsistente,
            Nombre = request.Nombre,
            Contenido = request.Contenido,
            Version = nextVersion,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = request.UsuarioCreacion
        };

        await _promptRepository.AddAsync(prompt);
        await _unitOfWork.SaveChangesAsync();

        // Guardar en historial
        var historial = new HistorialPrompt
        {
            IdPrompt = prompt.IdPrompt,
            Version = prompt.Version,
            Contenido = prompt.Contenido,
            FechaModificacion = DateTime.UtcNow,
            UsuarioModificacion = request.UsuarioCreacion,
            MotivoCambio = "Creación inicial"
        };

        await _historialRepository.AddAsync(historial);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(prompt);
    }

    public async Task<PromptSistemaDto> ActualizarPromptAsync(int id, ActualizarPromptRequest request, string usuarioModificacion)
    {
        var prompt = await _promptRepository.GetByIdAsync(id);
        if (prompt == null)
            throw new KeyNotFoundException("Prompt no encontrado.");

        // Guardar versión anterior en historial
        var historial = new HistorialPrompt
        {
            IdPrompt = prompt.IdPrompt,
            Version = prompt.Version,
            Contenido = prompt.Contenido,
            FechaModificacion = DateTime.UtcNow,
            UsuarioModificacion = usuarioModificacion,
            MotivoCambio = request.MotivoCambio ?? "Modificación"
        };

        await _historialRepository.AddAsync(historial);

        // Actualizar prompt con nueva versión
        prompt.Nombre = request.Nombre;
        prompt.Contenido = request.Contenido;
        prompt.Activo = request.Activo;
        prompt.Version = await _promptRepository.GetNextVersionAsync(prompt.IdAsistente);

        _promptRepository.Update(prompt);
        await _unitOfWork.SaveChangesAsync();

        // Guardar nueva versión en historial
        var nuevoHistorial = new HistorialPrompt
        {
            IdPrompt = prompt.IdPrompt,
            Version = prompt.Version,
            Contenido = prompt.Contenido,
            FechaModificacion = DateTime.UtcNow,
            UsuarioModificacion = usuarioModificacion,
            MotivoCambio = request.MotivoCambio ?? "Nueva versión"
        };

        await _historialRepository.AddAsync(nuevoHistorial);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(prompt);
    }

    public async Task ActivarPromptAsync(int id, string usuarioModificacion)
    {
        var prompt = await _promptRepository.GetByIdAsync(id);
        if (prompt == null)
            throw new KeyNotFoundException("Prompt no encontrado.");

        // Desactivar todos los prompts del asistente
        var promptsAsistente = await _promptRepository.GetByAsistenteIdAsync(prompt.IdAsistente);
        foreach (var p in promptsAsistente)
        {
            p.Activo = false;
            _promptRepository.Update(p);
        }

        // Activar el prompt seleccionado
        prompt.Activo = true;
        _promptRepository.Update(prompt);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DesactivarPromptAsync(int id)
    {
        var prompt = await _promptRepository.GetByIdAsync(id);
        if (prompt == null)
            throw new KeyNotFoundException("Prompt no encontrado.");

        prompt.Activo = false;
        _promptRepository.Update(prompt);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task EliminarPromptAsync(int id)
    {
        var prompt = await _promptRepository.GetByIdAsync(id);
        if (prompt == null)
            throw new KeyNotFoundException("Prompt no encontrado.");

        _promptRepository.Delete(prompt);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PromptSistemaDto> DuplicarPromptAsync(int id, string usuarioModificacion)
    {
        var promptOriginal = await _promptRepository.GetByIdAsync(id);
        if (promptOriginal == null)
            throw new KeyNotFoundException("Prompt no encontrado.");

        var nextVersion = await _promptRepository.GetNextVersionAsync(promptOriginal.IdAsistente);

        var nuevoPrompt = new PromptSistema
        {
            IdAsistente = promptOriginal.IdAsistente,
            Nombre = $"{promptOriginal.Nombre} (copia)",
            Contenido = promptOriginal.Contenido,
            Version = nextVersion,
            Activo = false,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = usuarioModificacion
        };

        await _promptRepository.AddAsync(nuevoPrompt);
        await _unitOfWork.SaveChangesAsync();

        var historial = new HistorialPrompt
        {
            IdPrompt = nuevoPrompt.IdPrompt,
            Version = nuevoPrompt.Version,
            Contenido = nuevoPrompt.Contenido,
            FechaModificacion = DateTime.UtcNow,
            UsuarioModificacion = usuarioModificacion,
            MotivoCambio = $"Duplicado desde prompt {id} (v{promptOriginal.Version})"
        };

        await _historialRepository.AddAsync(historial);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(nuevoPrompt);
    }

    public async Task<IEnumerable<HistorialPromptDto>> ObtenerHistorialAsync(int promptId)
    {
        var historial = await _historialRepository.GetByPromptIdAsync(promptId);
        return historial.Select(MapHistorialToDto);
    }

    private static PromptSistemaDto MapToDto(PromptSistema prompt)
    {
        return new PromptSistemaDto
        {
            IdPrompt = prompt.IdPrompt,
            IdAsistente = prompt.IdAsistente,
            Nombre = prompt.Nombre,
            Contenido = prompt.Contenido,
            Version = prompt.Version,
            Activo = prompt.Activo,
            FechaCreacion = prompt.FechaCreacion,
            UsuarioCreacion = prompt.UsuarioCreacion,
            AsistenteNombre = prompt.Asistente?.Nombre
        };
    }

    private static HistorialPromptDto MapHistorialToDto(HistorialPrompt historial)
    {
        return new HistorialPromptDto
        {
            IdHistorial = historial.IdHistorial,
            IdPrompt = historial.IdPrompt,
            Version = historial.Version,
            Contenido = historial.Contenido,
            FechaModificacion = historial.FechaModificacion,
            UsuarioModificacion = historial.UsuarioModificacion,
            MotivoCambio = historial.MotivoCambio,
            PromptNombre = historial.Prompt?.Nombre
        };
    }
}
