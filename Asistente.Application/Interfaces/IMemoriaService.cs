using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IMemoriaService
{
    Task<IEnumerable<ConversacionListDto>> ObtenerConversacionesAsync(int usuarioId);
    Task<IEnumerable<ConversacionListDto>> BuscarConversacionesAsync(int usuarioId, string titulo);
    Task<ConversacionDto?> ObtenerConversacionAsync(int id);
    Task<ConversacionDto> CrearConversacionAsync(int usuarioId);
    Task RenombrarConversacionAsync(int id, string titulo);
    Task ArchivarConversacionAsync(int id);
    Task EliminarConversacionAsync(int id);
    Task<string?> GenerarTituloAsync(int idConversacion, string primerMensaje, string respuesta);
    Task<DebugContextoDto> ObtenerDebugContextoAsync(int idConversacion);
}
