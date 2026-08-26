using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IHistorialPromptRepository
{
    Task<HistorialPrompt?> GetByIdAsync(int id);
    Task<IEnumerable<HistorialPrompt>> GetByPromptIdAsync(int promptId);
    Task<IEnumerable<HistorialPrompt>> GetAllAsync();
    Task AddAsync(HistorialPrompt historial);
    void Delete(HistorialPrompt historial);
}
