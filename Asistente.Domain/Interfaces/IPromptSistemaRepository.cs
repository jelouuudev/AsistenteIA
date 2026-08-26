using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IPromptSistemaRepository
{
    Task<PromptSistema?> GetByIdAsync(int id);
    Task<PromptSistema?> GetActiveByAsistenteIdAsync(int asistenteId);
    Task<IEnumerable<PromptSistema>> GetByAsistenteIdAsync(int asistenteId);
    Task<IEnumerable<PromptSistema>> GetAllAsync();
    Task AddAsync(PromptSistema prompt);
    void Update(PromptSistema prompt);
    void Delete(PromptSistema prompt);
    Task<int> GetNextVersionAsync(int asistenteId);
}
