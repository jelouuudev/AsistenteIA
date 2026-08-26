using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IAsistenteRepository
{
    Task<Entities.Asistente?> GetByIdAsync(int id);
    Task<Entities.Asistente?> GetByCodigoAsync(string codigo);
    Task<IEnumerable<Entities.Asistente>> GetAllAsync();
    Task<IEnumerable<Entities.Asistente>> GetAutorizadosParaUsuarioAsync(int idUsuario, IEnumerable<int> rolesUsuario);
    /// <summary>Códigos de herramientas activas del agente, consultados de forma aislada
    /// (AsNoTracking) para no depender del Include de GetAllAsync ni del identity-map.</summary>
    Task<List<string>> GetHerramientasActivasAsync(int idAgente);
    Task AddAsync(Entities.Asistente asistente);
    void Update(Entities.Asistente asistente);
    void Delete(Entities.Asistente asistente);
    Task EliminarAsignacionesAsync(int idAsistente);
}
